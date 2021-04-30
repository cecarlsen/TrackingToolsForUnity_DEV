/*
	Copyright © Carl Emil Carlsen 2020
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
	public class CameraFromChessboardExtrinsicsEstimator : MonoBehaviour
	{
		[SerializeField] Texture _cameraTexture;
		[SerializeField] bool _flipCameraTexture = false;
		[SerializeField, Tooltip( "Number of inner corners" )] Vector2Int _chessPatternSize = new Vector2Int( 7, 10 );
		[SerializeField, Tooltip( "Millimeters" )] int _chessTileSize = 15; // Real space (milimeters).
		[SerializeField] string _intrinsicsFileName = "DefaultCamera";
		[SerializeField] RawImage _processedCameraImage = null;
		[SerializeField] bool _tranformPattern = false;
		[SerializeField] bool _testPrecisionDotsEnabled = false;
		[SerializeField,Layer] int _testPrecisionDotsLayer = 0;
		[SerializeField] bool _applyRelative = true;
		[SerializeField] bool _fastAndImprecise = false;


		[SerializeField] string _extrinsicsFileName = "DefaultCameraFromChessboard";

		[SerializeField] Camera _mainCamera = null;

		Intrinsics _intrinsics;
		CameraExtrinsicsCalibrator _extrinsicsCalibrator;

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
		Transform _chessPatternTransform;
		GameObject _precisionDotsContainerObject;

		bool _dirtyCameraTexture;
		bool _initiated;

		static readonly string logPrepend = "<b>[" + nameof( CameraFromChessboardExtrinsicsEstimator ) + "]</b> ";


		public Texture cameraTexture {
			get { return _cameraTexture; }
			set {
				_cameraTexture = value;
				_dirtyCameraTexture = true;
			}
		}


		void Awake()
		{
			if( !Intrinsics.TryLoadFromFile( _intrinsicsFileName, out _intrinsics ) ) {
				Debug.LogError( "Intrinsics file '" + _intrinsicsFileName + "' not found.\n" );
				enabled = false;
				return;
			}

			_undistortMap1 = new Mat();
			_undistortMap2 = new Mat();

			_extrinsicsCalibrator = new CameraExtrinsicsCalibrator();

			TrackingToolsHelper.RenderPattern( _chessPatternSize, TrackingToolsHelper.PatternType.Chessboard, 1024, ref _chessPatternTexture, ref _patternRenderMaterial );

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
			_mainCamera.backgroundColor = Color.clear;
			_chessPatternTransform = GameObject.CreatePrimitive( PrimitiveType.Quad ).transform;
			_chessPatternTransform.name = "Chessboard";
			_chessPatternTransform.localScale = new Vector3( ( _chessPatternSize.x - 1 ) * _chessTileSize * 0.001f, ( _chessPatternSize.y - 1 ) * _chessTileSize * 0.001f, 0 );
			Shader unlitTextureShader = Shader.Find( "Unlit/Texture" );
			Material chessboardMaterial = new Material( unlitTextureShader );
			chessboardMaterial.mainTexture = _chessPatternTexture;
			_chessPatternTransform.GetComponent<Renderer>().material = chessboardMaterial;
			_precisionDotsContainerObject = TrackingToolsHelper.CreatePrecisionTestDots( _chessPatternTransform, _testPrecisionDotsLayer, _chessPatternSize, _chessTileSize / 1000f );

			// Update world points.
			TrackingToolsHelper.UpdateWorldSpacePatternPoints( _chessPatternSize, _chessPatternTransform.localToWorldMatrix, TrackingToolsHelper.PatternType.Chessboard, Vector2.zero, ref _chessCornersWorldMat );
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
			if( !_cameraTexture || !_dirtyCameraTexture ) return;

			if( !AdaptResources() ) return;

			// Update mat texture (If the textyre looks right in Unity, it needs to be flipped for OpenCV.)
			TrackingToolsHelper.TextureToMat( _cameraTexture, !_flipCameraTexture, ref _camTexMat, ref _tempTransferColors, ref _tempTransferTexture );

			// Convert to grayscale if more than one channel, else copy (and convert bit rate if necessary).
			TrackingToolsHelper.ColorMatToLumanceMat( _camTexMat, _camTexGrayMat );

			// Undistort (TODO: move undistortion to GPU as last step and work on distorted image instead).
			//Calib3d.undistort( _camTexGrayMat, _camTexGrayUndistortMat, _sensorMat, _distortionCoeffsMat );
			Imgproc.remap( _camTexGrayMat, _camTexGrayUndistortMat, _undistortMap1, _undistortMap2, Imgproc.INTER_LINEAR );

			// Find chessboard.
			bool foundBoard = TrackingToolsHelper.FindChessboardCorners( _camTexGrayUndistortMat, _chessPatternSize, ref _chessCornersImageMat, _fastAndImprecise );
			
			if( foundBoard )
			{
				// Draw chessboard.
				TrackingToolsHelper.DrawFoundPattern( _camTexGrayUndistortMat, _chessPatternSize, _chessCornersImageMat );

				// Update and apply extrinsics.
				bool foundExtrinsics = _extrinsicsCalibrator.UpdateExtrinsics( _chessCornersWorldMat, _chessCornersImageMat, _intrinsics, _cameraTexture.width, _cameraTexture.height );
				if( foundExtrinsics ){

					if( _tranformPattern ){
						_extrinsicsCalibrator.extrinsics.ApplyToTransform( _chessPatternTransform, _applyRelative ? _mainCamera.transform : null, _tranformPattern );
						if( !_applyRelative ) _mainCamera.transform.SetPositionAndRotation( Vector3.zero, Quaternion.identity );
					} else {
						_extrinsicsCalibrator.extrinsics.ApplyToTransform( _mainCamera.transform, _applyRelative ? _chessPatternTransform : null );
						if( !_applyRelative ) _chessPatternTransform.SetPositionAndRotation( Vector3.zero, Quaternion.identity );
					}
				}
				_arImage.gameObject.SetActive( foundExtrinsics );
			} else {
				_arImage.gameObject.SetActive( false );
			}

			// UI.
			Utils.fastMatToTexture2D( _camTexGrayUndistortMat, _processedCameraTexture ); // Flips vertically by default
			if( _precisionDotsContainerObject.activeSelf != _testPrecisionDotsEnabled ) _precisionDotsContainerObject.SetActive( _testPrecisionDotsEnabled );


			_dirtyCameraTexture = false;
		}


		public void SaveToFile( string optionalFileName = null )
		{
			// If we are in fast and imprecise mode, then detect and update again with higher precision before saving.
			if( _fastAndImprecise ) {
				TrackingToolsHelper.FindChessboardCorners( _camTexGrayUndistortMat, _chessPatternSize, ref _chessCornersImageMat );
				_extrinsicsCalibrator.UpdateExtrinsics( _chessCornersWorldMat, _chessCornersImageMat, _intrinsics, _cameraTexture.width, _cameraTexture.height );
			}

			if( !_extrinsicsCalibrator.isValid ){
				Debug.LogWarning( "Save extrinsics to file failed. No chessboard was found in camera image.\n" );
				return;
			}

			if( !string.IsNullOrEmpty( optionalFileName ) ) _extrinsicsFileName = optionalFileName;

			_extrinsicsCalibrator.extrinsics.SaveToFile( _extrinsicsFileName );

			Debug.Log( logPrepend + "Saved extrinsics to file.\n" + _extrinsicsFileName );
		}


		bool AdaptResources()
		{
			int w = _cameraTexture.width;
			int h = _cameraTexture.height;
			if( _processedCameraTexture != null && _processedCameraTexture.width == w && _processedCameraTexture.height == h ) return true; // Already adapted.

			// Get and apply intrinsics.
			bool success = _intrinsics.ToOpenCV( ref _sensorMat, ref _distortionCoeffsMat, w, h );
			if( !success ) return false;

			_intrinsics.ApplyToCamera( _mainCamera );

			// Create mats and textures.
			_camTexGrayMat = new Mat( h, w, CvType.CV_8UC1 );
			_camTexGrayUndistortMat = new Mat( h, w, CvType.CV_8UC1 );
			_processedCameraTexture = new Texture2D( w, h, GraphicsFormat.R8_UNorm, 0, TextureCreationFlags.None );
			_processedCameraTexture.name = "UndistortedCameraTex";
			_processedCameraTexture.wrapMode = TextureWrapMode.Repeat;
			_arTexture = new RenderTexture( w, h, 16, GraphicsFormat.R8G8B8A8_UNorm );
			_arTexture.name = "AR Texture";

			// Create undistort map (sensorMat remains unchanged even through it is passed as newCameraMatrix).
			Calib3d.initUndistortRectifyMap( _sensorMat, _distortionCoeffsMat, new Mat(), _sensorMat, new Size( _cameraTexture.width, _cameraTexture.height ), CvType.CV_32FC1, _undistortMap1, _undistortMap2 );

			// Update UI.
			_aspectFitter.aspectRatio = w / (float) h;
			_processedCameraImage.texture = _processedCameraTexture;
			_arImage.texture = _arTexture;
			_mainCamera.targetTexture = _arTexture;

			// Log.
			Debug.Log( logPrepend + "Tracking chessboard in camera image at " + w  + "x" + h + "\n" );

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

			float pointRadius = ( _chessPatternTransform.localScale.y / (float) _chessPatternSize.y ) * 0.2f;
			Gizmos.color = Color.red;
			for( int i = 0; i < _chessCornersWorldMat.total(); i++ ) {
				Vector3 p = _chessCornersWorldMat.ReadVector3( i );
				Gizmos.DrawSphere( p, pointRadius );
			}
		}
	}
}