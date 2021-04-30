/*
	Copyright © Carl Emil Carlsen 2020
	http://cec.dk
*/

using UnityEngine;
using OpenCVForUnity.CoreModule;

namespace TrackingTools
{
	public static class MatOfPoint2fExtensions
	{
		static double[] _temp2d = new double[ 2 ];
		static float[] _temp2f = new float[ 2 ];


		public static Vector2 ReadVector2( this MatOfPoint2f vectorArrayMat, int index )
		{
			switch( vectorArrayMat.depth() ) {
				case CvType.CV_64F:
					vectorArrayMat.get( index, 0, _temp2d );
					return new Vector2( (float) _temp2d[ 0 ], (float) _temp2d[ 1 ] );
				case CvType.CV_32F:
					vectorArrayMat.get( index, 0, _temp2f );
					return new Vector2( _temp2f[ 0 ], _temp2f[ 1 ] );
			}
			return Vector2.zero;
		}


		public static void WriteVector2( this MatOfPoint2f vectorArrayMat, Vector2 vector, int index )
		{
			switch( vectorArrayMat.depth() ) {
				case CvType.CV_64F:
					_temp2d[ 0 ] = vector.x;
					_temp2d[ 1 ] = vector.y;
					vectorArrayMat.put( index, 0, _temp2d );
					break;
				case CvType.CV_32F:
					_temp2f[ 0 ] = vector.x;
					_temp2f[ 1 ] = vector.y;
					vectorArrayMat.put( index, 0, _temp2f );
					break;
			}
		}
	}
}