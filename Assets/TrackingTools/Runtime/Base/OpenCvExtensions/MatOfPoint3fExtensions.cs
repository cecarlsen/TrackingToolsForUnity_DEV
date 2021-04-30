/*
	Copyright © Carl Emil Carlsen 2020
	http://cec.dk
*/

using UnityEngine;
using OpenCVForUnity.CoreModule;

namespace TrackingTools
{
	public static class MatOfPoint3fExtensions
	{
		static double[] _temp3d = new double[ 3 ];
		static float[] _temp3f = new float[ 3 ];


		public static Vector3 ReadVector3( this MatOfPoint3f vectorArrayMat, int index )
		{
			switch( vectorArrayMat.depth() ) {
				case CvType.CV_64F:
					vectorArrayMat.get( index, 0, _temp3d );
					return new Vector3( (float) _temp3d[ 0 ], (float) _temp3d[ 1 ], (float) _temp3d[ 2 ] );
				case CvType.CV_32F:
					vectorArrayMat.get( index, 0, _temp3f );
					return new Vector3( _temp3f[ 0 ], _temp3f[ 1 ], _temp3f[ 2 ] );
			}
			return Vector3.zero;
		}


		public static void WriteVector3( this MatOfPoint3f vectorArrayMat, Vector3 vector, int index )
		{
			switch( vectorArrayMat.depth() ) {
				case CvType.CV_64F:
					_temp3d[ 0 ] = vector.x;
					_temp3d[ 1 ] = vector.y;
					_temp3d[ 2 ] = vector.z;
					vectorArrayMat.put( index, 0, _temp3d );
					break;
				case CvType.CV_32F:
					_temp3f[ 0 ] = vector.x;
					_temp3f[ 1 ] = vector.y;
					_temp3f[ 2 ] = vector.z;
					vectorArrayMat.put( index, 0, _temp3f );
					break;
			}
		}
	}
}