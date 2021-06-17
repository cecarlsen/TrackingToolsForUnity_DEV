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
using OpenCVForUnity.CoreModule;

namespace TrackingTools
{
	[System.Serializable]
	public class Intrinsics
	{
		// We store resolution independent values in viewport space (zero at bottom-left).
		[SerializeField] double cx, cy;				// Principal point that is usually at the image center.
		[SerializeField] double fx, fy;				// Focal lengths.
		[SerializeField] double[] distortionCoeffs;	// K1, K2, K3, P1 & P2. Radial and tangential distortion coefficients.
		[SerializeField] double aspect;
		[SerializeField] double rmsError;

		static readonly string logPrepend = "<b>[" + nameof( Intrinsics ) + "]</b> ";

		public double Cx => cx;
		public double Cy => cy;
		public double Fx => fx;
		public double Fy => fy;
		public double Aspect => aspect;


		public void ApplyToCamera( Camera cam )
		{
			// Great explanation by jungguswns:
			// https://forum.unity.com/threads/how-to-use-opencv-camera-calibration-to-set-physical-camera-parameters.704120/

			// Also, about sensor size and focal lengths.
			// https://answers.opencv.org/question/139166/focal-length-from-calibration-parameters/

			float focalLength = cam.focalLength; // f can be arbitrary, as long as sensor_size is resized to to make ax,ay consistient
			cam.orthographic = false;
			cam.usePhysicalProperties = true;
			cam.sensorSize = new Vector2( (float) ( focalLength / fx ), (float) ( focalLength / fy ) );
			Vector2 lensShift = new Vector2( (float) ( ( 0.5 - cx ) / 1.0 ), (float) ( ( 0.5 - cy ) / 1.0 ) );
			//if( flippedLensShiftY ) lensShift.y *= -1;
			cam.lensShift = lensShift;
			cam.gateFit = Camera.GateFitMode.None;
		}


		public Texture2D CreateUndistortLutTexture( int width, int height )
		{
			Texture2D texture = new Texture2D( width, height, TextureFormat.RGFloat, false, linear: true );
			texture.name = "LensUndistortionLut";

			Vector2[] pixelData = new Vector2[ width * height ];
			Vector2 uv = new Vector2();
			for( int ny = 0; ny < height; ny++ ) {
				uv.y = ny / (float) height;
				for( int nx = 0; nx < width; nx++ ) {
					uv.x = nx / (float) width;
					pixelData[ ny * width + nx ] = Distort( uv );
				}
			}
			texture.SetPixelData( pixelData, 0 );
			texture.Apply();

			return texture;
		}


		// http://alumni.media.mit.edu/~sbeck/results/Distortion/distortion.html
		Vector2 Distort( Vector2 uv )
		{
			double k1 = distortionCoeffs[ 0 ];
			double k2 = distortionCoeffs[ 1 ];
			double k3 = distortionCoeffs[ 2 ];
			//double p1 = distortionCoeffs[ 3 ] / 720f; // are these normalized?
			//double p2 = distortionCoeffs[ 4 ] / 1280f;

			// To center.
			double x = ( uv.x - cx ) / fx / aspect;
			double y = ( uv.y - cy ) / fy;

			double r2 = x*x + y*y;

			double xd = x;
			double yd = y;

			// Radial distortion
			xd += x * ( k1*r2 + k2*r2*r2 + k3*r2*r2*r2 );
			yd += y * ( k1*r2 + k2*r2*r2 + k3*r2*r2*r2 );

			// Tangential distortion
			//xd += p1 * (r2 + 2*x*x) + 2*p2*x*y;
			//yd += p2 * (r2 + 2*y*y) + 2*p1*x*y;

			// Back to 0,0.
			xd = xd * fx * aspect + cx;
			yd = yd * fy + cy;

			return new Vector2( (float) xd, (float) yd );
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
			if( this.distortionCoeffs == null || this.distortionCoeffs.Length != distortionCoeffs.Length ) this.distortionCoeffs = new double[ distortionCoeffs.Length  ];
			System.Array.Copy( distortionCoeffs, 0, this.distortionCoeffs, 0, distortionCoeffs.Length );
			this.aspect = aspect;
			this.rmsError = rmsError;
		}


		public void UpdateFromOpenCV( Mat sensorMat, MatOfDouble distCoeffsMat, int width, int height, float rmsError )
		{
			if( distortionCoeffs == null || distCoeffsMat.IsDisposed || distortionCoeffs.Length != distCoeffsMat.total() ){
				distortionCoeffs = new double[ distCoeffsMat.total() ];
			}

			fx = sensorMat.ReadValue( 0, 0 ) / (double) width;
			fy = sensorMat.ReadValue( 1, 1 ) / (double) height;
			cx = sensorMat.ReadValue( 0, 2 ) / (double) width;
			cy = sensorMat.ReadValue( 1, 2 ) / (double) height;
			aspect = width / (double) height;
			this.rmsError = rmsError;

			for( int i = 0; i < distortionCoeffs.Length; i++ ) {
				distortionCoeffs[i] = distCoeffsMat.ReadValue( i );
			}
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