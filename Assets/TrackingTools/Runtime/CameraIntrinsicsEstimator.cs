/*
	Copyright © Carl Emil Carlsen 2020
	http://cec.dk

	Guiding notes about calibration accurency.
	https://stackoverflow.com/questions/12794876/how-to-verify-the-correctness-of-calibration-of-a-webcam
*/

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Experimental.Rendering;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.Calib3dModule;

namespace TrackingTools
{
	public class CameraIntrinsicsEstimator : MonoBehaviour
	{
		[Header("Input")]
		[SerializeField] Texture _cameraTexture;
		[SerializeField,Tooltip("Number of inner corners")] Vector2Int _chessPatternSize = new Vector2Int( 7, 5 );
		[SerializeField,Tooltip("Millimeters")] int _chessTileSize = 29; // Real space (milimeters).
		[SerializeField] int _desiredSampleCount = 4; // Four perfect samples should be ideal https://medium.com/@hey_duda/the-magic-behind-camera-calibration-8596b7ddcd71
		[SerializeField] bool _flipCameraTexture = false;

		[Header("Output")]
		[SerializeField] string _intrinsicsFileName = "DefaultCamera_1280x720";
		[SerializeField] bool _addErrorValueToFileName = false;

		[ Header("UI")]
		[SerializeField] Camera _mainCamera = null;
		[SerializeField] RawImage _processedCameraImage = null;
		[SerializeField] Image _sampleCountMeterFillImage = null;
		[SerializeField] Text _sampleCountText = null;
		[SerializeField] Text _rmsErrorText = null;

		State _state = State.Initiating;

		IntrinsicsCalibrator _intrinsicsCalibrator;
		CameraExtrinsicsCalibrator _extrinsicsCalibrator;

		Mat _camTexMat;
		Mat _camTexGrayMat;
		Mat _camTexGrayUndistortMat;
		Texture2D _processedCameraTexture;
		Texture2D _tempTransferTexture; // For conversion from RenderTexture input.
		Color32[] _tempTransferColors;
		MatOfPoint3f _chessCornersRealModelMat;
		MatOfPoint2f _chessCornersImageMat;
		MatOfPoint3f _chessCornersWorldMat;
		Vector2[] _prevChessCorners;
		int _successFrameCount;
		int _stableFrameCount;

		RenderTexture _chessPatternTexture;
		Material _patternRenderMaterial;

		MaterialPropFlasher _previewFlasher;

		Material _previewMaterial;
		AspectRatioFitter _aspectFitter;
		RenderTexture _arTexture;
		RawImage _arImage;
		Transform _chessPatternTransform;

		bool _dirtyCameraTexture;

		int _chessPatternPointCount;

		const float lowMovementThreshold = 0.003f;
		const int stableFrameCountThreshold = 50;
		const int correctDistortionSampleCountThreshold = 3;		

		static readonly string logPrepend = "<b>[" + nameof( CameraIntrinsicsEstimator ) + "]</b> ";


		public Texture cameraTexture {
			get { return _cameraTexture; }
			set {
				_cameraTexture = value;
				_dirtyCameraTexture = true;
			}
		}

		enum State
		{
			 Initiating,
			 Calibrating,
			 Testing
		}


		void Awake()
		{
			Application.targetFrameRate = 30;

			_chessPatternPointCount = _chessPatternSize.x * _chessPatternSize.y;

			// Prepare OpenCV.
			_extrinsicsCalibrator = new CameraExtrinsicsCalibrator();
			_prevChessCorners = new Vector2[ _chessPatternPointCount ];
			_chessCornersRealModelMat = TrackingToolsHelper.CreateRealModelPatternPoints( _chessPatternSize, _chessTileSize, TrackingToolsHelper.PatternType.Chessboard );

			// Create objects.
			_chessPatternTransform = GameObject.CreatePrimitive( PrimitiveType.Quad ).transform;
			_chessPatternTransform.name = "Chessboard";
			_chessPatternTransform.localScale = new Vector3( (_chessPatternSize.x-1) * _chessTileSize * 0.001f, (_chessPatternSize.y-1) * _chessTileSize * 0.001f, 0 );

			// Prepare world points.
			TrackingToolsHelper.UpdateWorldSpacePatternPoints( _chessPatternSize, _chessPatternTransform.localToWorldMatrix, TrackingToolsHelper.PatternType.Chessboard, Vector2.zero, ref _chessCornersWorldMat );

			// Prepare UI.
			TrackingToolsHelper.RenderPattern( _chessPatternSize, TrackingToolsHelper.PatternType.Chessboard, 1024, ref _chessPatternTexture, ref _patternRenderMaterial );
			_aspectFitter = _processedCameraImage.GetComponent<AspectRatioFitter>();
			if( !_aspectFitter ) _aspectFitter = _processedCameraImage.gameObject.AddComponent<AspectRatioFitter>();
			_aspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
			Shader shader = Shader.Find( TrackingToolsConstants.previewShaderName );
			_previewMaterial = new Material( shader );
			_processedCameraImage.material = _previewMaterial;
			_processedCameraImage.color = Color.white;
			_arImage = new GameObject( "ARImage" ).AddComponent<RawImage>();
			_arImage.transform.SetParent( _processedCameraImage.transform );
			_arImage.rectTransform.FitParent();
			_arImage.gameObject.SetActive( false );
			Shader unlitTextureShader = Shader.Find( "Unlit/Texture" );
			Material chessboardMaterial = new Material( unlitTextureShader );
			chessboardMaterial.mainTexture = _chessPatternTexture;
			_chessPatternTransform.GetComponent<Renderer>().material = chessboardMaterial;
			if( _sampleCountMeterFillImage ) _sampleCountMeterFillImage.fillAmount = 0;
			_previewFlasher = new MaterialPropFlasher( _previewMaterial, "_Whiteout", TrackingToolsConstants.flashDuration );

			// Setup camera.
			_mainCamera.backgroundColor = Color.clear;
			_mainCamera.gameObject.SetActive( false );
		}


		void OnDestroy()
		{
			if( _camTexMat != null ) _camTexMat.Dispose();
			if( _tempTransferTexture ) Destroy( _tempTransferTexture );
			if( _chessCornersImageMat != null ) _chessCornersImageMat.Dispose();
			if( _previewMaterial ) Destroy( _previewMaterial );
			if( _arTexture ) _arTexture.Release();
			if( _chessPatternTexture ) _chessPatternTexture.Release();
			if( _patternRenderMaterial ) Destroy( _patternRenderMaterial );
			if( _chessCornersWorldMat != null ) _chessCornersWorldMat.Dispose();

			Reset();
		}


		void Update()
		{
			if( !_cameraTexture || !_dirtyCameraTexture ) return;

			// Init.
			AdaptResources();

			// Update mat texture ( If the texture looks correct in Unity, then it needs to be flipped for OpenCV ).
			TrackingToolsHelper.TextureToMat( _cameraTexture, !_flipCameraTexture, ref _camTexMat, ref _tempTransferColors, ref _tempTransferTexture );

			// Convert to grayscale if more than one channel, else copy (and convert bit rate if necessary).
			TrackingToolsHelper.ColorMatToLumanceMat( _camTexMat, _camTexGrayMat );

			// During testing, undistort before.
			if( _state == State.Testing ) {
				Calib3d.undistort( _camTexGrayMat, _camTexGrayUndistortMat, _intrinsicsCalibrator.sensorMat, _intrinsicsCalibrator.distortionCoeffsMat );
			}

			// Find chessboard.
			Mat chessboardSourceMat = _state == State.Calibrating ? _camTexGrayMat : _camTexGrayUndistortMat;
			bool foundBoard = TrackingToolsHelper.FindChessboardCorners( chessboardSourceMat, _chessPatternSize, ref _chessCornersImageMat );
			if( foundBoard ) TrackingToolsHelper.DrawFoundPattern( chessboardSourceMat, _chessPatternSize, _chessCornersImageMat );

			// During calibration, undistort after.
			if( _state == State.Calibrating ) {
				if( _intrinsicsCalibrator.sampleCount > correctDistortionSampleCountThreshold ) {
					Calib3d.undistort( _camTexGrayMat, _camTexGrayUndistortMat, _intrinsicsCalibrator.sensorMat, _intrinsicsCalibrator.distortionCoeffsMat );
				} else {
					_camTexGrayMat.copyTo( _camTexGrayUndistortMat );
				}
			}

			// State dependent updates.
			switch( _state )
			{
				case State.Calibrating: UpdateCalibration( foundBoard ); break;
				case State.Testing: UpdateTesting( foundBoard ); break;
			}

			// UI.
			Utils.fastMatToTexture2D( _camTexGrayUndistortMat, _processedCameraTexture ); // Will flip as default.
			_previewFlasher.Update();
			UpdateSampleCounterUI();

			_dirtyCameraTexture = false;
		}
	

		void UpdateCalibration( bool foundBoard )
		{
			if( foundBoard ) {
				_successFrameCount++;
				float averageMovement = ComputeAverageMovement();
				if( averageMovement < lowMovementThreshold ) _stableFrameCount++;
				else _stableFrameCount = 0;
			} else {
				_successFrameCount = 0;
				_stableFrameCount = 0;
			}

			// When consistently stable, gather sample.
			if( _stableFrameCount == stableFrameCountThreshold )
			{
				_intrinsicsCalibrator.AddSample( _chessCornersRealModelMat, _chessCornersImageMat );
				_intrinsicsCalibrator.UpdateIntrinsics();
				_previewFlasher.Start();
				_rmsErrorText.text = _intrinsicsCalibrator.rmsError.ToString( "F3" );
				_stableFrameCount = 0;

				// When enough samples are gathered, save to file and switch to testing mode.
				if( _intrinsicsCalibrator.sampleCount == _desiredSampleCount )
				{
					string intrinsicsFileName = _intrinsicsFileName;
					if( _addErrorValueToFileName ) intrinsicsFileName += "_E-" + _intrinsicsCalibrator.rmsError.ToString( "F02" ).Replace( ".", "," );
					string filePath = _intrinsicsCalibrator.intrinsics.SaveToFile( intrinsicsFileName );
					SwitchState( State.Testing );

					Debug.Log( logPrepend + "Saved intrinsics to file.\n" + filePath );
				}
			}
		}


		void UpdateTesting( bool foundBoard )
		{
			_arImage.enabled = foundBoard;
			if( !foundBoard ) return;

			bool success =_extrinsicsCalibrator.UpdateExtrinsics( _chessCornersWorldMat, _chessCornersImageMat, _intrinsicsCalibrator.intrinsics, _intrinsicsCalibrator.textureWidth, _intrinsicsCalibrator.textureHeight );
			if( success ) {
				_extrinsicsCalibrator.extrinsics.ApplyToTransform( _mainCamera.transform );
			}
			_arImage.enabled = success;
		}


		void SwitchState( State newState )
		{
			switch( newState )
			{
				case State.Calibrating:
					break;

				case State.Testing:

					// UI
					_sampleCountMeterFillImage.transform.parent.gameObject.SetActive( false );
					_sampleCountText.gameObject.SetActive( false );
					_intrinsicsCalibrator.intrinsics.ApplyToCamera( _mainCamera );
					_mainCamera.gameObject.SetActive( true );
					_arImage.gameObject.SetActive( true );
					break;

			}
			_state = newState;
		}


		void AdaptResources()
		{
			int w = _cameraTexture.width;
			int h = _cameraTexture.height;
			if( _processedCameraTexture != null && _processedCameraTexture.width == w && _processedCameraTexture.height == h ) return;

			// Start over again.
			Reset();

			_intrinsicsCalibrator = new IntrinsicsCalibrator( w, h );

			// Create mats and textures.
			_camTexGrayMat = new Mat( h, w, CvType.CV_8UC1 );
			_camTexGrayUndistortMat = new Mat( h, w, CvType.CV_8UC1 );
			_processedCameraTexture = new Texture2D( w, h, GraphicsFormat.R8_UNorm, 0, TextureCreationFlags.None );
			_processedCameraTexture.name = "UndistortedCameraTex";
			_processedCameraTexture.wrapMode = TextureWrapMode.Repeat;
			_arTexture = new RenderTexture( w, h, 16, GraphicsFormat.R8G8B8A8_UNorm );
			_arTexture.name = "AR Texture";

			// Change state.
			if( _state == State.Initiating ) SwitchState( State.Calibrating );

			// Update UI.
			_aspectFitter.aspectRatio = w / (float) h;
			_processedCameraImage.texture = _processedCameraTexture;
			_arImage.texture = _arTexture;
			_mainCamera.targetTexture = _arTexture;
		}


		float ComputeAverageMovement()
		{
			float averageMovement = 0;
			for( int n = 0; n < _chessPatternPointCount; n++ ) {
				Vector2 chessCorner = _chessCornersImageMat.ReadVector2( n );
				if( _successFrameCount > 0 ) averageMovement += ( _prevChessCorners[ n ] - chessCorner ).magnitude;
				_prevChessCorners[ n ] = chessCorner;
			}
			if( _successFrameCount == 0 ) return 0;
			return averageMovement / (float) _prevChessCorners.Length / (float) _cameraTexture.height;
		}


		void UpdateSampleCounterUI()
		{
			if( _sampleCountMeterFillImage ) _sampleCountMeterFillImage.fillAmount = _stableFrameCount / (float) stableFrameCountThreshold;
			if( _sampleCountText ) _sampleCountText.text = _intrinsicsCalibrator.sampleCount + " / " + _desiredSampleCount;
		}


		void Reset()
		{
			if( _camTexGrayMat != null ) _camTexGrayMat.Dispose();
			if( _camTexGrayUndistortMat != null ) _camTexGrayUndistortMat.Dispose();
			if( _processedCameraTexture ) Destroy( _processedCameraTexture );
			if( _intrinsicsCalibrator != null ) _intrinsicsCalibrator.Clear();

			_stableFrameCount = 0;
			_successFrameCount = 0;
		}
	}
}