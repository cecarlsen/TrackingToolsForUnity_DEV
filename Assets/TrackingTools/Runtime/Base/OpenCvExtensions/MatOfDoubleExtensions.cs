/*
	Copyright © Carl Emil Carlsen 2020
	http://cec.dk
*/

using OpenCVForUnity.CoreModule;

namespace TrackingTools
{
	public static class MatOfDoubleExtensions
	{
		static double[] _temp1d = new double[ 1 ];
		static float[] _temp1f = new float[ 1 ];


		/// <summary>
		/// Helper to read a value from a Mat (without garabge!).
		/// </summary>
		public static double ReadValue( this MatOfDouble mat, int n )
		{
			switch( mat.depth() ) {
				case CvType.CV_64F:
					mat.get( 0, n, _temp1d );
					return _temp1d[ 0 ];
				case CvType.CV_32F:
					mat.get( 0, n, _temp1f );
					return _temp1f[ 0 ];
			}

			return 0;
		}


		/// <summary>
		/// Helper to write a value to a Mat (without garbage!).
		/// </summary>
		public static void WriteValue( this MatOfDouble mat, double value, int n )
		{
			switch( mat.depth() ) {
				case CvType.CV_64F:
					_temp1d[ 0 ] = value;
					mat.put( 0, n, _temp1d );
					break;
				case CvType.CV_32F:
					_temp1f[ 0 ] = (float) value;
					mat.put( 0, n, _temp1f );
					break;
			}
		}
	}
}