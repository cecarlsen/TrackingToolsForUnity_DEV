/*
	Copyright © Carl Emil Carlsen 2020
	http://cec.dk
*/

using UnityEngine;
using UnityEngine.Events;

namespace TrackingTools
{
	public class WebCameraTextureProvider : MonoBehaviour
	{
		[SerializeField] int _requestedDeviceIndex = 0;
		[SerializeField] int _requestedWidth = 1920; // Because device.availableResolutions returns null https://docs.unity3d.com/2019.3/Documentation/ScriptReference/WebCamDevice-availableResolutions.html
		[SerializeField] int _requestedHeight = 1080;
		[SerializeField] int _requestedFrameRate = 30;
		[SerializeField] bool _logStatus = true;
		[SerializeField] UnityEvent<Texture> _webCamTextureEvent = null;
		[SerializeField] UnityEvent<float> _webCamAspectEvent = null;

		WebCamTexture _webCamTexture;

		static readonly string logPrepend = "<b>[" + nameof( WebCameraTextureProvider ) + "]</b> ";

		
		void Start()
		{
			if( WebCamTexture.devices.Length == 0 )
			{
				Debug.LogWarning( "No cameras found.\n" );
				Destroy( this );
				return;
			}

			if( _requestedDeviceIndex > WebCamTexture.devices.Length-1 ) _requestedDeviceIndex = 0;
			WebCamDevice device = WebCamTexture.devices[ _requestedDeviceIndex ];

			_webCamTexture = new WebCamTexture( device.name, _requestedWidth, _requestedHeight, _requestedFrameRate );
			_webCamTexture.name = device.name;
			_webCamTexture.Play();

			if( _logStatus ) Debug.Log( logPrepend + "Started " + _webCamTexture.width + "x" + _webCamTexture.height + "\n" );

			_webCamAspectEvent.Invoke( _webCamTexture.width / (float) +_webCamTexture.height );
		}


		void OnDestroy()
		{
			_webCamTexture.Stop();
			Destroy( _webCamTexture );
		}


		void Update()
		{
			if( _webCamTexture.didUpdateThisFrame ) _webCamTextureEvent.Invoke( _webCamTexture );
		}
	}

}
