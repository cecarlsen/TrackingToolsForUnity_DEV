/*
	Copyright © Carl Emil Carlsen 2020
	http://cec.dk

	To compute the intrinsics of a camera you need multiple pairs of sampled image space and real space pattern points.
	Either the image space points or the real space points need to change for each sample.
	Image space is measured in pixels and "real space" is measured in millimeters.
*/

using System.Collections.Generic;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.Calib3dModule;

namespace TrackingTools
{
	public class IntrinsicsCalibrator
	{
		List<Mat> _patternRealSamples;
		List<Mat> _patternImageSamples;
		Mat _sensorMat;
		MatOfDouble _distortionCoeffsMat;
		List<Mat> _rotationSamples;
		List<Mat> _translationSamples;
		Size _textureSize;
		Intrinsics _intrinsics;
		float _rmsError = 1;

		public int sampleCount { get { return _patternRealSamples.Count; } }
		public Mat sensorMat { get { return _sensorMat; } }
		public MatOfDouble distortionCoeffsMat { get { return _distortionCoeffsMat; } }
		public Intrinsics intrinsics { get { return _intrinsics; } }
		public int textureWidth { get { return (int) _textureSize.width; } }
		public int textureHeight{ get { return (int) _textureSize.height; } }
		public Size textureSize { get { return _textureSize; } }
		public float rmsError { get { return _rmsError; } }



		public IntrinsicsCalibrator( int imageWidth, int imageHeight )
		{
			const int regularSampleCount = 4;

			_intrinsics = new Intrinsics();
			_patternImageSamples = new List<Mat>( regularSampleCount );
			_patternRealSamples = new List<Mat>( regularSampleCount );
			_distortionCoeffsMat = new MatOfDouble();
			_rotationSamples = new List<Mat>( regularSampleCount );
			_translationSamples = new List<Mat>( regularSampleCount );
			_sensorMat = Mat.eye( 3, 3, CvType.CV_64FC1 );
			_textureSize = new Size( imageWidth, imageHeight );
		}


		/// <summary>
		/// Add a pair of real space + image space points.
		/// Beware that calibration can fail if pattern is not rotated to fade forward, so that z is zero.
		/// Also ensure that the point order in the the two point sets are matching.
		/// </summary>
		/// <param name="patternRealModelSample">Must be measured in millimeters</param>
		/// <param name="patternImageSample"></param>
		public void AddSample( MatOfPoint3f patternRealModelSample, MatOfPoint2f patternImageSample )
		{
			//Debug.Log( "patternRealModelSample\n" + patternRealModelSample.dump() );
			//Debug.Log( "patternImageSample\n" + patternImageSample.dump() );

			_patternRealSamples.Add( patternRealModelSample.clone() );
			_patternImageSamples.Add( patternImageSample.clone() );
		}


		public void UpdateIntrinsics( bool samplesHaveDistortion = true, bool useTextureAspect = false, bool flipVerticalLensShift = false )
		{
			int flags = 0;

			// This is useful in the case of a projector where the imcoming points are already undistorted and we can asume no distortion in the view frustrum.
			if( !samplesHaveDistortion ) {
				flags = 
					Calib3d.CALIB_FIX_TANGENT_DIST |
					Calib3d.CALIB_FIX_K1 |
					Calib3d.CALIB_FIX_K2 |
					Calib3d.CALIB_FIX_K3 |
					Calib3d.CALIB_FIX_K4 |
					Calib3d.CALIB_FIX_K5;
			}

			// This is useful in case of projectors.
			if( useTextureAspect ) flags |= Calib3d.CALIB_FIX_ASPECT_RATIO;

			// https://forum.unity.com/threads/released-opencv-for-unity.277080/page-8#post-2348856
			_rmsError = (float) Calib3d.calibrateCamera( _patternRealSamples, _patternImageSamples, _textureSize, /*out*/ _sensorMat, /*out*/ _distortionCoeffsMat, /*out*/ _rotationSamples, /*out*/ _translationSamples, flags );

			//Debug.Log( "New intrinsics matrix\n" + _sensorMat.dump() );

			// About RMS Error
			// It's the average re-projection error. This number gives a good estimation of precision of the found parameters. 
			// This should be as close to zero as possible. Given the intrinsic, distortion, rotation and translation matrices 
			// we may calculate the error for one view by using the projectPoints to first transform the object point to image 
			// point. Then we calculate the absolute norm between what we got with our transformation and the corner/circle 
			// finding algorithm. To find the average error we calculate the arithmetical mean of the errors calculated for 
			// all the calibration images.
			// https://docs.opencv.org/2.4/doc/tutorials/calib3d/camera_calibration/camera_calibration.html

			// Flip vertical lens shift. It puzzels me why I need to do this for projectors and not cameras ... but it is needed.
			if( flipVerticalLensShift ) _sensorMat.WriteValue( _textureSize.height - _sensorMat.ReadValue( 1, 2 ), 1, 2 ); // cy

			// Update intrinsics object.
			_intrinsics.UpdateFromOpenCV( _sensorMat, _distortionCoeffsMat, textureWidth, textureHeight, _rmsError );
		}


		public void Clear()
		{
			foreach( Mat mat in _patternImageSamples ) mat.release();
			foreach( Mat mat in _patternRealSamples ) mat.release();
			foreach( Mat mat in _rotationSamples ) mat.release();
			foreach( Mat mat in _translationSamples ) mat.release();
			_patternImageSamples.Clear();
			_patternRealSamples.Clear();
			_rotationSamples.Clear();
			_translationSamples.Clear();
		}
	}

}