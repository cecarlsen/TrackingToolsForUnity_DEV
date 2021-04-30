/*
	Copyright © Carl Emil Carlsen 2020
	http://cec.dk
*/

using System;
using UnityEngine;
using UnityEngine.Events;
using com.rfilkov.kinect;
using UnityEngine.Experimental.Rendering;

public class KinectAzureTexture2DProvider : MonoBehaviour
{
	[SerializeField] int _sensorId = 0;
	[SerializeField] UnityEvent<Texture2D> _irTexture2DEvent = null;
	[SerializeField] UnityEvent<Texture2D> _colorTexture2DEvent = null;

	Texture2D _irTexture;
	byte[] _rawImageDataBytes;

	ulong _lastIRFrameTime;
	ulong _lastColorFrameTime;


	void Update()
	{
		KinectManager kinectManager = KinectManager.Instance;
		if( kinectManager && kinectManager.IsInitialized() )
		{
			KinectInterop.SensorData sensorData = kinectManager.GetSensorData( _sensorId );

			// Azure Kinect Examples unpacks IR on the GPU into a RenderTexture. We need a Texture2D.
			if( _irTexture2DEvent != null ) ConvertAndOutputIRTexture2D( kinectManager, sensorData );

			// The color texture is already a Texture2D, so we just output it.
			if( _colorTexture2DEvent != null && sensorData.lastColorFrameTime != _lastColorFrameTime ) {
				Texture colorTexture = kinectManager.GetColorImageTex( _sensorId ) as Texture;
				colorTexture.wrapMode = TextureWrapMode.Repeat;
				if( colorTexture ){
					if( string.IsNullOrEmpty( colorTexture.name ) ) colorTexture.name = "KinectColor";
					_colorTexture2DEvent.Invoke( colorTexture as Texture2D );
				}
				_lastColorFrameTime = sensorData.lastColorFrameTime;
			}
		}
	}


	void ConvertAndOutputIRTexture2D( KinectManager kinectManager, KinectInterop.SensorData sensorData )
	{
		if( sensorData.lastInfraredFrameTime != _lastIRFrameTime )
		{
			// Create texture.
			if( !_irTexture ) {
				int width = sensorData.depthImageWidth;
				int height = sensorData.depthImageHeight;
				int pixelCount = width * height;
				_irTexture = new Texture2D( width, height, GraphicsFormat.R16_UNorm, TextureCreationFlags.None );
				_irTexture.name = "KinectIR";
				_rawImageDataBytes = new byte[ pixelCount * 2 ];
			}

			// Get raw image data.
			ushort[] rawImageData = kinectManager.GetRawInfraredMap( _sensorId );

			// ushort[] to byte[].
			// https://stackoverflow.com/questions/37213819/convert-ushort-into-byte-and-back
			Buffer.BlockCopy( rawImageData, 0, _rawImageDataBytes, 0, rawImageData.Length * 2 );

			// Load into texture.
			_irTexture.LoadRawTextureData( _rawImageDataBytes );
			_irTexture.Apply();

			// Output.
			_irTexture2DEvent.Invoke( _irTexture );

			// Store time.
			_lastIRFrameTime = sensorData.lastInfraredFrameTime;
		}
	}


	void OnDestroy()
	{
		if( _irTexture ) Destroy( _irTexture );
	}
}