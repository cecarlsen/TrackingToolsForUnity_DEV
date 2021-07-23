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
		Material _material;
		RenderTexture _undistortedTexture;

		public RenderTexture undistortedTexture => _undistortedTexture;
		public Texture2D undistortLut => _undistortLut;


		static class ShaderIDs
		{
			public static readonly int undistortLutRead = Shader.PropertyToID( "_UndistortLutRead" );
			public static readonly int resolution = Shader.PropertyToID( "_Resolution" );
		}


		public CameraLensUndistorterGpu()
		{
			_material = new Material( Shader.Find( "Hidden/" + nameof( CameraLensUndistorterGpu ) ) );
		}


		public RenderTexture Update( Texture texture, Intrinsics intrinsics )
		{
			int w = texture.width;
			int h = texture.height;

			if( !_undistortLut || _undistortLut.width != w || _undistortLut.height != h || intrinsics != _intrinsics )
			{
				_intrinsics = intrinsics;
				_undistortLut = intrinsics.CreateUndistortRectifyTexture( w, h );
			}

			if( !_undistortedTexture || _undistortedTexture.width != w || _undistortedTexture.height != h || _undistortedTexture.graphicsFormat != texture.graphicsFormat )
			{
				if( _undistortedTexture ) _undistortedTexture.Release();
				_undistortedTexture = new RenderTexture( w, h, 0, texture.graphicsFormat, 0 );
				_undistortedTexture.useMipMap = false;
				_undistortedTexture.autoGenerateMips = false;
				_undistortedTexture.wrapMode = texture.wrapMode;
				_undistortedTexture.name = "UndistortedTexture";
			}

			_material.SetTexture( ShaderIDs.undistortLutRead, _undistortLut );
			_material.SetVector( ShaderIDs.resolution, new Vector2( w, h ) );
			Graphics.Blit( texture, _undistortedTexture, _material );

			return _undistortedTexture;
		}


		public void Release()
		{
			if( _undistortedTexture ) _undistortedTexture.Release();
		}
	}
}