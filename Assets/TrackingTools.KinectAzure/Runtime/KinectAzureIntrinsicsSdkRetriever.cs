/*
	Copyright © Carl Emil Carlsen 2021
	http://cec.dk
*/

using UnityEngine;
using com.rfilkov.kinect;
using Microsoft.Azure.Kinect.Sensor;

namespace TrackingTools
{
	public class KinectAzureIntrinsicsSdkRetriever : MonoBehaviour
	{
		[SerializeField] Kinect4AzureInterface _kinectInterface = null;
		[SerializeField] DepthMode _depthMode = DepthMode.NFOV_Unbinned;
		[SerializeField] ColorResolution _colorResolution = ColorResolution.R720p;

		[ Header("Output")]
		[SerializeField] string _intrinsicsFileName = "Kinect";
		[SerializeField] bool _appendSerialToFileName = true;
		[SerializeField] string _appendFileName= "";

		KinectManager _kinectManager;

		public string appendFileName {
			get { return _appendFileName; }
			set { _appendFileName = value; }
		}


		void Init()
		{
			_kinectManager = KinectManager.Instance;
			Device sensor = _kinectInterface.kinectSensor;
			Calibration calibration = sensor.GetCalibration( _depthMode, _colorResolution );

			ConvertAndSaveToFile( _intrinsicsFileName + "_Depth_" + _depthMode.ToString(), calibration.DepthCameraCalibration );
			ConvertAndSaveToFile( _intrinsicsFileName + "_Color_" + _colorResolution.ToString(), calibration.ColorCameraCalibration );
		}


		void ConvertAndSaveToFile( string fileName, CameraCalibration cameraCalibration )
		{
			Microsoft.Azure.Kinect.Sensor.Intrinsics kinectIntrinsics = cameraCalibration.Intrinsics;

			// "Brown-Conrady" is the distortion model for OpenCV undist() K1-K6  https://stackoverflow.com/questions/61148203/reference-for-opencvs-camera-model
			if( kinectIntrinsics.Type != CalibrationModelType.BrownConrady ) {
				Debug.LogError( "Something went wrong. OpenCV needs Brown-Conrady distortion model.\n" );
				return;
			}
			
			//Debug.Log( fileName );
			//Debug.Log( string.Join( ", ", kinectIntrinsics.Parameters ) );

			// Intrinsics parameters from the Kinect SDK:
			//		cx, cy, fx, fy, 
			//		k1, k2, k3, k4, k5, k6, 
			//		codx, cody, p2, p1, metric radius.
			// https://microsoft.github.io/Azure-Kinect-Sensor-SDK/master/structk4a__calibration__intrinsic__parameters__t_1_1__param.html
			float[] p = kinectIntrinsics.Parameters;
			int w = cameraCalibration.ResolutionWidth;
			int h = cameraCalibration.ResolutionHeight;

			// Convert to TrackingTools intrinsics.
			Intrinsics intrinsics = new Intrinsics();
			double cx = p[ 0 ] / (double) w;
			double cy = p[ 1 ] / (double) h;
			double fx = p[ 2 ] / (double) w;
			double fy = p[ 3 ] / (double) h;
			double[] distCoeff = new double[] { // 8 distortion parameters
				p[ 4 ], p[ 5 ],					// k1, k2,
				p[ 13 ], p[ 12 ],				// p1, p2,
				p[ 6 ], p[ 7 ], p[ 8 ], p[ 9 ]	// k3, k4, k5, k6
			};
			double aspect = w / (double) h;
			const double rmsError = 0; // Presuming they got it right at the factory.
			intrinsics.Update( cx, cy, fx, fy, distCoeff, aspect, rmsError );

			//Debug.Log( "Resolution: " + w + "x" + h );
			//Debug.Log( intrinsics );
			//Debug.Log( "FOV: " + intrinsics.FovHorizontal + ", " + intrinsics.FovVertical );

			// Save.
			if( _appendSerialToFileName ) fileName += " " + _kinectInterface.kinectSensor.SerialNum;
			if( !string.IsNullOrEmpty( _appendFileName ) ) fileName += " " + _appendFileName;
			intrinsics.SaveToFile( fileName );
		}


		void Update()
		{
			if( !_kinectManager && KinectManager.Instance && KinectManager.Instance.IsDepthSensorsStarted() ) Init();
		}
	}
}