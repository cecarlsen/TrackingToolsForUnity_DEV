/*
	Copyright © Carl Emil Carlsen 2020-2022
	http://cec.dk

	Camera intriniscs values stored as defined by OpenCV.

	Beware that values are NOT independent from aspect.
	Imagine you crop a 16:9 camera to 4:3. This will most
	certainly change the distortion coefficents.

	OpenCV camera intrinsics matrix
	
		[ fx,  0, cx ]
		[  0, fy, cy ]
		[  0,  0,  1 ]
		
		https://docs.opencv.org/4.x/dc/dbb/tutorial_py_calibration.html
*/

using System.IO;
using UnityEngine;
using OpenCVForUnity.CoreModule;

namespace TrackingTools
{
	[System.Serializable]
	public class Intrinsics
	{
		/// <summary>
		/// The horizontal principal point as defined by OpenCV; an offset measure in pixels from upper-left corner (right is positive).
		/// If camera sensor is placed perfectly on-axis the value will be: ( referenceResolution.x * 0.5 ).
		/// Lens shift can be derived: ( shiftX = 0.5 - cx / referenceResolution.x )
		/// </summary>
		[SerializeField] double _cx;
		
		/// <summary>
		/// The vertical principal point as defined by OpenCV; an offset measure in pixels from upper-left corner (down is positive).
		/// If camera sensor is placed perfectly on-axis the value will be: ( referenceResolution.y * 0.5 ).
		/// Lens shift can be derived: ( shiftX = 0.5 - cx / referenceResolution.y )
		/// </summary>
		[SerializeField] double _cy;

		/// <summary>
		/// Horizontal focal length as defined by OpenCV.
		/// Product of the physical focal length of the lens (F measured in mm) and the horizontal size of the (potentially non-square) individual imager elements (sx measured in px/mm). Equation: ( fx = F * sx ).
		/// </summary>
		[SerializeField] double _fx;
		
		/// <summary>
		/// Vertical focal length as defined by OpenCV.
		/// Product of the physical focal length of the lens (F measured in mm) and the horizontal size of the (potentially non-square) individual imager elements (sx measured in px/mm). Equation: ( fy = F * sy ).
		/// </summary>
		[SerializeField] double _fy;

		/// <summary>
		/// Radial and tangential distortion coefficients as defined by OpenCV: [k1,k2,p1,p2,k3]. https://docs.opencv.org/4.x/dc/dbb/tutorial_py_calibration.html
		/// </summary>
		[SerializeField] double[] _distortionCoeffs;

		/// <summary>
		/// Reference image pixel resolution. The values of cx, cy, fx, fy will be relative to this.
		/// </summary>
		[SerializeField] Vector2Int _referenceResolution;

		/// <summary>
		/// Root mean square error.
		/// </summary>
		[SerializeField] double _rmsError;


		public bool isValid => _distortionCoeffs != null; // If json deserialization failed _distortionCoeffs will be null.


		public Vector2Int referenceResolution => _referenceResolution;

		float imageAspect => _referenceResolution.x / (float) _referenceResolution.y;


		static readonly string logPrepend = "<b>[" + nameof( Intrinsics ) + "]</b> ";
		const int distortionCoeffCount = 5;



		public string SaveToFile( string fileName )
		{
			if( !Directory.Exists( TrackingToolsConstants.intrinsicsDirectoryPath ) ) Directory.CreateDirectory( TrackingToolsConstants.intrinsicsDirectoryPath );
			string filePath = TrackingToolsConstants.intrinsicsDirectoryPath + "/" + fileName;
			if( !fileName.EndsWith( ".json" ) ) filePath += ".json";
			File.WriteAllText( filePath, JsonUtility.ToJson( this ) );
			return filePath;
		}
		

		public static bool TryLoadFromFile( string fileName, out Intrinsics intrinsics )
		{
			intrinsics = null;

			if( !Directory.Exists( TrackingToolsConstants.intrinsicsDirectoryPath ) ) {
				Debug.LogError( logPrepend + "Directory missing.\n" + TrackingToolsConstants.intrinsicsDirectoryPath );
				return false;
			}

			string filePath = TrackingToolsConstants.intrinsicsDirectoryPath + "/" + fileName;
			if( !fileName.EndsWith( ".json" ) ) filePath += ".json";
			if( !File.Exists( filePath ) ) {
				Debug.LogError( logPrepend + "File missing.\n" + filePath );
				return false;
			}

			string jsonText = File.ReadAllText( filePath );
			intrinsics = JsonUtility.FromJson<Intrinsics>( jsonText );

			if( !intrinsics.isValid ) {
				Debug.LogError( logPrepend + "Failed to load json text:\n" + jsonText );
				return false;
			}

			return true;
		}


		public void UpdateFromOpenCV( Mat cameraIntrinsicsMat, MatOfDouble distCoeffsMat, Vector2Int referenceResolution, float rmsError )
		{
			if( _distortionCoeffs == null || distCoeffsMat.IsDisposed || _distortionCoeffs.Length != distCoeffsMat.total() ){
				_distortionCoeffs = new double[ distCoeffsMat.total() ];
			}

			_referenceResolution = referenceResolution;

			UpdateFromOpenCVCameraIntrinsicsMatrix( cameraIntrinsicsMat );

			for( int i = 0; i < _distortionCoeffs.Length; i++ ) _distortionCoeffs[i] = distCoeffsMat.ReadValue( i );

			this._rmsError = rmsError;
		}


		public void UpdateFromOpenCV( Mat cameraIntrinsicsMat, Vector2Int referenceResolution, float rmsError )
		{
			_referenceResolution = referenceResolution;

			UpdateFromOpenCVCameraIntrinsicsMatrix( cameraIntrinsicsMat );

			this._rmsError = rmsError;
		}


		public bool ApplyToToOpenCV( ref Mat cameraIntrinsicsMat, ref MatOfDouble distCoeffsMat )
		{
			if( cameraIntrinsicsMat == null || cameraIntrinsicsMat.IsDisposed || cameraIntrinsicsMat.rows() != 3 || cameraIntrinsicsMat.cols() != 3 ){
				cameraIntrinsicsMat = Mat.eye( 3, 3, CvType.CV_64F );
			}
			if( distCoeffsMat == null || distCoeffsMat.IsDisposed || distCoeffsMat.total() != distortionCoeffCount ) {
				distCoeffsMat = new MatOfDouble( new Mat( 1, distortionCoeffCount, CvType.CV_64F ) ); // This seems to be the only way to get distCoeffs.Length columns.
			}

			ApplyToOpenCVCameraIntrinsicsMatrix( cameraIntrinsicsMat );

			for( int i = 0; i < _distortionCoeffs.Length; i++ ) distCoeffsMat.WriteValue( _distortionCoeffs[ i ], i );

			return true;
		}


		public bool ApplyToToOpenCV( ref Mat cameraIntrinsicsMat )
		{
			if( cameraIntrinsicsMat == null || cameraIntrinsicsMat.IsDisposed || cameraIntrinsicsMat.rows() != 3 || cameraIntrinsicsMat.cols() != 3 ) {
				cameraIntrinsicsMat = Mat.eye( 3, 3, CvType.CV_64F );
			}

			ApplyToOpenCVCameraIntrinsicsMatrix( cameraIntrinsicsMat );

			return true;
		}


		public void UpdateFromUnityCamera( Camera cam )
		{
			if( cam.orthographic ){
				Debug.LogError( logPrepend + " UpdateFromUnityCamera failed. Camera cannot be orthographic.\n" );
				return;
			}

			if( !cam.usePhysicalProperties ){
				Debug.LogError( logPrepend + " UpdateFromUnityCamera failed. Camera must use physical properties.\n" );
				return;
			}

			cam.gateFit = Camera.GateFitMode.None;

			_referenceResolution = new Vector2Int( cam.pixelWidth, cam.pixelHeight );

			_cx = ( -cam.lensShift.x + 0.5f ) * _referenceResolution.x;
			_cy = (  cam.lensShift.y + 0.5f ) * _referenceResolution.y;
			_fx = ( cam.focalLength / cam.sensorSize.x ) * _referenceResolution.x;
			_fy = ( cam.focalLength / cam.sensorSize.y ) * _referenceResolution.y;

			_distortionCoeffs = new double[ distortionCoeffCount ]; // No distortion.

			_rmsError = 0; // No error.
		}


		public void ApplyToUnityCamera( Camera cam )
		{
			// Great explanation by jungguswns:
			// https://forum.unity.com/threads/how-to-use-opencv-camera-calibration-to-set-physical-camera-parameters.704120/

			// Also, about sensor size and focal lengths.
			// https://answers.opencv.org/question/139166/focal-length-from-calibration-parameters/

			cam.orthographic = false;
			cam.usePhysicalProperties = true;
			cam.gateFit = Camera.GateFitMode.None;

			// Keep the current focal length. F can be arbitrary, as long as sensor size is resized to to make fx and fy consistient
			float focalLength = cam.focalLength;

			cam.lensShift =  new Vector2(
				(float) - ( ( _cx / (double) _referenceResolution.x ) - 0.5 ), 
				(float)   ( ( _cy / (double) _referenceResolution.y ) - 0.5 )
			);
			cam.sensorSize = new Vector2(
				(float) ( focalLength * _referenceResolution.x / _fx ),
				(float) ( focalLength * _referenceResolution.y / _fy )
			);
		}


		/*
		public void ToProjectionMatrix
		(
			float near, float far,
			ref Matrix4x4 projectionMatrix
		)
		{

		}
		*/


		void UpdateFromOpenCVCameraIntrinsicsMatrix( Mat cameraIntrinsicsMat )
		{
			_fx = cameraIntrinsicsMat.ReadValue( 0, 0 );
			_fy = cameraIntrinsicsMat.ReadValue( 1, 1 );
			_cx = cameraIntrinsicsMat.ReadValue( 0, 2 );
			_cy = cameraIntrinsicsMat.ReadValue( 1, 2 );
		}


		void ApplyToOpenCVCameraIntrinsicsMatrix( Mat cameraIntrinsicsMat )
		{
			cameraIntrinsicsMat.WriteValue( _fx, 0, 0 );
			cameraIntrinsicsMat.WriteValue( _fy, 1, 1 );
			cameraIntrinsicsMat.WriteValue( _cx, 0, 2 );
			cameraIntrinsicsMat.WriteValue( _cy, 1, 2 );
		}


		/*
		public void FlipY()
		{
			fy = -fy;
			cy = 1 - cy;
		}
		*/


		public override string ToString()
		{
			if( !isValid ) return "Invalid";
			return "(cx,cy,fx,fy): ( " + _cx + ", " + _cy + ", " + _fx + ", " + _fy + " ) dist: ( " + _distortionCoeffs[0] + ", " + _distortionCoeffs[ 1 ] + ", " + _distortionCoeffs[ 2 ] + ", " + _distortionCoeffs[ 3 ] + ", " + _distortionCoeffs[ 4 ] + " )";
		}
	}
}