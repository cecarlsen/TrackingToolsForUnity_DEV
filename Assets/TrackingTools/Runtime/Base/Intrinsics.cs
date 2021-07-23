/*
	Copyright © Carl Emil Carlsen 2020-2021
	http://cec.dk

	Intriniscs values stored independent from resolution.

	Beware that values are NOT independent from aspect.
	Imagine you crop a 16:9 camera to 4:3. This will most
	certainly change the distortion coefficents.
*/

using System.IO;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.Calib3dModule;
using OpenCVForUnity.UnityUtils;

namespace TrackingTools
{
	[System.Serializable]
	public class Intrinsics
	{
		// We store resolution independent values in viewport space (zero at bottom-left).
		[SerializeField] double cx, cy;		// Principal point that is usually at the image center.
		[SerializeField] double fx, fy;		// Focal lengths.

		// Order as provided by Calib3d.calibrateCamera and expected by initUndistortRectifyMap: K1, K2, P1, P2, K3.
		[SerializeField] double[] distortionCoeffs; 

		[SerializeField] double aspect;
		[SerializeField] double rmsError;

		static readonly string logPrepend = "<b>[" + nameof( Intrinsics ) + "]</b> ";

		public double Cx => cx;
		public double Cy => cy;
		public double Fx => fx;
		public double Fy => fy;

		/// <summary>
		/// Width divided by height.
		/// </summary>
		public double Aspect => aspect;

		/// <summary>
		/// Field of view (vertically) in degrees.
		/// Note that FOV makes less sense as the principal point offset diviates from 0.5.
		/// </summary>
		public double FovVertical => Mathf.Atan2( 0.5f, (float) fy ) * 2 * Mathf.Rad2Deg;

		/// <summary>
		/// Field of view (horizontally) in degrees.
		/// Note that FOV makes less sense as the principal point offset diviates from 0.5.
		/// </summary>
		public double FovHorizontal => Mathf.Atan2( 0.5f, (float) fx ) * 2 * Mathf.Rad2Deg;


		public void ApplyToCamera( Camera cam )
		{
			// Great explanation by jungguswns:
			// https://forum.unity.com/threads/how-to-use-opencv-camera-calibration-to-set-physical-camera-parameters.704120/

			// Also, about sensor size and focal lengths.
			// https://answers.opencv.org/question/139166/focal-length-from-calibration-parameters/

			float focalLength = cam.focalLength; // f can be arbitrary, as long as sensor_size is resized to to make ax, ay consistient.
			cam.orthographic = false;
			cam.usePhysicalProperties = true;
			cam.sensorSize = new Vector2( (float) ( focalLength / fx ), (float) ( focalLength / fy ) );
			Vector2 lensShift = new Vector2( (float) ( ( 0.5 - cx ) / 1.0 ), (float) ( ( 0.5 - cy ) / 1.0 ) );
			//if( flippedLensShiftY ) lensShift.y *= -1;
			cam.lensShift = lensShift;
			cam.gateFit = Camera.GateFitMode.None;
		}


		public void DrawFrustumGizmo( Vector3 originPosition, Quaternion originRotation, float nearClip, float farClip )
		{
			Gizmos.matrix *= Matrix4x4.TRS( originPosition, originRotation, Vector3.one );

			Vector3 dirSE = new Vector3( ( 0f - (float) cx ) / (float) fx, ( 0f - (float) cy ) / (float) fy, 1 );
			Vector3 dirNE = new Vector3( ( 0f - (float) cx ) / (float) fx, ( 1f - (float) cy ) / (float) fy, 1 );
			Vector3 dirNW = new Vector3( ( 1f - (float) cx ) / (float) fx, ( 1f - (float) cy ) / (float) fy, 1 );
			Vector3 dirSW = new Vector3( ( 1f - (float) cx ) / (float) fx, ( 0f - (float) cy ) / (float) fy, 1 );

			Vector3 nearSE = dirSE * nearClip;
			Vector3 farSE = dirSE * farClip;
			Vector3 nearNE = dirNE * nearClip;
			Vector3 farNE = dirNE * farClip;
			Vector3 nearNW = dirNW * nearClip;
			Vector3 farNW = dirNW * farClip;
			Vector3 nearSW = dirSW * nearClip;
			Vector3 farSW = dirSW * farClip;

			Gizmos.DrawLine( nearSE, farSE );
			Gizmos.DrawLine( nearNE, farNE );
			Gizmos.DrawLine( nearNW, farNW );
			Gizmos.DrawLine( nearSW, farSW );
			Gizmos.DrawLine( nearSE, nearNE );
			Gizmos.DrawLine( nearNE, nearNW );
			Gizmos.DrawLine( nearNW, nearSW );
			Gizmos.DrawLine( nearSW, nearSE );
			Gizmos.DrawLine( farSE, farNE );
			Gizmos.DrawLine( farNE, farNW );
			Gizmos.DrawLine( farNW, farSW );
			Gizmos.DrawLine( farSW, farSE );
		}

		
		public Texture2D CreateUndistortRectifyTexture( int width, int height )
		{
			Mat sensorMat = null;
			MatOfDouble distMat = null;
			Mat undistortMap = new Mat();
			Mat undistortMapUnused = new Mat();

			ToOpenCV( ref sensorMat, ref distMat, width, height );

			// Create undistort map.
			// SensorMat remains unchanged even through it is passed as newCameraMatrix.
			// By passing in CV_32FC2 as type, we are requesting both U and V offsets to be stored in map1 (undistortMap).
			// https://docs.opencv.org/4.5.2/d9/d0c/group__calib3d.html#ga7dfb72c9cf9780a347fbe3d1c47e5d5a
			Calib3d.initUndistortRectifyMap( sensorMat, distMat, new Mat(), sensorMat, new Size( width, height ), CvType.CV_32FC2, undistortMap, undistortMapUnused );

			Texture2D undistortionTexture = new Texture2D( width, height, GraphicsFormat.R32G32_SFloat, TextureCreationFlags.None );
			undistortionTexture.name = "UndistortLUT";
			undistortionTexture.wrapMode = TextureWrapMode.Repeat;

			Utils.fastMatToTexture2D( undistortMap, undistortionTexture );

			sensorMat.release();
			distMat.release();
			undistortMap.release();
			undistortMapUnused.release();

			return undistortionTexture;
		}


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

			intrinsics = JsonUtility.FromJson<Intrinsics>( File.ReadAllText( filePath ) );
			return true;
		}


		public void Update( double cx, double cy, double fx, double fy, double[] distortionCoeffs, double aspect, double rmsError )
		{
			this.cx = cx;
			this.cy = cy;
			this.fx = fx;
			this.fy = fy;
			if( this.distortionCoeffs == null || this.distortionCoeffs.Length != distortionCoeffs.Length ) this.distortionCoeffs = new double[ distortionCoeffs.Length ];
			System.Array.Copy( distortionCoeffs, 0, this.distortionCoeffs, 0, distortionCoeffs.Length );
			this.aspect = aspect;
			this.rmsError = rmsError;
		}


		public void UpdateFromOpenCV( Mat sensorMat, MatOfDouble distCoeffsMat, int width, int height, float rmsError )
		{
			if( distortionCoeffs == null || distortionCoeffs.Length != distCoeffsMat.total() ){
				distortionCoeffs = new double[ distCoeffsMat.total() ];
			}

			fx = sensorMat.ReadValue( 0, 0 ) / (double) width;
			fy = sensorMat.ReadValue( 1, 1 ) / (double) height;
			cx = sensorMat.ReadValue( 0, 2 ) / (double) width;
			cy = sensorMat.ReadValue( 1, 2 ) / (double) height;
			for( int i = 0; i < distortionCoeffs.Length; i++ ) distortionCoeffs[i] = distCoeffsMat.ReadValue( i );
			aspect = width / (double) height;
			this.rmsError = rmsError;
		}


		public void UpdateFromOpenCV( Mat sensorMat, int width, int height, float rmsError )
		{
			fx = sensorMat.ReadValue( 0, 0 ) / (double) width;
			fy = sensorMat.ReadValue( 1, 1 ) / (double) height;
			cx = sensorMat.ReadValue( 0, 2 ) / (double) width;
			cy = sensorMat.ReadValue( 1, 2 ) / (double) height;
			aspect = width / (double) height;
			this.rmsError = rmsError;
		}


		public bool ToOpenCV( ref Mat sensorMat, ref MatOfDouble distCoeffsMat, int width, int height )
		{
			if( !ValidateAspect( width, height ) ) return false;

			if( sensorMat == null || sensorMat.IsDisposed || sensorMat.rows() != 3 || sensorMat.cols() != 3 ){
				sensorMat = Mat.eye( 3, 3, CvType.CV_64F );
			}
			if( distCoeffsMat == null || distCoeffsMat.IsDisposed || distCoeffsMat.total() != distortionCoeffs.Length ) {
				distCoeffsMat = new MatOfDouble( new Mat( 1, distortionCoeffs.Length, CvType.CV_64F ) ); // This seems to be the only way to get distCoeffs.Length columns.
			}

			// Assuming the rest of the matrix is identity.
			sensorMat.WriteValue( fx * width, 0, 0 );
			sensorMat.WriteValue( fy * height, 1, 1 );
			sensorMat.WriteValue( cx * width, 0, 2 );
			sensorMat.WriteValue( cy * height, 1, 2 );

			for( int i = 0; i < distortionCoeffs.Length; i++ ) distCoeffsMat.WriteValue( distortionCoeffs[ i ], i );

			return true;
		}


		public bool ToOpenCV( ref Mat sensorMat, int width, int height )
		{
			if( !ValidateAspect( width, height ) ) return false;

			if( sensorMat == null || sensorMat.IsDisposed || sensorMat.rows() != 3 || sensorMat.cols() != 3 ) {
				sensorMat = Mat.eye( 3, 3, CvType.CV_64F );
			}

			// Assuming the rest of the matrix is identity.
			sensorMat.WriteValue( fx * width, 0, 0 );
			sensorMat.WriteValue( fy * height, 1, 1 );
			sensorMat.WriteValue( cx * width, 0, 2 );
			sensorMat.WriteValue( cy * height, 1, 2 );

			return true;
		}


		/*
		public void FlipY()
		{
			fy = -fy;
			cy = 1 - cy;
		}
		*/


		bool ValidateAspect( int width, int height )
		{
			double desiredAspect = width / (double) height;
			if( System.Math.Abs( desiredAspect - aspect ) > 0.0001f ) {
				Debug.LogError( logPrepend + "Conversion failed. Aspects must match.\n" + "Has aspect: " + aspect.ToString( "F4" ) + ". Wants aspect: " + desiredAspect.ToString( "F4" ) );
				return false;
			}
			return true;
		}


		public override string ToString()
		{
			return "(cx,cy,fx,fy): (" + cx + "," + cy + "," + fx + "," + fy + ") dist: (" + distortionCoeffs[0] + "," + distortionCoeffs[ 1 ] + "," + distortionCoeffs[ 2 ] + "," + distortionCoeffs[ 3 ] + "," + distortionCoeffs[ 4 ] + ")";
		}
	}
}