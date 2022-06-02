/*
	Copyright © Carl Emil Carlsen 2020-2022
	http://cec.dk
*/

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.Calib3dModule;
using OpenCVForUnity.UnityUtils;

namespace TrackingTools
{
	public static class TrackingToolsHelper
	{
		static readonly string logPrepend = "<b>[" + nameof( TrackingToolsHelper ) + "]</b> ";

		static Size _tempSize;
		static double[] _temp1d = new double[ 1 ];
		static double[] _temp2d = new double[ 2 ];
		static double[] _temp3d = new double[ 3 ];
		static float[] _temp1f = new float[ 1 ];
		static float[] _temp2f = new float[ 2 ];
		static float[] _temp3f = new float[ 3 ];


		[System.Serializable]
		public enum PatternType
		{
			Checkerboard,
			CircleGrid,
			AsymmetricCircleGrid
		}


		public static string GetIntrinsicsFilePath( string fileNameWithoutExtension )
		{
			return TrackingToolsConstants.intrinsicsDirectoryPath + "/" + fileNameWithoutExtension + ".json";
		}


		public static string GetExtrinsicsFilePath( string fileNameWithoutExtension )
		{
			return TrackingToolsConstants.extrinsicsDirectoryPath + "/" + fileNameWithoutExtension + ".json";
		}


		public static void TextureToMat( Texture texture, bool flipTexture, ref Mat mat, ref Color32[] tempTransferColors, ref Texture2D tempTransferTexture )
		{
			if( mat == null ) mat = GetCompatibleMat( texture );

			int w = texture.width;
			int h = texture.height;
			int pxCount = h*w;

			if( texture is WebCamTexture ) {
				WebCamTexture webCamTex = texture as WebCamTexture;
				//if( !webCamTex.didUpdateThisFrame ) return; // We can't rely on this field, it seems to not be updated (2020.1)
				if( tempTransferColors == null || tempTransferColors.Length != pxCount ) tempTransferColors = new Color32[ pxCount ];
				Utils.webCamTextureToMat( webCamTex, mat, tempTransferColors, flipTexture );
			} else if( texture is Texture2D ) {
				Utils.fastTexture2DToMat( texture as Texture2D, mat, flipTexture );
			} else if( texture is RenderTexture ) {
				RenderTexture renderCamTex = texture as RenderTexture;
				if( tempTransferTexture == null || tempTransferTexture.width != w || tempTransferTexture.height != h ) {
					tempTransferTexture = new Texture2D( w, h, renderCamTex.graphicsFormat, TextureCreationFlags.None );
				}
				RenderTexture.active = renderCamTex;
				tempTransferTexture.ReadPixels( new UnityEngine.Rect( 0, 0, w, h ), 0, 0 );
				RenderTexture.active = null;
				Utils.fastTexture2DToMat( tempTransferTexture, mat, flipTexture );
			} else {
				Debug.LogWarning( logPrepend + "TODO: implement for texture type.\n" + texture );
				return;
			}
		}


		public static void ColorMatToLumanceMat( Mat colorTexMat, Mat grayTexMat )
		{
			bool isCameraTexture16Bit = colorTexMat.depth() == CvType.CV_16U;
			if( colorTexMat.channels() > 1 ) {
				Imgproc.cvtColor( colorTexMat, grayTexMat, Imgproc.COLOR_BGR2GRAY );
				if( isCameraTexture16Bit ) {
					// TODO 16bit to 8bit convertion case.
					Debug.LogWarning( logPrepend + "Handling of 16bit RGBA textures to be implemented...\n" );
					return;
				}
			} else {
				if( isCameraTexture16Bit ) {
					colorTexMat.convertTo( grayTexMat, CvType.CV_8U, 1 / 256.0 );
				} else {
					colorTexMat.copyTo( grayTexMat );
				}
			}
		}


		public static Mat GetCompatibleMat( Texture texture )
		{
			int camTexCvType;
			Scalar defaultValue;
			switch( texture.graphicsFormat ) {
				case GraphicsFormat.R8_UNorm:
					camTexCvType = CvType.CV_8UC1;
					defaultValue = new Scalar( 1 );
					break;
				case GraphicsFormat.R16_UNorm:
					camTexCvType = CvType.CV_16UC1;
					defaultValue = new Scalar( 1 );
					break;
				default:
					camTexCvType = CvType.CV_8UC4;
					defaultValue = new Scalar( 0, 0, 0, 255 );
					break;
			}
			return new Mat( texture.height, texture.width, camTexCvType, defaultValue );
		}

		/// <summary>
		/// Creates a collection of points constituting a pattern for recognition. Measured in millimeters.
		/// In real space, y increases upwards. Since the first point is at upper-left corner, y starts high and decrease.
		/// </summary>
		/// <param name="patternSize">For chessbord, count the inner corners</param>
		/// <param name="tileSize">Measured in millimeters</param>
		/// <param name="patternType">OpenCv supported pattern type</param>
		/// <returns>Points in real space (mm)</returns>
		public static MatOfPoint3f CreateRealModelPatternPoints( Vector2Int patternSize, int tileSize, PatternType patternType )
		{
			bool isAsym = patternType == PatternType.AsymmetricCircleGrid;

			int cornerCount = patternSize.y * patternSize.x;
			Vector2Int boardSize = patternSize * tileSize;
			MatOfPoint3f chessCornersModelSpace = new MatOfPoint3f();
			chessCornersModelSpace.alloc( cornerCount );
			int c = 0;
			_temp3d[2] = 0;
			for( int y = 0; y < patternSize.y; y++ ) {
				for( int x = 0; x < patternSize.x; x++, c++ ) {
					_temp3d[ 0 ] = x * tileSize + ( isAsym && y%2==1 ? tileSize * 0.5 : 0 );
					_temp3d[ 1 ] = boardSize.y - y * tileSize * ( isAsym ? 0.5f : 1f );
					chessCornersModelSpace.put( c, 0, _temp3d );
				}
			}
			return chessCornersModelSpace;
		}


		/// <summary>
		/// Updates a collection of points constituting a pattern for recognition. Zero point is at the center.
		/// </summary>
		/// <param name="patternSize">For chessbord, count the inner corners</param>
		/// <param name="transform">The transform must be scaled to fit the aspect of the pattern</param>
		/// <param name="patternType">OpenCV supported pattern type</param>
		/// <param name="patternPointsWorldSpace">Point collection</param>
		public static void UpdateWorldSpacePatternPoints( Vector2Int patternSize, Matrix4x4 patternToWorldMatrix, PatternType patternType, Vector2 patternBorderSizeUV, ref MatOfPoint3f pointsWorldSpace )
		{
			bool isAsym = patternType == PatternType.AsymmetricCircleGrid;

			// Instantiate array.
			int cornerCount = patternSize.y * patternSize.x;
			if( pointsWorldSpace == null || pointsWorldSpace.rows() != cornerCount ){
				pointsWorldSpace = new MatOfPoint3f();
				pointsWorldSpace.alloc( cornerCount );
			}
			
			// Fill.
			int c = 0;
			Vector2 size = Vector2.one - patternBorderSizeUV * 2;
			Vector2 step = new Vector2( size.x / ( patternSize.x - 1f + ( isAsym ? 0.5f : 0 ) ), size.y / ( patternSize.y - 1f ) );
			for( int ny = 0; ny < patternSize.y; ny++ )
			{
				float y = 1 - patternBorderSizeUV.y - ny * step.y - 0.5f;
				for( int nx = 0; nx < patternSize.x; nx++, c++ )
				{
					float x = patternBorderSizeUV.x + nx * step.x - 0.5f;
					Vector3 point = new Vector3( x, y, 0 );
					if( isAsym && ny % 2 == 1 ) point.x += step.x * 0.5f;
					point = patternToWorldMatrix.MultiplyPoint3x4( point );
					pointsWorldSpace.WriteVector3( point, c );
				}
			}
		}


		public static bool FindChessboardCorners( Mat grayTexMat, Vector2Int innerCornerCount, ref MatOfPoint2f cornerPoints, bool fastAndImprecise = false, bool hasMarker = false )
		{
			Vector2IntToSize( innerCornerCount, ref _tempSize );
			if( cornerPoints == null ) cornerPoints = new MatOfPoint2f();

			bool success;
			//if( fastAndImprecise )
			//{
				//Just give me fast.
				//const int flags = 
				//	Calib3d.CALIB_CB_FAST_CHECK;// |
					//Calib3d.CALIB_CB_NORMALIZE_IMAGE |
					//Calib3d.CALIB_CB_FILTER_QUADS |
					//Calib3d.CALIB_CB_ADAPTIVE_THRESH;
				//success = Calib3d.findChessboardCorners( grayTexMat, _tempSize, cornerPoints, flags );

				// Because the old version prefers to start at black tile corner vs findChessboardCornersSB prefering to start
				// at white tile corner, we have to reverse the order of points for the old version to match up.
				//if( success ) ReverseOrder( cornerPoints );
			
			//} else {
				// FindChessboardCornersSB() is supposed to work better than combined findChessboardCorners() and cornerSubPix().
				int flagsSB = 0;
				if( !fastAndImprecise ){
					flagsSB |= Calib3d.CALIB_CB_EXHAUSTIVE; // Run an exhaustive search to improve detection rate. (Note: this seems to have very positive impact).
					flagsSB |= Calib3d.CALIB_CB_ACCURACY; // Up sample input image to improve sub-pixel accuracy due to aliasing effects. This should be used if an accurate camera calibration is required.
				}
				if( hasMarker ){
					flagsSB |= Calib3d.CALIB_CB_MARKER; // The detected pattern must have a marker
					flagsSB |= Calib3d.CALIB_CB_LARGER; // The detected pattern is allowed to be larger than patternSize.
				}
				// Calib3d.CALIB_CB_NORMALIZE_IMAGE;	// Normalize the image gamma with equalizeHist before detection
			
				success = Calib3d.findChessboardCornersSB( grayTexMat, _tempSize, cornerPoints, flagsSB );
			//}
			
			return success;
		}


		public static void ReverseOrder( MatOfPoint2f points )
		{
			int count = points.rows();
			for( int i = 0; i < count / 2; i++ ) {
				Vector2 vec2 = points.ReadVector2( i );
				int i2 = count - i - 1;
				points.WriteVector2( points.ReadVector2( i2 ), i );
				points.WriteVector2( vec2, i2 );
			}
		}


		public static bool FindAsymmetricCirclesGrid( Mat grayTexMat,  Vector2Int patternSize, ref MatOfPoint2f centerPoints )
		{
			Vector2IntToSize( patternSize, ref _tempSize );
			if( centerPoints == null ) centerPoints = new MatOfPoint2f();

			return Calib3d.findCirclesGrid( grayTexMat, _tempSize, centerPoints, Calib3d.CALIB_CB_ASYMMETRIC_GRID );
		}


		public static void DrawFoundPattern( Mat grayTexMat, Vector2Int patternSize, MatOfPoint2f points )
		{
			Vector2IntToSize( patternSize, ref _tempSize );
			Calib3d.drawChessboardCorners( grayTexMat, _tempSize, points, true );
		}


		/*
		public static void ApplyPose( Mat rotationVectorMat, Mat translationVectorMat, Transform transform, bool inverse = false )
		{
			Vector3 translation = translationVectorMat.ReadVector3();
			Vector3 rotationVector = rotationVectorMat.ReadVector3();

			if( inverse ){
				translation *= -1;
				rotationVector *= -1;
			}

			Quaternion rotation = Quaternion.AngleAxis( rotationVector.magnitude * Mathf.Rad2Deg, -rotationVector );
			if( inverse ) translation = rotation * translation;

			transform.SetPositionAndRotation( translation, rotation );
		}
		*/


		/// <summary>
		/// Returns a pattern size that is ensured to not have rotational symmetry (if not PatternType.CircleGrid). 
		/// The tile size is rounded down. Smallest acceptable pattern is 3x4. It will not allow square sizes.
		/// </summary>
		/// <returns>The corrected pattern size</returns>
		public static Vector2Int GetClosestValidPatternSize( Vector2Int patternSize, PatternType patternType )
		{
			int patternSizeMin = patternType == PatternType.Checkerboard ? 3 : 2;

			if( patternSize.x < 3 ) patternSize.x = patternSizeMin;
			if( patternSize.y < 3 ) patternSize.y = patternSizeMin;

			switch( patternType )
			{
				case PatternType.CircleGrid:
					if( patternSize.x == patternSize.y ) {
						if( patternSize.y == patternSizeMin ) patternSize.y++;
						else patternSize.y--;
					}
					break;

				case PatternType.Checkerboard:
					if( patternSize.x % 2 == 0 && patternSize.y % 2 == 0 ) {
						if( patternSize.y == patternSizeMin ) patternSize.y++;
						else patternSize.y--;
					}
					break;

				case PatternType.AsymmetricCircleGrid:
					if( patternSize.y % 2 == 0 ) {
						if( patternSize.y == patternSizeMin ) patternSize.y++;
						else patternSize.y--;
					}
					break;
			}
			
			return patternSize;
		}


		/// <summary>
		/// Generate a OpenCV compatible pattern for recognition.
		/// </summary>
		/// <param name="patternSize"></param>
		/// <param name="patternType"></param>
		/// <param name="resolution"></param>
		/// <param name="renderTexture"></param>
		/// <param name="material"></param>
		/// <param name="border">Relative to tile size</param>
		/// <param name="invert"></param>
		/// <returns>Border size (uv space)</returns>
		public static Vector2 RenderPattern( Vector2Int patternSize, PatternType patternType, int resolutionMax, ref RenderTexture renderTexture, ref Material material, float border = 0, bool invert = false )
		{
			// Sanitize.
			if( border < 0 ) border = 0;
			int patternSizeMin = patternType == PatternType.Checkerboard ? 3 : 2;
			if( patternSize.x < patternSizeMin ) patternSize.x = patternSizeMin;
			if( patternSize.y < patternSizeMin ) patternSize.y = patternSizeMin;

			// Compute placement.
			bool isAsym = patternType == PatternType.AsymmetricCircleGrid;
			Vector2Int tileCount = patternSize - Vector2Int.one;
			Vector2 step;
			if( isAsym ) step = new Vector2( 1 / ( tileCount.x + 0.5f ), 1 / (float) tileCount.y );
			else step = new Vector2( 1 / (float) tileCount.x, 1 / (float) tileCount.y );
			step.x = step.x / ( 1 + step.x * border * 2 );
			step.y = step.y / ( 1 + step.y * border * 2 * ( isAsym ? 2 : 1) );
			Vector2 tilCountWithBorders = tileCount + Vector2.one * ( border * 2 );
			float textureAspect;
			if( isAsym ) textureAspect = (tilCountWithBorders.x + 0.5f ) / ( tilCountWithBorders.y - tileCount.y*0.5f );
			else textureAspect = tilCountWithBorders.x / tilCountWithBorders.y;
			Vector2 textureProportion = textureAspect > 1 ? new Vector2( 1, 1 / textureAspect ) : new Vector2( textureAspect, 1 );
			Vector2Int resolution = new Vector2Int( Mathf.RoundToInt( textureProportion.x * resolutionMax ), Mathf.RoundToInt( textureProportion.y * resolutionMax ) );
			Vector2 zero = new Vector2( step.x * border, 1 - step.y * border );
			if( isAsym ) zero.y = 1 - step.y * border * 2;

			// Ensure resources.
			if( renderTexture == null || renderTexture.width != resolution.x || renderTexture.height != resolution.y ) {
				if( renderTexture ) renderTexture.Release();
				renderTexture = new RenderTexture( resolution.x, resolution.y, 16, GraphicsFormat.R8G8B8A8_UNorm );
				renderTexture.Create();
				renderTexture.name = patternType.ToString();
				renderTexture.wrapMode = TextureWrapMode.Repeat;
			}
			if( !material ) material = new Material( Shader.Find( "Hidden/UnlitColor" ) );

			// Setup.
			Graphics.SetRenderTarget( renderTexture );
			GL.modelview = Matrix4x4.identity;
			GL.LoadOrtho();
			GL.Clear( true, true, invert ? Color.black : Color.white );

			// Render.
			material.color = invert ? Color.white : Color.black;
			material.SetPass( 0 );
			switch( patternType )
			{
				case PatternType.Checkerboard:
				GL.Begin( GL.QUADS );
				for( int ny = 0; ny < tileCount.y; ny++ ) {
					float y = zero.y - ny * step.y;
					for( int nx = 0; nx < tileCount.x; nx++ ) {
						if( ( nx + ( ny % 2 == 0 ? 1 : 0 ) ) % 2 == 1 ) continue; // Upper-left corner must be white.
						float x = zero.x + nx * step.x;// - 1;
						GL.Vertex3( x, y, 0 );
						GL.Vertex3( x + step.x, y, 0 );
						GL.Vertex3( x+step.x, y - step.y, 0 );
						GL.Vertex3( x, y - step.y, 0 );
					}
				}					
				GL.End();
				break;

				case PatternType.CircleGrid:
				case PatternType.AsymmetricCircleGrid:

					// These values are set from experimentation.
					const float asymCircleSize = 0.10f;		// When too large, the points are also recognised as a chess pattern
					const float symCircleSyize = 0.08f;
					const int circleResolution = 128;

					Vector2 circleSize = new Vector2( 1 / textureProportion.x, 1 / textureProportion.y ) * ( textureAspect > 1 ? step.x : step.y*2 );
					circleSize *= isAsym ? asymCircleSize : symCircleSyize;
					for( int ny = 0; ny < patternSize.y; ny++ ) {
						float y = zero.y - ny * step.y;
						float ax = isAsym && ny % 2 == 1 ? step.x * 0.5f : 0;
						for( int nx = 0; nx < patternSize.x; nx++ ) {
							float x = zero.x + nx * step.x + ax;
							GL.Begin( GL.TRIANGLE_STRIP );
							for( int p = 0; p < circleResolution; p++ ) {
								float a = ( p / (float) circleResolution ) * Mathf.PI * 2;
								GL.Vertex3( x + Mathf.Cos( a ) * circleSize.x, y + Mathf.Sin( a ) * circleSize.y, 0 );
								GL.Vertex3( x, y, 0 );
							}
							GL.Vertex3( x + circleSize.x, y, 0 );
							GL.End();
						}
					}
				break;
			}

			// Finish.
			Graphics.SetRenderTarget( null );

			return new Vector2( zero.x, 1 - zero.y );
		}



		public static GameObject CreatePrecisionTestDots( Transform calibrationBoardTransform, int layer, Vector2Int chessPatternSize, float chessTileSizeMeters )
		{
			GameObject precisionDotsContainerObject = new GameObject( "TestDots" );
			precisionDotsContainerObject.transform.SetParent( calibrationBoardTransform );
			Transform[] dotTransforms = new Transform[ 3 ];

			Material dotMaterial = new Material( Shader.Find( "Hidden/UnlitColor" ) );
			for( int i = 0; i < dotTransforms.Length; i++ ) {
				Transform dotTransform = GameObject.CreatePrimitive( PrimitiveType.Quad ).transform;
				dotTransform.name = "Dot" + ( i + 1 );
				dotTransform.SetParent( precisionDotsContainerObject.transform );
				dotTransform.localScale = Vector3.one * TrackingToolsConstants.precisionTestDotSize;
				dotTransform.GetComponent<Renderer>().sharedMaterial = dotMaterial;
				dotTransform.gameObject.layer = layer;
				dotTransforms[ i ] = dotTransform;
			}
			float dotOffsetX = ( ( chessPatternSize.x - 1 ) * 0.5f + 1 ) * chessTileSizeMeters;
			float dotOffsetY = ( ( chessPatternSize.y - 4 ) * 0.5f + 1 ) * chessTileSizeMeters;
			dotTransforms[ 0 ].localPosition = new Vector3( dotOffsetX, dotOffsetY, 0 );
			dotTransforms[ 1 ].localPosition = new Vector3( -dotOffsetX, 0, 0 );
			dotTransforms[ 2 ].localPosition = new Vector3( dotOffsetX, -dotOffsetY, 0 );
			return precisionDotsContainerObject;
		}


		static void Vector2IntToSize( Vector2Int vec, ref Size size )
		{
			if( size == null ) {
				size = new Size( vec.x, vec.y );
			} else {
				size.width = vec.x;
				size.height = vec.y;
			}
		}
	}
}