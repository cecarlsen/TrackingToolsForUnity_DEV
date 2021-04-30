/*
	Copyright © Carl Emil Carlsen 2020
	http://cec.dk

	To compute the intrinsics of a camera you need multiple pairs of sampled image space and real space pattern points.
	Either the image space points or the real space points need to change for each sample.
	Image space is measured in pixels and "real space" is measured in millimeters.
*/

using System.Collections.Generic;
using UnityEngine;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.Calib3dModule;

namespace TrackingTools
{
	public class ProjectorFromCameraExtrinsicsCalibrator
	{
		List<Mat> _patternWorldSamples;
		Mat _cameraSensorMat;
		Mat _projectorSensorMat;
		MatOfDouble _noDistCoeffs;
		List<Mat> _cameraPatternImageSamples;
		List<Mat> _projectorPatternImageSamples;
		Mat _rotation3x3Mat;
		Mat _translationVecMat;
		Mat _essentialMat;
		Mat _fundamentalMat;
		Extrinsics _extrinsics;

		public int sampleCount { get { return _cameraPatternImageSamples.Count; } }
		public Extrinsics extrinsics { get { return _extrinsics; } }


		public ProjectorFromCameraExtrinsicsCalibrator()
		{
			const int regularSampleCount = 8;

			_cameraSensorMat = new Mat();
			_projectorSensorMat = new Mat();
			_noDistCoeffs = new MatOfDouble( new double[ 5 ] );
			_patternWorldSamples = new List<Mat>( regularSampleCount );
			_projectorPatternImageSamples = new List<Mat>( regularSampleCount );
			_cameraPatternImageSamples = new List<Mat>( regularSampleCount );
			_rotation3x3Mat = new Mat();
			_translationVecMat = new Mat();
			_essentialMat = new Mat();
			_fundamentalMat = new Mat();
			_extrinsics = new Extrinsics();
		}


		/// <summary>
		/// Add a pair of real space + image space points. Points must be undistorted.
		/// </summary>
		/// <param name="patternRealSample">Must be measured in millimeters</param>
		/// <param name="patternImageSample"></param>
		public void AddSample( MatOfPoint3f patternWorldSample, MatOfPoint2f cameraPatternImageSample, MatOfPoint2f projectorPatternImageSample )
		{
			_patternWorldSamples.Add( patternWorldSample.clone() );
			_cameraPatternImageSamples.Add( cameraPatternImageSample.clone() );
			_projectorPatternImageSamples.Add( projectorPatternImageSample.clone() );
		}


		public void RemovePreviousSample()
		{
			int index = _patternWorldSamples.Count - 1;
			_patternWorldSamples.RemoveAt( index );
			_cameraPatternImageSamples.RemoveAt( index );
			_projectorPatternImageSamples.RemoveAt( index );
		}


		/// <summary>
		/// Update the extrinsics of projector relative to camera.
		/// </summary>
		/// <param name="cameraIntrinsics"></param>
		/// <param name="projectorIntrinsics"></param>
		/// <param name="textureSize"></param>
		public void Update( Intrinsics cameraIntrinsics, Intrinsics projectorIntrinsics, Size textureSize )
		{
			int w = (int) textureSize.width;
			int h = (int) textureSize.height;

			cameraIntrinsics.ToOpenCV( ref _cameraSensorMat, w, h );
			projectorIntrinsics.ToOpenCV( ref _projectorSensorMat, w, h );

			// In order to match OpenCV's pixel space (zero at top-left) and Unity's camera space (up is positive), we flip the sensor matrix.

			_cameraSensorMat.WriteValue( - _cameraSensorMat.ReadValue( 1, 1 ), 1, 1 ); // fy
			_cameraSensorMat.WriteValue( textureSize.height - _cameraSensorMat.ReadValue( 1, 2 ), 1, 2 ); // cy
			_projectorSensorMat.WriteValue( - _projectorSensorMat.ReadValue( 1, 1 ), 1, 1 ); // fy
			_projectorSensorMat.WriteValue( textureSize.height - _projectorSensorMat.ReadValue( 1, 2 ), 1, 2 ); // cy

			int flag = 0;
			
			// Don't recompute and change intrinsics parameters.
			flag |= Calib3d.CALIB_FIX_INTRINSIC;
			
			// Don't recompute distortions, ignore them. We assume the incoming points have already bee undistorted.
			flag |=
				Calib3d.CALIB_FIX_TANGENT_DIST |
				Calib3d.CALIB_FIX_K1 |
				Calib3d.CALIB_FIX_K2 |
				Calib3d.CALIB_FIX_K3 |
				Calib3d.CALIB_FIX_K4 |
				Calib3d.CALIB_FIX_K5;

			// Compute!
			Calib3d.stereoCalibrate
			(
				_patternWorldSamples, _cameraPatternImageSamples, _projectorPatternImageSamples,
				_cameraSensorMat, _noDistCoeffs,
				_projectorSensorMat, _noDistCoeffs,
				textureSize,
				_rotation3x3Mat, _translationVecMat, _essentialMat, _fundamentalMat,
				flag
			);

			_extrinsics.UpdateFromOpenCvStereoCalibrate( _rotation3x3Mat, _translationVecMat );
		}


		public void Clear()
		{
			foreach( Mat mat in _patternWorldSamples ) mat.Dispose();
			foreach( Mat mat in _projectorPatternImageSamples ) mat.Dispose();
			foreach( Mat mat in _cameraPatternImageSamples ) mat.Dispose();
			_patternWorldSamples.Clear();
			_projectorPatternImageSamples.Clear();
			_cameraPatternImageSamples.Clear();
		}
	}
}