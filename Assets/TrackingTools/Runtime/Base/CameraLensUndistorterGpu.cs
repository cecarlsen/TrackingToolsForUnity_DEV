/*
	Copyright © Carl Emil Carlsen 2021
	http://cec.dk

	GPU version of Calib3d.undistort().
*/

using UnityEngine;

namespace TrackingTools
{
	public class CameraLensUndistorterGpu
	{
		Intrinsics _intrinsics;
		Texture2D _undistortLut;
		ComputeShader _computeShader;
		RenderTexture _undistortedTexture;

		Vector2Int _threadGroupCount;

		const int threadGroudWidth = 8; // Must match define in compute shader.

		public RenderTexture undistortedTexture => _undistortedTexture;


		static class ShaderIDs
		{
			public static readonly int sourceTextureRead = Shader.PropertyToID( "_SourceTextureRead" );
			public static readonly int resolution = Shader.PropertyToID( "_Resolution" );
		}


		public RenderTexture Update( Texture texture, Intrinsics intrinsics )
		{
			int w = texture.width;
			int h = texture.height;

			if( !_undistortLut || _undistortLut.width != w || _undistortLut.height != h || intrinsics != _intrinsics )
			{
				_intrinsics = intrinsics;
				_undistortLut = intrinsics.CreateUndistortLutTexture( w, h );
				
				if( _undistortedTexture ) _undistortedTexture.Release();
				_undistortedTexture = new RenderTexture( w, h, 0, texture.graphicsFormat, 0 );
				_undistortedTexture.useMipMap = false;
				_undistortedTexture.autoGenerateMips = false;
				_undistortedTexture.enableRandomWrite = true;
				_undistortedTexture.name = "UndistortedTexture";

				_computeShader = Object.Instantiate( Resources.Load<ComputeShader>( nameof( CameraLensUndistorterGpu ) ) );
				_computeShader.hideFlags = HideFlags.HideAndDontSave;
				_computeShader.SetTexture( 0, "_UndistortLutRead", _undistortLut );
				_computeShader.SetTexture( 0, "_UndistortedTexture", _undistortedTexture );
				_computeShader.SetInts( ShaderIDs.resolution, new int[]{ w, h } );

				_threadGroupCount = new Vector2Int( Mathf.CeilToInt( w / (float) threadGroudWidth ), Mathf.CeilToInt( h / (float) threadGroudWidth ) );
			}

			_computeShader.SetTexture( 0, ShaderIDs.sourceTextureRead, texture );
			_computeShader.Dispatch( 0, _threadGroupCount.x, _threadGroupCount.y, 1 );

			return _undistortedTexture;
		}


		public void Release()
		{
			if( _undistortedTexture ) _undistortedTexture.Release();
		}
	}
}