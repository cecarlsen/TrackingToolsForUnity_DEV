/*
	Copyright © Carl Emil Carlsen 2020
	http://cec.dk
*/

using UnityEngine;
using OpenCVForUnity.CoreModule;

namespace TrackingTools
{
	public static class MatExtensions
	{
		static double[] _temp1d = new double[ 1 ];
		static float[] _temp1f = new float[ 1 ];


		/// <summary>
		/// Helper to read a value from a Mat (without garabge!).
		/// </summary>
		public static double ReadValue( this Mat mat, int ny, int nx )
		{
			switch( mat.depth() ) {
				case CvType.CV_64F:
					mat.get( ny, nx, _temp1d );
					return _temp1d[ 0 ];
				case CvType.CV_32F:
					mat.get( ny, nx, _temp1f );
					return _temp1f[ 0 ];
			}

			return 0;
		}


		/// <summary>
		/// Helper to write a value to a Mat (without garbage!).
		/// </summary>
		public static void WriteValue( this Mat mat, double value, int ny, int nx )
		{
			switch( mat.depth() ) {
				case CvType.CV_64F:
					_temp1d[ 0 ] = value;
					mat.put( ny, nx, _temp1d );
					break;
				case CvType.CV_32F:
					_temp1f[ 0 ] = (float) value;
					mat.put( ny, nx, _temp1f );
					break;
			}
		}


		public static Vector3 ReadVector3( this Mat vectorMat )
		{
			Vector3 vector = Vector3.zero;
			vector.x = (float) vectorMat.ReadValue( 0, 0 );
			vector.y = (float) vectorMat.ReadValue( 1, 0 );
			vector.z = (float) vectorMat.ReadValue( 2, 0 );
			return vector;
		}


		public static void WriteVector3( this Mat vectorMat, Vector3 vector )
		{
			vectorMat.WriteValue( vector.x, 0, 0 );
			vectorMat.WriteValue( vector.y, 1, 0 );
			vectorMat.WriteValue( vector.z, 2, 0 );
		}

	}
}