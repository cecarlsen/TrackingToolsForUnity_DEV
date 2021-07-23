/*
	Copyright © Carl Emil Carlsen 2021
	http://cec.dk
*/

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Experimental.Rendering;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.Calib3dModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.ImgprocModule;

namespace TrackingTools.Tests
{
	public class UndistortionTest : MonoBehaviour
	{
		[SerializeField] Texture _sourceTexture = null;
		[SerializeField] string _intrinsicsFileName = "";
		[SerializeField] Mode _mode = Mode.Calib3d_undistort;

		[Header("UI")]
		[SerializeField] RawImage _rawImage = null;


		Intrinsics _intrinsics;

		Mat _sensorMat;
		MatOfDouble _distMat;

		Mat _sourceMat;
		Mat _undistortedMat;

		Color32[] _tempTransferColors;
		Texture2D _tempTranferTexture;

		Mat _undistortMap;
		Mat _undistortMapUnused;

		CameraLensUndistorterGpu _gpuUndistorter;

		Texture _processedCameraTexture;


		public Texture sourceTexture {
			get { return _sourceTexture; }
			set { _sourceTexture = value; }
		}


		[System.Serializable] enum Mode { Calib3d_undistort, Imgproc_remap, CameraLensUndistorterGpu }


		void Awake()
		{
			if( !Intrinsics.TryLoadFromFile( _intrinsicsFileName, out _intrinsics ) ) {
				Debug.LogError( "Instrinsics file not found.\n" );
				enabled = false;
				return;
			}

			_gpuUndistorter = new CameraLensUndistorterGpu();

			_undistortMap = new Mat();
			_undistortMapUnused = new Mat();
		}


		void OnDestroy()
		{
			if( _sensorMat != null ) _sensorMat.release();
			if( _distMat != null ) _distMat.release();
			if( _sourceMat != null ) _sourceMat.release();
			if( _undistortedMat != null ) _undistortedMat.release();
			if( _undistortMap != null ) _undistortMap.release();
			if( _undistortMapUnused != null ) _undistortMapUnused.release();
			_gpuUndistorter.Release();
		}


		void Update()
		{
			if( !_sourceTexture ) return;

			int w = _sourceTexture.width;
			int h = _sourceTexture.height;

			if( _sensorMat == null )
			{
				_intrinsics.ToOpenCV( ref _sensorMat, ref _distMat, w, h );

				_undistortedMat = new Mat( h, w, CvType.CV_8UC4 );
				_processedCameraTexture = new Texture2D( w, h, _sourceTexture.graphicsFormat, 0, TextureCreationFlags.None );
				_processedCameraTexture.name = "UndistortedCameraTex";
				_processedCameraTexture.wrapMode = TextureWrapMode.Repeat;

				// Create undistort map (sensorMat remains unchanged even through it is passed as newCameraMatrix).
				// https://docs.opencv.org/4.5.2/d9/d0c/group__calib3d.html#ga7dfb72c9cf9780a347fbe3d1c47e5d5a
				Calib3d.initUndistortRectifyMap( _sensorMat, _distMat, new Mat(), _sensorMat, new Size( w, h ), CvType.CV_32FC2, _undistortMap, _undistortMapUnused );
			}

			if( _mode != Mode.CameraLensUndistorterGpu ) {
				TrackingToolsHelper.TextureToMat( _sourceTexture, false, ref _sourceMat, ref _tempTransferColors, ref _tempTranferTexture );
			}

			switch( _mode )
			{
				case Mode.Calib3d_undistort:
					Calib3d.undistort( _sourceMat, _undistortedMat, _sensorMat, _distMat );
					break;
				case Mode.Imgproc_remap:
					Imgproc.remap( _sourceMat, _undistortedMat, _undistortMap, _undistortMapUnused, Imgproc.INTER_LINEAR );
					break;
				case Mode.CameraLensUndistorterGpu:
					_gpuUndistorter.Update( _sourceTexture, _intrinsics );
					break;
			}

			if( _mode != Mode.CameraLensUndistorterGpu ) {
				Utils.fastMatToTexture2D( _undistortedMat, (Texture2D) _processedCameraTexture, flip: false, flipCode: 1, flipAfter: true );
				if( _rawImage ) _rawImage.texture = _processedCameraTexture;
			} else {
				if( _rawImage ) _rawImage.texture = _gpuUndistorter.undistortedTexture;
			}
		}
	}
}