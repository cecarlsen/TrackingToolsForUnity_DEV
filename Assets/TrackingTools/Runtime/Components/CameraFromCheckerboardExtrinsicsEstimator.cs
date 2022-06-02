/*
	Copyright © Carl Emil Carlsen 2020-2022
	http://cec.dk
*/

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Experimental.Rendering;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.Calib3dModule;
using OpenCVForUnity.ImgprocModule;


namespace TrackingTools
{
	public class CameraFromCheckerboardExtrinsicsEstimator : MonoBehaviour
	{
		[SerializeField] Camera _targetCamera = null;
		[SerializeField,Tooltip("Optional")] Transform _targetCheckerboardTransform;

		[Header("Input")]
		[SerializeField] Texture _cameraSourceTexture;
		[SerializeField] bool _flipSourceTextureVertically = false;
		[SerializeField] string _intrinsicsFileName = "DefaultCamera";
		[SerializeField] Checkerboard _calibrationBoard = null;
		[SerializeField,Tooltip("Only use when you cannot control lighting conditions.")] bool _normalizeSourceTexture = false;

		[Header("Options")]
		[SerializeField,Tooltip("Transform the calibration board instead of the camera.")] bool _tranformCalibrationBoard = false;
		[SerializeField] bool _fastAndImprecise = false;

		[Header("Output")]
		[SerializeField,Tooltip("Name used when SaveToFile is called and no file name is provided.")] string _defaultExtrinsicsFileName = "DefaultCameraFromChessboard";

		[Header("UI")]
		[SerializeField] RawImage _processedCameraImage = null;

		[Header("Projector Test Dots")]
		[SerializeField] bool _testPrecisionDotsEnabled = false;
		[SerializeField,Layer] int _testPrecisionDotsLayer = 0;

		[Header("Debug")]
		[SerializeField] bool _showInputWorldPointsGizmos = false;
		[SerializeField] bool _showFoundPointsInImage = false;


		Intrinsics _intrinsics;
		ExtrinsicsCalibrator _extrinsicsCalibrator;

		Mat _sensorMat;
		MatOfDouble _distortionCoeffsMat;

		Mat _camTexMat;
		Mat _camTexGrayMat;
		Mat _camTexGrayUndistortMat;
		Texture2D _processedCameraTexture;
		Texture2D _tempTransferTexture; // For conversion from RenderTexture input.
		Color32[] _tempTransferColors;

		Mat _undistortMap1;
		Mat _undistortMap2;

		MatOfPoint2f _chessCornersImageMat;
		MatOfPoint3f _chessCornersWorldMat;

		Material _previewMaterial;
		Material _patternRenderMaterial;
		RenderTexture _chessPatternTexture;
		RenderTexture _arTexture;

		AspectRatioFitter _aspectFitter;
		RawImage _arImage;
		GameObject _precisionDotsContainerObject;

		bool _dirtyCameraTexture;
		bool _initiated;

		static readonly string logPrepend = "<b>[" + nameof( CameraFromCheckerboardExtrinsicsEstimator ) + "]</b> ";


		public Texture cameraSourceTexture {
			get { return _cameraSourceTexture; }
			set {
				_cameraSourceTexture = value;
				_dirtyCameraTexture = true;
			}
		}


		void Awake()
		{
			if( !Intrinsics.TryLoadFromFile( _intrinsicsFileName, out _intrinsics ) ) {
				Debug.LogError( logPrepend + "Intrinsics file '" + _intrinsicsFileName + "' not found.\n" );
				enabled = false;
				return;
			}

			if( !_calibrationBoard ){
				Debug.LogError( logPrepend + "Missing calibration bord reference\n" );
				enabled = false;
				return;
			}

			_undistortMap1 = new Mat();
			_undistortMap2 = new Mat();

			_extrinsicsCalibrator = new ExtrinsicsCalibrator();

			TrackingToolsHelper.RenderPattern( _calibrationBoard.checkerPatternSize, TrackingToolsHelper.PatternType.Checkerboard, 1024, ref _chessPatternTexture, ref _patternRenderMaterial );

			// Prepare UI.
			_aspectFitter = _processedCameraImage.GetComponent<AspectRatioFitter>();
			if( !_aspectFitter ) _aspectFitter = _processedCameraImage.gameObject.AddComponent<AspectRatioFitter>();
			_aspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
			_previewMaterial = new Material( Shader.Find( TrackingToolsConstants.previewShaderName ) );
			_processedCameraImage.material = _previewMaterial;
			_processedCameraImage.color = Color.white;
			_arImage = new GameObject( "ARImage" ).AddComponent<RawImage>();
			_arImage.transform.SetParent( _processedCameraImage.transform );
			_arImage.rectTransform.FitParent();
			_arImage.gameObject.SetActive( false );
			_targetCamera.backgroundColor = Color.clear;
			
			if( !_targetCheckerboardTransform ) _targetCheckerboardTransform = new GameObject( "CalibrationBoard" ).transform;
			_targetCheckerboardTransform.localScale = new Vector3( ( _calibrationBoard.checkerPatternSize.x - 1 ) * _calibrationBoard.checkerTileSize * 0.001f, ( _calibrationBoard.checkerPatternSize.y - 1 ) * _calibrationBoard.checkerTileSize * 0.001f, 0 );
			MeshFilter meshFilter;
			if( !( meshFilter = _targetCheckerboardTransform.GetComponent<MeshFilter>() ) ) meshFilter = _targetCheckerboardTransform.gameObject.AddComponent<MeshFilter>();
			meshFilter.sharedMesh = PrimitiveFactory.Quad();
			MeshRenderer meshRenderer;
			if( !( meshRenderer = _targetCheckerboardTransform.GetComponent<MeshRenderer>() ) ) meshRenderer = _targetCheckerboardTransform.gameObject.AddComponent<MeshRenderer>();
			Shader unlitTextureShader = Shader.Find( "Hidden/UnlitTexture" );
			Material calibrationBoardMaterial = new Material( unlitTextureShader );
			calibrationBoardMaterial.mainTexture = _chessPatternTexture;
			meshRenderer.sharedMaterial = calibrationBoardMaterial;

			_precisionDotsContainerObject = TrackingToolsHelper.CreatePrecisionTestDots( _targetCheckerboardTransform, _testPrecisionDotsLayer, _calibrationBoard.checkerPatternSize, _calibrationBoard.checkerTileSize / 1000f );

			// Update world points.
			TrackingToolsHelper.UpdateWorldSpacePatternPoints( _calibrationBoard.checkerPatternSize, _targetCheckerboardTransform.localToWorldMatrix, TrackingToolsHelper.PatternType.Checkerboard, Vector2.zero, ref _chessCornersWorldMat );
		}


		void OnDestroy()
		{
			if( _camTexMat != null ) _camTexMat.release();
			if( _tempTransferTexture ) Destroy( _tempTransferTexture );
			if( _chessCornersImageMat != null ) _chessCornersImageMat.release();
			if( _previewMaterial ) Destroy( _previewMaterial );
			if( _arTexture ) _arTexture.Release();
			if( _chessPatternTexture ) _chessPatternTexture.Release();
			if( _patternRenderMaterial ) Destroy( _patternRenderMaterial );
			if( _extrinsicsCalibrator != null ) _extrinsicsCalibrator.Release();
			if( _undistortMap1 != null ) _undistortMap1.release();
			if( _undistortMap2 != null ) _undistortMap2.release();

			Reset();
		}
		

		void Update()
		{
			if( !_cameraSourceTexture || !_dirtyCameraTexture ) return;

			if( !AdaptResources() ) return;

			// Update mat texture ( If the texture looks correct in Unity, then it needs to be flipped for OpenCV ).
			TrackingToolsHelper.TextureToMat( _cameraSourceTexture, !_flipSourceTextureVertically, ref _camTexMat, ref _tempTransferColors, ref _tempTransferTexture );

			// Convert to grayscale if more than one channel, else copy (and convert bit rate if necessary).
			TrackingToolsHelper.ColorMatToLumanceMat( _camTexMat, _camTexGrayMat );

			// Sometimes normalization makes it easier for FindChessboardCorners.
			if( _normalizeSourceTexture ) Core.normalize( _camTexGrayMat, _camTexGrayMat, 0, 255, Core.NORM_MINMAX, CvType.CV_8U );
			
			// Undistort (TODO: move undistortion to GPU as last step and work on distorted image instead).
			//Calib3d.undistort( _camTexGrayMat, _camTexGrayUndistortMat, _sensorMat, _distortionCoeffsMat );
			Imgproc.remap( _camTexGrayMat, _camTexGrayUndistortMat, _undistortMap1, _undistortMap2, Imgproc.INTER_LINEAR );

			// Find chessboard.
			bool foundBoard = TrackingToolsHelper.FindChessboardCorners( _camTexGrayUndistortMat, _calibrationBoard.checkerPatternSize, ref _chessCornersImageMat, _fastAndImprecise );
			
			if( foundBoard )
			{
				// Draw chessboard.
				if( _showFoundPointsInImage ) TrackingToolsHelper.DrawFoundPattern( _camTexGrayUndistortMat, _calibrationBoard.checkerPatternSize, _chessCornersImageMat );

				// Update and apply extrinsics.
				bool foundExtrinsics = _extrinsicsCalibrator.UpdateExtrinsics( _chessCornersWorldMat, _chessCornersImageMat, _intrinsics ); // TODO: Problem could be here
				if( foundExtrinsics ){

					if( _tranformCalibrationBoard ){
						_extrinsicsCalibrator.extrinsics.ApplyToTransform( _targetCheckerboardTransform, _targetCamera.transform, inverse: _tranformCalibrationBoard ); // TODO: Problem could be here
					} else {
						_extrinsicsCalibrator.extrinsics.ApplyToTransform( _targetCamera.transform, _targetCheckerboardTransform );
					}
				}
				_arImage.gameObject.SetActive( foundExtrinsics );
			} else {
				_arImage.gameObject.SetActive( false );
			}

			// UI.
			Utils.fastMatToTexture2D( _camTexGrayUndistortMat, _processedCameraTexture ); // Will flip back to Unity orientation by default.
			if( _precisionDotsContainerObject.activeSelf != _testPrecisionDotsEnabled ) _precisionDotsContainerObject.SetActive( _testPrecisionDotsEnabled );


			_dirtyCameraTexture = false;
		}
		

		public void SaveToFile( string optionalFileName = null )
		{
			// If we are in fast and imprecise mode, then detect and update again with higher precision before saving.
			if( _fastAndImprecise ) {
				TrackingToolsHelper.FindChessboardCorners( _camTexGrayUndistortMat, _calibrationBoard.checkerPatternSize, ref _chessCornersImageMat );
				_extrinsicsCalibrator.UpdateExtrinsics( _chessCornersWorldMat, _chessCornersImageMat, _intrinsics );
			}

			if( !_extrinsicsCalibrator.isValid ){
				Debug.LogWarning( "Save extrinsics to file failed. No chessboard was found in camera image.\n" );
				return;
			}

			if( !string.IsNullOrEmpty( optionalFileName ) ) _defaultExtrinsicsFileName = optionalFileName;

			_extrinsicsCalibrator.extrinsics.SaveToFile( _defaultExtrinsicsFileName );

			Debug.Log( logPrepend + "Saved extrinsics to file.\n" + _defaultExtrinsicsFileName );
		}


		bool AdaptResources()
		{
			int w = _cameraSourceTexture.width;
			int h = _cameraSourceTexture.height;
			if( _processedCameraTexture != null && _processedCameraTexture.width == w && _processedCameraTexture.height == h ) return true; // Already adapted.

			// Get and apply intrinsics.
			bool success = _intrinsics.ApplyToToOpenCV( ref _sensorMat, ref _distortionCoeffsMat );//, w, h );
			if( !success ) return false;

			_intrinsics.ApplyToUnityCamera( _targetCamera );

			// Create mats and textures.
			_camTexGrayMat = new Mat( h, w, CvType.CV_8UC1 );
			_camTexGrayUndistortMat = new Mat( h, w, CvType.CV_8UC1 );
			_processedCameraTexture = new Texture2D( w, h, GraphicsFormat.R8_UNorm, 0, TextureCreationFlags.None );
			_processedCameraTexture.name = "UndistortedCameraTex";
			_processedCameraTexture.wrapMode = TextureWrapMode.Repeat;
			_arTexture = new RenderTexture( w, h, 16, GraphicsFormat.R8G8B8A8_UNorm );
			_arTexture.name = "AR Texture";

			// Create undistort map (sensorMat remains unchanged even through it is passed as newCameraMatrix).
			Calib3d.initUndistortRectifyMap( _sensorMat, _distortionCoeffsMat, new Mat(), _sensorMat, new Size( _cameraSourceTexture.width, _cameraSourceTexture.height ), CvType.CV_32FC1, _undistortMap1, _undistortMap2 );

			// Update UI.
			_aspectFitter.aspectRatio = w / (float) h;
			_processedCameraImage.texture = _processedCameraTexture;
			_arImage.texture = _arTexture;
			_targetCamera.targetTexture = _arTexture;

			// Log.
			Debug.Log( logPrepend + "Running at " + w  + "x" + h + "\n" );

			return true;
		}


		void Reset()
		{
			if( _sensorMat != null ) _sensorMat.Dispose();
			if( _distortionCoeffsMat != null ) _distortionCoeffsMat.Dispose();
			if( _camTexGrayMat != null ) _camTexGrayMat.Dispose();
			if( _camTexGrayUndistortMat != null ) _camTexGrayUndistortMat.Dispose();
			if( _processedCameraTexture ) Destroy( _processedCameraTexture );
		}


		void OnDrawGizmos()
		{
			if( _chessCornersWorldMat == null ) return;

			if( _showInputWorldPointsGizmos )
			{
				float pointRadius = ( _targetCheckerboardTransform.localScale.y / (float) _calibrationBoard.checkerPatternSize.y ) * 0.2f;
				Gizmos.color = Color.red;
				for( int i = 0; i < _chessCornersWorldMat.total(); i++ ) {
					Vector3 p = _chessCornersWorldMat.ReadVector3( i );
					Gizmos.DrawSphere( p, pointRadius );
					#if UNITY_EDITOR
						UnityEditor.Handles.Label( p, i.ToString() );
					#endif
				}
			}
		}
	}
}