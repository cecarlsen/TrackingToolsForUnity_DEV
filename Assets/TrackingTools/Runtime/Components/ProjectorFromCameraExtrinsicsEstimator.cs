/*
	Copyright © Carl Emil Carlsen 2020-2022
	http://cec.dk

	Based on and inspired by the following (but written from scratch).

		Elliot Woods and Kyle McDonald did a workshop in 2011.
		http://artandcode.com/3d/workshops/4a-calibrating-projectors-and-cameras/

		Cassinelli Alvaro improved on the solution
		https://www.youtube.com/watch?v=pCq7u2TvlxU

	Note: Perhaps interesting solution by DTU students.
	https://backend.orbit.dtu.dk/ws/portalfiles/portal/91373186/PhotonicsWest2014.pdf
*/

using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Experimental.Rendering;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.Calib3dModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.ImgprocModule;

namespace TrackingTools
{
	/// <summary>
	/// Estimates intrinsics and extriniscs of a video projector relative to a camera with known intrinsics.
	/// </summary>
	public class ProjectorFromCameraExtrinsicsEstimator : MonoBehaviour
	{
		[ Header("Input")]
		[SerializeField] Texture _cameraSourceTexture = null;
		[SerializeField,ReadOnlyAtRuntime] string _cameraIntrinsicsFileName = "DefaultCamera";
		[SerializeField] ProjectorCheckerboard _projectorCheckerboard = null;
		[SerializeField] bool _flipCameraTextureVertically = false;
		[SerializeField,ReadOnlyAtRuntime] OperationMode _operationMode = OperationMode.TimedSampling;

		[ Header( "Output" )]
		[SerializeField] string _projectorIntrinsicsFileName = "DefaultProjector_1280x720";
		[SerializeField] bool _addErrorValueToIntrinsicsFileName = false;
		[SerializeField] string _projectorExtrinsicsFileName = "DefaultProjector_1280x720_From_DefaultCamera_1280x720";

		[Header("UI")]
		[SerializeField,ReadOnlyAtRuntime] Camera _mainCamera = null;
		[SerializeField,ReadOnlyAtRuntime] Camera _projectorCamera = null;
		[SerializeField,ReadOnlyAtRuntime] RawImage _processedCameraImage = null;
		[SerializeField,ReadOnlyAtRuntime] Slider _circlePatternScaleSlider = null;
		[SerializeField,ReadOnlyAtRuntime] Slider _circlePatternOffsetXSlider = null;
		[SerializeField,ReadOnlyAtRuntime] Slider _circlePatternOffsetYSlider = null;
		[SerializeField,ReadOnlyAtRuntime] Image _stableSampleMeterFillImage = null;
		[SerializeField,ReadOnlyAtRuntime] Text _sampleCountText = null;
		[SerializeField,ReadOnlyAtRuntime] Text _intrinsicsErrorText = null;
		[SerializeField,ReadOnlyAtRuntime] Text _extrinsicsErrorText = null;
		[SerializeField,ReadOnlyAtRuntime] Button _saveButton = null;
		[SerializeField,ReadOnlyAtRuntime] Button _undoSampleButton = null;
		[SerializeField,ReadOnlyAtRuntime] Button _manualSampleButton = null;

		[Header("Debug")]
		[SerializeField] bool _showDotGizmos = false;

		Intrinsics _cameraIntrinsics;
		ExtrinsicsCalibrator _cameraExtrinsicsCalibrator;
		IntrinsicsCalibrator _projectorIntrinsicsCalibrator;
		ProjectorFromCameraExtrinsicsCalibrator _projectorExtrinsicsCalibrator;

		State _state = State.Initiating;

		Mat _camTexMat;
		Mat _camTexGrayMat;
		Mat _camTexGrayUndistortMat;
		Mat _camTexGrayUndistortInvMat;
		Texture2D _processedCameraTexture;

		Color32[] _tempTransferColors;
		Texture2D _tempTransferTexture;

		RenderTexture _arTexture;

		Mat _sensorMat;
		MatOfDouble _distortionCoeffsMat;
		MatOfDouble _noDistCoeffs;

		MatOfPoint2f _chessCornersImageMat;
		MatOfPoint3f _chessCornersWorldMat;

		MatOfPoint2f _circlePointsProjectorRenderImageMat;
		MatOfPoint3f _circlePointsRenderedWorldMat;
		MatOfPoint2f _circlePointsCameraImageMat;
		MatOfPoint3f _circlePointsRealModelMat;
		MatOfPoint3f _circlePointsDetectedWorldMat;

		Mat _undistortMap1;
		Mat _undistortMap2;

		bool _dirtyTexture;

		RenderTexture _chessPatternTexture;
		RenderTexture _circlePatternTexture;

		MaterialPropFlasher _previewFlasher;
		RawImage _arImage;
		AspectRatioFitter _cameraAspectFitter;

		Material _previewMaterial;
		Material _circlePatternBoardMaterial;
		Material _screenBorderMaterial;
		Material _patternRenderMaterial;

		Transform _calibrationBoardTransform;
		Transform _chessPatternTransform;
		Transform _circlePatternTransform;
		GameObject _precisionDotsContainerObject;
		Transform _projectorSampleMeterTransform;

		//float _circleChessOffset;

		Plane _calibrationboardPlane;

		Vector2 _circlePatternBorderSizeUV;

		Matrix4x4 _circlePatternToWorldPrevFrame;
		Matrix4x4 _chessPatternToWorldPrevFrame;

		Vector2Int _circlePatternSize;
		int _stableFrameCount;
		int _circlePatternPointCount;

		float _extrinsicsErrorAvgM; // in meters

		int _stableSampleCountThreshold;
		bool _sampleManuallyRequested;

		bool _manualCirclePatternTransformationRequested = true;


		StringBuilder _sb;

		static Vector3[] edgeCornersNormalized = {
			new Vector3( -0.5f, 0.5f, 0 ),
			new Vector3( 0.5f, 0.5f, 0 ),
			new Vector3( 0.5f, -0.5f, 0 ),
			new Vector3( -0.5f, -0.5f, 0 ),
		};
		static readonly Vector2Int defaultCirclesPatternSize = new Vector2Int( 3, 9 ); // We need a patter without rotational symmetry, so that OpenCV can figure out what is up and down.
		const float circlePatternBorder = 0.5f; // Relative to horizontal tile size.
		const float lowMovementThreshold = 0.0008f; // Average chess pattern board movement in meters.
		const int blindSampleCountTarget = 4;
		const int stableFrameCountThresholdForTimedSampling = 20;
		const int stableFrameCountThresholdForManualSampling = 5;

		static readonly string logPrepend = "<b>[" + nameof( ProjectorFromCameraExtrinsicsEstimator ) + "]</b> ";


		public Texture cameraSourceTexture {
			get { return _cameraSourceTexture; }
			set {
				_cameraSourceTexture = value;
				_dirtyTexture = true;
			}
		}

		public int sampleCount { get { return _projectorIntrinsicsCalibrator == null ? 0 : _projectorIntrinsicsCalibrator.sampleCount; } }


		[System.Serializable]
		public enum OperationMode
		{
			ManualSamlping,
			TimedSampling
		}


		enum State
		{
			Initiating,
			BlindCalibration,		// Project circle pattern in center of the projector image. 
			TrackedCalibration,		// Project onto calibration board.
			Testing
		}



		void Awake()
		{
			Application.targetFrameRate = 30;

			// Check resources.
			if( _operationMode == OperationMode.ManualSamlping && !_manualSampleButton ) {
				Debug.LogError( logPrepend + "Missing sample button. You must provide a sample button when OperationMode is " + OperationMode.ManualSamlping );
				enabled = false;
				return;
			}

			// Load files.
			if( !Intrinsics.TryLoadFromFile( _cameraIntrinsicsFileName, out _cameraIntrinsics ) ) {
				Debug.LogError( logPrepend + "Failed. Intrinsics file '" + _cameraIntrinsicsFileName + "' is missing.");
				enabled = false;
				return;
			}

			// Find shaders.
			Shader unlitColorShader = Shader.Find( "Hidden/UnlitColor" );
			Shader unlitTextureShader = Shader.Find( "Hidden/UnlitTexture" );
			Shader unlitTintedTextureShader = Shader.Find( "Hidden/UnlitTintedInvertibleTexture" );

			_sb = new StringBuilder();

			// Operation mode dependent things.
			_stableSampleCountThreshold = _operationMode == OperationMode.ManualSamlping ? stableFrameCountThresholdForManualSampling : stableFrameCountThresholdForTimedSampling;
			if( _manualSampleButton ){
				_manualSampleButton.gameObject.SetActive( _operationMode == OperationMode.ManualSamlping );
				_manualSampleButton.interactable = false;
			}

			// Prepare OpenCV.
			_cameraExtrinsicsCalibrator = new ExtrinsicsCalibrator();
			_projectorExtrinsicsCalibrator = new ProjectorFromCameraExtrinsicsCalibrator();
			_noDistCoeffs = new MatOfDouble( new double[] { 0, 0, 0, 0 } );
			_circlePointsProjectorRenderImageMat = new MatOfPoint2f();
			_circlePointsRealModelMat = new MatOfPoint3f();
			_circlePointsDetectedWorldMat = new MatOfPoint3f();
			_undistortMap1 = new Mat();
			_undistortMap2 = new Mat();

			// Create patterns.
			TrackingToolsHelper.RenderPattern( _projectorCheckerboard.checkerPatternSize, TrackingToolsHelper.PatternType.Checkerboard, 1024, ref _chessPatternTexture, ref _patternRenderMaterial );

			// Layers.
			int uiLayer = LayerMask.NameToLayer( "UI" );
			int mainCameraLayerMask = LayerMask.GetMask( "Default" );
			int projectorLayer = LayerMask.NameToLayer( "TransparentFX" );
			int projectorLayerMask = LayerMask.GetMask( "TransparentFX" );

			// Objects.
			_calibrationBoardTransform = new GameObject( "CalibrationBoard" ).transform; 

			// Create and prepare UI.
			_cameraAspectFitter = _processedCameraImage.GetComponent<AspectRatioFitter>();
			if( !_cameraAspectFitter ) _cameraAspectFitter = _processedCameraImage.gameObject.AddComponent<AspectRatioFitter>();
			_cameraAspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
			_previewMaterial = new Material( Shader.Find( TrackingToolsConstants.previewShaderName ) );
			_processedCameraImage.gameObject.layer = uiLayer;
			_processedCameraImage.material = _previewMaterial;
			_processedCameraImage.color = Color.white;

			_arImage = new GameObject( "ARImage" ).AddComponent<RawImage>();
			_arImage.transform.SetParent( _processedCameraImage.transform );
			_arImage.transform.SetAsFirstSibling();
			_arImage.raycastTarget = false;
			_arImage.rectTransform.FitParent();
			_arImage.gameObject.layer = uiLayer;

			_mainCamera.transform.SetPositionAndRotation( Vector3.zero, Quaternion.identity );
			_mainCamera.cullingMask = mainCameraLayerMask;

			_projectorCamera.transform.SetPositionAndRotation( Vector3.zero, Quaternion.identity );
			_projectorCamera.cullingMask = projectorLayerMask;
			_projectorCamera.usePhysicalProperties = false;

			_chessPatternTransform = GameObject.CreatePrimitive( PrimitiveType.Quad ).transform;
			_chessPatternTransform.SetParent( _calibrationBoardTransform );
			_chessPatternTransform.name = "Chessboard";
			Material chessboardMaterial = new Material( unlitTextureShader );
			chessboardMaterial.mainTexture = _chessPatternTexture;
			_chessPatternTransform.GetComponent<Renderer>().material = chessboardMaterial;
			float chessTileSizeMeters = _projectorCheckerboard.checkerTileSize * 0.001f;
			_chessPatternTransform.localScale = new Vector3( (_projectorCheckerboard.checkerPatternSize.x-1) * chessTileSizeMeters, (_projectorCheckerboard.checkerPatternSize.y-1) * chessTileSizeMeters, 0 );
			TrackingToolsHelper.UpdateWorldSpacePatternPoints( _projectorCheckerboard.checkerPatternSize, _chessPatternTransform.localToWorldMatrix, TrackingToolsHelper.PatternType.Checkerboard, Vector2.zero, ref _chessCornersWorldMat );

			_circlePatternTransform = GameObject.CreatePrimitive( PrimitiveType.Quad ).transform;
			_circlePatternTransform.name = "Circlesboard";
			_circlePatternBoardMaterial = new Material( unlitTintedTextureShader );
			_circlePatternTransform.GetComponent<Renderer>().material = _circlePatternBoardMaterial;
			_circlePatternTransform.position = Vector3.forward;
			_circlePatternTransform.gameObject.layer = projectorLayer;

			_precisionDotsContainerObject = TrackingToolsHelper.CreatePrecisionTestDots( _calibrationBoardTransform, projectorLayer, _projectorCheckerboard.checkerPatternSize, chessTileSizeMeters );
			_precisionDotsContainerObject.SetActive( false );

			_projectorSampleMeterTransform = GameObject.CreatePrimitive( PrimitiveType.Quad ).transform;
			_projectorSampleMeterTransform.gameObject.layer = projectorLayer;
			_projectorSampleMeterTransform.name = "ProjectorSampleMeter";
			_projectorSampleMeterTransform.localScale = new Vector3( _chessPatternTransform.localScale.x, TrackingToolsConstants.precisionTestDotSize, 0 );
			_projectorSampleMeterTransform.SetParent( _calibrationBoardTransform );
			float dotOffsetY = ( ( _projectorCheckerboard.checkerPatternSize.y - 4 ) * 0.5f + 1 ) * chessTileSizeMeters;
			_projectorSampleMeterTransform.localPosition = new Vector3( 0, - dotOffsetY - chessTileSizeMeters );
			Material sampleMeterMaterial = new Material( unlitColorShader );
			_projectorSampleMeterTransform.GetComponent<Renderer>().sharedMaterial = sampleMeterMaterial;
			_projectorSampleMeterTransform.gameObject.SetActive( false );

			_intrinsicsErrorText.gameObject.SetActive( false );
			_extrinsicsErrorText.gameObject.SetActive( false );
			_saveButton.gameObject.SetActive( false );
			_undoSampleButton.gameObject.SetActive( false );

			_screenBorderMaterial = new Material( unlitColorShader );

			_circlePatternToWorldPrevFrame = Matrix4x4.identity;

			_previewFlasher = new MaterialPropFlasher( _previewMaterial, "_Whiteout", TrackingToolsConstants.flashDuration );
			UpdateSampleCounterUI();

			// Subscribe.
			_circlePatternScaleSlider.onValueChanged.AddListener( ( float v ) => _manualCirclePatternTransformationRequested = true );//OnCirclePatternScaleSliderChanged );
			_circlePatternOffsetXSlider.onValueChanged.AddListener( ( float v ) => _manualCirclePatternTransformationRequested = true );
			_circlePatternOffsetYSlider.onValueChanged.AddListener( ( float v ) => _manualCirclePatternTransformationRequested = true );
			_saveButton.onClick.AddListener( SaveToFiles );
			_undoSampleButton.onClick.AddListener( UndoSample );
			if( _manualSampleButton ) {
				_manualSampleButton.onClick.AddListener( () => _sampleManuallyRequested = true); ;
			}
		}


		void OnDestroy()
		{
			if( _camTexMat != null ) _camTexMat.release();
			if( _camTexGrayMat != null ) _camTexGrayMat.release();
			if( _camTexGrayUndistortMat != null ) _camTexGrayUndistortMat.release();
			if( _camTexGrayUndistortInvMat != null ) _camTexGrayUndistortInvMat.release();
			if( _sensorMat != null ) _sensorMat.release();
			if( _distortionCoeffsMat != null ) _distortionCoeffsMat.release();
			if( _noDistCoeffs != null ) _noDistCoeffs.release();
			if( _arTexture ) _arTexture.Release();
			if( _previewMaterial ) Destroy( _previewMaterial );
			if( _chessPatternTransform ) Destroy( _chessPatternTransform.gameObject );
			if( _chessPatternTexture ) _chessPatternTexture.Release();
			if( _circlePatternTexture ) _chessPatternTexture.Release();
			if( _projectorIntrinsicsCalibrator != null ) _projectorIntrinsicsCalibrator.Clear();
			if( _cameraExtrinsicsCalibrator != null ) _cameraExtrinsicsCalibrator.Release();
			if( _screenBorderMaterial != null ) Destroy( _screenBorderMaterial );
			if( _undistortMap1 != null ) _undistortMap1.release();
			if( _undistortMap2 != null ) _undistortMap2.release();
		}


		void OnValidate()
		{
			
		}


		void Update()
		{
			if( !_cameraSourceTexture || !_dirtyTexture ) return;

			// Adapt resources.
			if( !AdaptResources() ) return;

			// Preprocess camera image.
			PreProcessCameraImage();

			// Find and apply chessboard extrinsics.
			bool foundChessPattern = FindAndApplyChessPatternExtrinsics();

			// Chessboard dependent routine.
			bool foundCirclePattern = false;
			if( foundChessPattern )
			{
				// Detect circle pattern.
				foundCirclePattern = DetectAndRaycastCirclePatternOntoChessboardPlane();

				// Compute where the circle pattern was located in the projector camera image (needed for ComputeAverageMovement and sampling).
				if( foundCirclePattern ) UpdateCirclePatternInProjectorImage();

				// TODO we could check when the error is lowest (because of continous stillness) and sample at that moment.
				if( foundCirclePattern && ( _state == State.TrackedCalibration || _state == State.Testing ) ) ComputeRenderedVsDetectedError();

				if( _state != State.Testing )
				{
					// Check movement and update stable frame counter.
					if( foundCirclePattern && ComputeAverageWorldSpaceMovement() < lowMovementThreshold ){
						_stableFrameCount++;
					} else { 
						_stableFrameCount = 0;
						_sampleManuallyRequested = false;
					}

					// Sample when continuously stable.
					if(
						( _stableFrameCount >= _stableSampleCountThreshold && _circlePatternPointCount == _circlePointsDetectedWorldMat.rows() ) &&
						( _operationMode == OperationMode.TimedSampling || ( _operationMode == OperationMode.ManualSamlping && _sampleManuallyRequested ) )
					){
						Sample();
					}
				}

				// Update circle pattern size. We do this lastly because it is likely that we will detect the circles in next frame.
				UpdateCirclePatternSize();
			}

			// Update UI.
			Utils.fastMatToTexture2D( _camTexGrayUndistortMat, _processedCameraTexture ); // Flips the texture vertically by default
			_stableSampleMeterFillImage.fillAmount = _stableFrameCount / (float) _stableSampleCountThreshold;
			_previewFlasher.Update();
			if( _previewFlasher.changed ) _circlePatternBoardMaterial.color = Color.Lerp( Color.white, Color.black, _previewFlasher.value );
			if( _intrinsicsErrorText.gameObject.activeSelf ) _intrinsicsErrorText.text = _projectorIntrinsicsCalibrator.rmsError.ToString( "F2" );
			if( _extrinsicsErrorText.gameObject.activeSelf ) _extrinsicsErrorText.text = foundCirclePattern ? ( _extrinsicsErrorAvgM * 100f ).ToString( "F2" ) : "-"; // Cm
			_undoSampleButton.gameObject.SetActive( sampleCount > blindSampleCountTarget && _state != State.Testing );

			// Projector UI.
			if( _state == State.TrackedCalibration || _state == State.Testing ){
				if( foundCirclePattern ) UpdateSampleMeterProjectorUI();
				_projectorSampleMeterTransform.gameObject.SetActive( foundCirclePattern );
				_precisionDotsContainerObject.SetActive( foundChessPattern );
			}

			// Remember where the chess and circle patterns where this frame.
			_chessPatternToWorldPrevFrame = _chessPatternTransform.localToWorldMatrix;
			_circlePatternToWorldPrevFrame = _circlePatternTransform.localToWorldMatrix;

			// Only allow manual sampling when stable.
			if( _operationMode == OperationMode.ManualSamlping ) _manualSampleButton.interactable = _stableFrameCount >= _stableSampleCountThreshold;

			// Manual circleboard transformation.
			if( _state == State.BlindCalibration && _manualCirclePatternTransformationRequested ) UpdateManualCircleBoardTransformation();

			// Done.
			_dirtyTexture = false;
		}



		void OnRenderObject()
		{
			Camera cam = Camera.current;
			if( cam.targetDisplay != 1 ) return;

			// Draw 1px border on projection.
			_screenBorderMaterial.SetPass( 0 );
			GL.LoadPixelMatrix();
			GL.Begin( GL.LINE_STRIP );
			GL.Vertex3( 1, 1, 0 );
			GL.Vertex3( cam.pixelWidth-1, 0, 0 );
			GL.Vertex3( cam.pixelWidth-1, cam.pixelHeight-1, 0 );
			GL.Vertex3( 1, cam.pixelHeight-1, 0 );
			GL.Vertex3( 1, 1, 0 );
			GL.End();
		}


		bool AdaptResources()
		{
			int camWidth = _cameraSourceTexture.width;
			int camHeight = _cameraSourceTexture.height;
			if( _processedCameraTexture != null && _processedCameraTexture.width == camWidth && _processedCameraTexture.height == camHeight ) return true;

			bool intrinsicsConversionSuccess = _cameraIntrinsics.ApplyToToOpenCV( ref _sensorMat, ref _distortionCoeffsMat );//, w, h );
			if( !intrinsicsConversionSuccess ) return false;
			_cameraIntrinsics.ApplyToUnityCamera( _mainCamera );

			// Get resolution of projector.
			int projWidth, projHeight;
			int projectorTargetDisplayIndex = _projectorCamera.targetDisplay;
			List<DisplayInfo> displayInfos = new List<DisplayInfo>();
			Screen.GetDisplayLayout( displayInfos );
			if( projectorTargetDisplayIndex < displayInfos.Count ) {
				DisplayInfo projDisplay = displayInfos[ projectorTargetDisplayIndex ];
				projWidth = projDisplay.width;
				projHeight = projDisplay.height;
			} else {
				// Fallback.
				projWidth = camWidth;
				projHeight = camHeight;
			}

			// Ensure that camera has right aspect.
			float projectorAspect = projWidth / (float) projHeight;
			_projectorCamera.usePhysicalProperties = true;
			_projectorCamera.gateFit = Camera.GateFitMode.None;
			_projectorCamera.orthographic = false;
			_projectorCamera.sensorSize = new Vector2( _projectorCamera.sensorSize.y * projectorAspect, _projectorCamera.sensorSize.y );

			// Create projector calibrator
			_projectorIntrinsicsCalibrator = new IntrinsicsCalibrator( projWidth, projHeight );

			// Create textures.
			_camTexGrayMat = new Mat( camHeight, camWidth, CvType.CV_8UC1 );
			_camTexGrayUndistortMat = new Mat( camHeight, camWidth, CvType.CV_8UC1 );
			_camTexGrayUndistortInvMat = new Mat( camHeight, camWidth, CvType.CV_8UC1 );

			_processedCameraTexture = new Texture2D( camWidth, camHeight, GraphicsFormat.R8_UNorm, 0, TextureCreationFlags.None );
			_processedCameraTexture.name = "ProcessedCameraTex";

			_arTexture = new RenderTexture( camWidth, camHeight, 16, GraphicsFormat.R8G8B8A8_UNorm );
			_arTexture.name = "AR Texture";

			// Update circle pattern size.
			UpdateCirclePatternSize();

			// Create undistort map.
			Calib3d.initUndistortRectifyMap( _sensorMat, _distortionCoeffsMat, new Mat(), _sensorMat, new Size( _cameraSourceTexture.width, _cameraSourceTexture.height ), CvType.CV_32FC1, _undistortMap1, _undistortMap2 );

			// Switch state.
			SwitchState( State.BlindCalibration );

			// Update UI.
			_processedCameraImage.texture = _processedCameraTexture;
			_arImage.texture = _arTexture;
			_cameraAspectFitter.aspectRatio = camWidth / (float) camHeight;
			_mainCamera.targetTexture = _arTexture;

			return true;
		}


		void Sample()
		{
			_projectorIntrinsicsCalibrator.AddSample( _circlePointsRealModelMat, _circlePointsProjectorRenderImageMat );
			_projectorExtrinsicsCalibrator.AddSample( _circlePointsDetectedWorldMat, _circlePointsCameraImageMat, _circlePointsProjectorRenderImageMat );
			_stableFrameCount = 0;

			// Update projector intrinsics and extrinnsics.
			if( sampleCount >= blindSampleCountTarget ) UpdateProjectorIntrinsicsAndExtrinsics();

			// State switching.
			if( _state == State.BlindCalibration && sampleCount == blindSampleCountTarget ) SwitchState( State.TrackedCalibration );

			// Update UI.
			_previewFlasher.Start();
			UpdateSampleCounterUI();

			_sampleManuallyRequested = false;
		}


		void PreProcessCameraImage()
		{
			// Convert texture to mat (if the texture looks right in Unity, then it needs to be flipped for OpenCV).
			TrackingToolsHelper.TextureToMat( _cameraSourceTexture, !_flipCameraTextureVertically, ref _camTexMat, ref _tempTransferColors, ref _tempTransferTexture );

			// Convert to grayscale if more than one channel, else copy (and convert bit rate if necessary).
			TrackingToolsHelper.ColorMatToLumanceMat( _camTexMat, _camTexGrayMat );

			// Undistort.
			//Calib3d.undistort( _camTexGrayMat, _camTexGrayUndistortMat, _sensorMat, _distortionCoeffsMat );
			Imgproc.remap( _camTexGrayMat, _camTexGrayUndistortMat, _undistortMap1, _undistortMap2, Imgproc.INTER_LINEAR );
		}


		bool FindAndApplyChessPatternExtrinsics()
		{
			bool found = TrackingToolsHelper.FindChessboardCorners( _camTexGrayUndistortMat, _projectorCheckerboard.checkerPatternSize, ref _chessCornersImageMat, fastAndImprecise: true );
			if( found ) {
				TrackingToolsHelper.DrawFoundPattern( _camTexGrayUndistortMat, _projectorCheckerboard.checkerPatternSize, _chessCornersImageMat );
				_cameraExtrinsicsCalibrator.UpdateExtrinsics( _chessCornersWorldMat, _chessCornersImageMat, _cameraIntrinsics );
				_cameraExtrinsicsCalibrator.extrinsics.ApplyToTransform( _calibrationBoardTransform, _mainCamera.transform, inverse: true ); // Transform board instead of camera.
			}
			_chessPatternTransform.gameObject.SetActive( found );
			return found;
		}


		void UpdateCirclePatternInProjectorImage()
		{
			// Use the circle pattern transform from last update frame, because it is more likely that it will match the detected reality.
			TrackingToolsHelper.UpdateWorldSpacePatternPoints( _circlePatternSize, _circlePatternToWorldPrevFrame, TrackingToolsHelper.PatternType.AsymmetricCircleGrid, _circlePatternBorderSizeUV, ref _circlePointsRenderedWorldMat );
			for( int p = 0; p < _circlePatternPointCount; p++ ) {
				Vector3 worldPoint = _circlePointsRenderedWorldMat.ReadVector3( p );
				Vector3 viewportPoint = _projectorCamera.WorldToViewportPoint( worldPoint );
				Vector2 imagePoint = new Vector2( viewportPoint.x * _projectorIntrinsicsCalibrator.textureWidth, ( 1 - viewportPoint.y ) * _projectorIntrinsicsCalibrator.textureHeight ); // Viewport space has zero at bottom-left, image space (opencv) has zero at top-left. So flip y.
				_circlePointsProjectorRenderImageMat.WriteVector2( imagePoint, p );
			}

			//TrackingToolsHelper.DrawFoundPattern( _camTexGrayUndistortMat, circlesPatternSize, _circlePointsProjectorRenderImageMat ); // Testing
		}


		bool DetectAndRaycastCirclePatternOntoChessboardPlane()
		{
			// Find circle pattern in camera image
			Core.bitwise_not( _camTexGrayUndistortMat, _camTexGrayUndistortInvMat ); // Invert. We need dark circles on a bright background.
			if( !TrackingToolsHelper.FindAsymmetricCirclesGrid( _camTexGrayUndistortInvMat, _circlePatternSize, ref _circlePointsCameraImageMat ) ) return false;

			// Draw deteted circles.
			TrackingToolsHelper.DrawFoundPattern( _camTexGrayUndistortMat, _circlePatternSize, _circlePointsCameraImageMat );

			// Raycast circles against chessboard plane.
			_calibrationboardPlane.SetNormalAndPosition( _calibrationBoardTransform.forward, _calibrationBoardTransform.position );
			for( int p = 0; p < _circlePatternPointCount; p++ )
			{
				// Tranform from points detected by camera to points in Unity world space.
				Vector2 cameraImagePoint = _circlePointsCameraImageMat.ReadVector2( p );
				cameraImagePoint.y = _cameraSourceTexture.height - cameraImagePoint.y; // Unity screen space has zero at bottom-left, OpenCV textures has zero at top-left.
				Ray ray = _mainCamera.ScreenPointToRay( cameraImagePoint );
				float hitDistance;
				if( !_calibrationboardPlane.Raycast( ray, out hitDistance ) ) return false;

				// For extrinsics calibration (using stereoCalibrate()).
				Vector3 worldPoint = ray.origin + ray.direction * hitDistance;
				_circlePointsDetectedWorldMat.WriteVector3( worldPoint, p );

				// For intrinsics calibration (using calibrateCamera()).
				Vector3 realModelPoint = Quaternion.Inverse( _calibrationBoardTransform.rotation ) * ( worldPoint - _calibrationBoardTransform.position );
				realModelPoint.z = 0; // Remove very small numbers. It seems CalibrateCamera() does not accept varying z values. I got an execption.
				realModelPoint *= 1000; // To millimeters
				_circlePointsRealModelMat.WriteVector3( realModelPoint, p );
			}
			return true;
		}


		void ComputeRenderedVsDetectedError()
		{
			// The "reprojection" error between the rendered world points and the detected ones. We use a simple average
			// of the diviation because it is more intuitive to interpret than Root Mean Square (RMS).
			_extrinsicsErrorAvgM = 0;
			for( int i = 0; i < _circlePatternPointCount; i++ ) {
				Vector3 renderedWorldPoint = _circlePointsRenderedWorldMat.ReadVector3( i );
				Vector3 detectedWorldPoint = _circlePointsDetectedWorldMat.ReadVector3( i );
				_extrinsicsErrorAvgM += Vector3.Distance( renderedWorldPoint, detectedWorldPoint );
			}
			_extrinsicsErrorAvgM /= _circlePatternPointCount;
		}


		void UpdateProjectorIntrinsicsAndExtrinsics()
		{
			_projectorIntrinsicsCalibrator.UpdateIntrinsics(
				samplesHaveDistortion : false,		// Light projectors (should) have no distortion.
				useTextureAspect: true				// We asume that the aspect is as advertised.
			);

			_projectorExtrinsicsCalibrator.Update( _cameraIntrinsics, _projectorIntrinsicsCalibrator.intrinsics, _projectorIntrinsicsCalibrator.textureSize );

			// Apply.
			_projectorIntrinsicsCalibrator.intrinsics.ApplyToUnityCamera( _projectorCamera );
			_projectorExtrinsicsCalibrator.extrinsics.ApplyToTransform( _projectorCamera.transform );
		}


		void SwitchState( State newState )
		{
			Debug.Log( logPrepend + "Switching state to " + newState + "\n" );

			switch( newState )
			{
				case State.TrackedCalibration:
					
					// Place circle pattern into the calibration board.
					_circlePatternTransform.SetParent( _calibrationBoardTransform );
					_circlePatternTransform.localRotation = Quaternion.identity;
					_circlePatternTransform.localPosition = -Vector3.right * ( _projectorCheckerboard.dotPatternCenterOffset / 1000f );

					// No more need for manual circle pattern manipulation.
					_circlePatternScaleSlider.gameObject.SetActive( false );
					_circlePatternOffsetXSlider.gameObject.SetActive( false );
					_circlePatternOffsetYSlider.gameObject.SetActive( false );

					// Show error values and save button.
					_intrinsicsErrorText.gameObject.SetActive( true );
					_extrinsicsErrorText.gameObject.SetActive( true );
					_saveButton.gameObject.SetActive( true );

					break;

				case State.Testing:

					// Nore more sampling.
					_projectorSampleMeterTransform.gameObject.SetActive( false );

					// No more saving.
					_saveButton.gameObject.SetActive( false );

					break;
			}

			_state = newState;
		}
		

		float ComputeAverageWorldSpaceMovement()
		{
			Matrix4x4 chessPatternToWorldCurrent = _chessPatternTransform.localToWorldMatrix;
			float averageMovement = 0;
			for( int i = 0; i < edgeCornersNormalized.Length; i++ ) {
				Vector3 cornerPoint = chessPatternToWorldCurrent.MultiplyPoint3x4( edgeCornersNormalized[ i ] );
				Vector3 cornerPointPrev = _chessPatternToWorldPrevFrame.MultiplyPoint3x4( edgeCornersNormalized[ i ] );
				averageMovement = Vector3.Distance( cornerPoint, cornerPointPrev );
			}
			return averageMovement / (float) edgeCornersNormalized.Length; 
		}



		void UpdateCirclePatternSize()
		{
			if( _state == State.Initiating || _state == State.BlindCalibration ) {
				_circlePatternSize = defaultCirclesPatternSize;

			} else {

				const int circlePatternSizeYMin = 7;
				const int desiredPixelsPerCirclePatternSegment = 25; // 50px is recommended, but for a 720p camera, this will give too fex dots.
				float desiredCirclePatternNumAspect = _projectorCheckerboard.checkerPatternSize.x / ( _projectorCheckerboard.checkerPatternSize.y ) / 2f ;// 3f / 4f / 2f;

				float patternDistance = Vector3.Distance( _mainCamera.transform.position, _circlePatternTransform.position );
				float patternHeight = _chessPatternTransform.localScale.y;
				float viewHeightAtPatternPosition = Mathf.Tan( _mainCamera.fieldOfView * Mathf.Deg2Rad * 0.5f ) * patternDistance * 2; 
				int circlePatternPixelHeight = (int) ( ( patternHeight / viewHeightAtPatternPosition ) * _cameraSourceTexture.height );
				int optimalPatternSizeY = Mathf.Max( circlePatternSizeYMin, Mathf.FloorToInt( circlePatternPixelHeight / (float) desiredPixelsPerCirclePatternSegment ) );
				int optimalPatternSizeX = Mathf.FloorToInt( optimalPatternSizeY * desiredCirclePatternNumAspect );
				_circlePatternSize = TrackingToolsHelper.GetClosestValidPatternSize( new Vector2Int( optimalPatternSizeX, optimalPatternSizeY ), TrackingToolsHelper.PatternType.AsymmetricCircleGrid );
			}

			_circlePatternPointCount = _circlePatternSize.x * _circlePatternSize.y;

			if( _circlePointsProjectorRenderImageMat != null && _circlePointsProjectorRenderImageMat.rows() == _circlePatternPointCount ) return;

			if( _circlePointsProjectorRenderImageMat != null  ) _circlePointsProjectorRenderImageMat.release();
			if( _circlePointsRealModelMat != null  ) _circlePointsRealModelMat.release();
			if( _circlePointsDetectedWorldMat != null ) _circlePointsDetectedWorldMat.release();
			_circlePointsProjectorRenderImageMat.alloc( _circlePatternPointCount );
			_circlePointsRealModelMat.alloc( _circlePatternPointCount );
			_circlePointsDetectedWorldMat.alloc( _circlePatternPointCount );

			// Render pattern to texture.
			_circlePatternBorderSizeUV = TrackingToolsHelper.RenderPattern( _circlePatternSize, TrackingToolsHelper.PatternType.AsymmetricCircleGrid, 2048, ref _circlePatternTexture, ref _patternRenderMaterial, circlePatternBorder, true );
			_circlePatternBoardMaterial.mainTexture = _circlePatternTexture;

			// Update transform to match.
			float circleTextureAspect = _circlePatternTexture.width / (float) _circlePatternTexture.height;
			float borderProportion = ( _circlePatternSize.y - 1 + 2f ) / ( _circlePatternSize.y - 1f ); // Asymmetric patttern tiles are half the height.
			_circlePatternTransform.localScale = new Vector3( circleTextureAspect, 1, 0 ) * _chessPatternTransform.localScale.y * borderProportion;

			if( _state == State.TrackedCalibration || _state == State.Testing ) _circlePatternTransform.localPosition = -Vector3.right * ( _projectorCheckerboard.dotPatternCenterOffset  / 1000f );
		}


		void UpdateSampleCounterUI()
		{
			if( _sampleCountText ){
				if( _state == State.BlindCalibration ) {
					_sb.Clear();
					_sb.Append( sampleCount );
					_sb.Append( " / " );
					_sb.Append( blindSampleCountTarget );
					_sampleCountText.text = _sb.ToString();
				} else {
					_sampleCountText.text = sampleCount.ToString();
				}
			}
		}


		void UpdateSampleMeterProjectorUI()
		{
			float t = _stableFrameCount / (float) _stableSampleCountThreshold;
			float fullWidth = _chessPatternTransform.localScale.x;
			_projectorSampleMeterTransform.localPosition = new Vector3( - (1-t) * fullWidth * 0.5f, _projectorSampleMeterTransform.localPosition.y, 0 );
			_projectorSampleMeterTransform.localScale = new Vector3( t * fullWidth, TrackingToolsConstants.precisionTestDotSize, 0 );
		}


		void UpdateManualCircleBoardTransformation()
		{
			float xOffT = _circlePatternOffsetXSlider.value;
			float yOffT = _circlePatternOffsetYSlider.value;
			float scaleT = _circlePatternScaleSlider.value;

			const float relScaleYMax = 1;
			const float relScaleYMin = 0.5f;
			float relScaleY = Mathf.Lerp( relScaleYMin, relScaleYMax, scaleT );

			float viewHeight = _circlePatternTransform.localScale.y / relScaleY;
			float dist = ( viewHeight * 0.5f ) / Mathf.Tan( _projectorCamera.fieldOfView * 0.5f * Mathf.Deg2Rad );
			float xOff = ( xOffT - 0.5f ) * ( viewHeight * _projectorCamera.aspect - _circlePatternTransform.localScale.x );
			float yOff = ( yOffT - 0.5f ) * ( viewHeight - _circlePatternTransform.localScale.y );

			_circlePatternTransform.position = 
				_projectorCamera.transform.position +
				_projectorCamera.transform.rotation * new Vector3( xOff, yOff, dist );
			
			_manualCirclePatternTransformationRequested = false;
		}


		void UndoSample()
		{
			if( sampleCount <= blindSampleCountTarget ) return;
			_projectorExtrinsicsCalibrator.RemovePreviousSample();
			UpdateProjectorIntrinsicsAndExtrinsics();
		}


		void SaveToFiles()
		{
			string intrinsicsFileName = _projectorIntrinsicsFileName;
			if( _addErrorValueToIntrinsicsFileName ) intrinsicsFileName += "_E-" + _projectorIntrinsicsCalibrator.rmsError.ToString( "F02" ).Replace( ".", "," );
			string intrinsicsFilePath = _projectorIntrinsicsCalibrator.intrinsics.SaveToFile( intrinsicsFileName );
			string extrinsicsFilePath = _projectorExtrinsicsCalibrator.extrinsics.SaveToFile( _projectorExtrinsicsFileName );

			Debug.Log( logPrepend + "Saves files\n" + intrinsicsFilePath + "\n" + extrinsicsFilePath );

			SwitchState( State.Testing );
		}



		void OnDrawGizmos()
		{
			if( _showDotGizmos && _processedCameraTexture != null && _circlePointsRenderedWorldMat != null )
			{
				float radius = ( _circlePatternTransform.localScale.y / _circlePatternSize.y ) * 0.1f;
				Vector3 prevCirclePointRenderWorld = Vector3.zero;
				Vector3 prevCirclePointWorld = Vector3.zero;
				for( int p = 0; p < _circlePatternPointCount; p++ )
				{
					Vector3 circlePointRenderWorld = _circlePointsRenderedWorldMat.ReadVector3( p );
					Vector3 circlePointsWorld = _circlePointsDetectedWorldMat.ReadVector3( p );

					if( p == 0 ) Gizmos.color = Color.red;
					else if( p == 1 ) Gizmos.color = Color.yellow;
					else Gizmos.color = Color.blue;
					Gizmos.DrawSphere( circlePointRenderWorld, radius );
					if( p != 0 ) Gizmos.DrawLine( prevCirclePointRenderWorld, circlePointRenderWorld );

					Gizmos.color = Color.yellow;
					Gizmos.DrawSphere( circlePointsWorld, radius );
					if( p != 0 ) Gizmos.DrawLine( prevCirclePointWorld, circlePointsWorld );

					prevCirclePointRenderWorld = circlePointRenderWorld;
					prevCirclePointWorld = circlePointsWorld;
				}
			}
		}
	}
}