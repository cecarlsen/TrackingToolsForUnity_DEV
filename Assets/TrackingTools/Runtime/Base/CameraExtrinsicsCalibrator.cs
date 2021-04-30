/*
	Copyright © Carl Emil Carlsen 2020
	http://cec.dk
*/

using OpenCVForUnity.CoreModule;
using OpenCVForUnity.Calib3dModule;

namespace TrackingTools
{
	public class CameraExtrinsicsCalibrator
	{
		Extrinsics _extrinsics;

		Mat _sensorMatrix;
		MatOfDouble _noDistCoeffs;

		Mat _rotationVecMat;
		Mat _translationVecMat;

		bool _isValid;

		public Extrinsics extrinsics { get { return _extrinsics; } }
		public bool isValid { get { return _isValid; } }


		public CameraExtrinsicsCalibrator()
		{
			_noDistCoeffs = new MatOfDouble( new double[5] );
			_rotationVecMat = new Mat();
			_translationVecMat = new Mat();
			_extrinsics = new Extrinsics();
		}


		public bool UpdateExtrinsics( MatOfPoint3f patternPointsWorldMat, MatOfPoint2f patternPointsImageMat, Intrinsics intrinsics, int imageWidth, int imageHeight )
		{
			intrinsics.ToOpenCV( ref _sensorMatrix, imageWidth, imageHeight );

			// In order to match OpenCV's pixel space (zero at top-left) and Unity's camera space (up is positive), we flip the sensor matrix.
			_sensorMatrix.WriteValue( - _sensorMatrix.ReadValue( 1, 1 ), 1, 1 ); // fy
			_sensorMatrix.WriteValue( imageHeight - _sensorMatrix.ReadValue( 1, 2 ), 1, 2 ); // cy

			// Find pattern pose, relative to camera (at zero position) using solvePnP.
			_isValid = Calib3d.solvePnP( patternPointsWorldMat, patternPointsImageMat, _sensorMatrix, _noDistCoeffs, _rotationVecMat, _translationVecMat );

			if( _isValid ) {
				_extrinsics.UpdateFromOpenCvSolvePnp( _rotationVecMat, _translationVecMat );
			}

			return _isValid;
		}


		public void Release()
		{
			_noDistCoeffs.release();
			_rotationVecMat.release();
			_translationVecMat.release();
		}
	}
}