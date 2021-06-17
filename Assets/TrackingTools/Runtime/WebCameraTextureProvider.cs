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
		[SerializeField,ReadOnlyAtRuntime] int _requestedDeviceIndex = 0;
		[SerializeField,ReadOnlyAtRuntime] int _requestedWidth = 1920; // Because device.availableResolutions returns null https://docs.unity3d.com/2019.3/Documentation/ScriptReference/WebCamDevice-availableResolutions.html
		[SerializeField,ReadOnlyAtRuntime] int _requestedHeight = 1080;
		[SerializeField,ReadOnlyAtRuntime] int _requestedFrameRate = 30;

		[Header("Undistortion")]
		[SerializeField,ReadOnlyAtRuntime] bool _undistortionEnabled = false;
		[SerializeField, ReadOnlyAtRuntime, Tooltip("Needed for undistortion")] string _intrinsicsFileName = "";

		[Header("Debug")]
		[SerializeField] bool _logStatus = true;

		[Header("Output")]
		[SerializeField] UnityEvent<Texture> _webCamTextureEvent = null;
		[SerializeField] UnityEvent<RenderTexture> _undistortedWebCamTextureEvent = null;
		[SerializeField] UnityEvent<float> _webCamAspectEvent = null;

		WebCamTexture _webCamTexture;

		Intrinsics _intrinsics;
		CameraLensUndistorterGpu _lensUndistorter;

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

			if( _undistortionEnabled && !Intrinsics.TryLoadFromFile( _intrinsicsFileName, out _intrinsics ) ) {
				Debug.LogError( "Intrinsics file '" + _intrinsicsFileName + "' not found.\n" );
				_undistortionEnabled = false;
			} else {
				_lensUndistorter = new CameraLensUndistorterGpu();
			}

			if( _logStatus ) Debug.Log( logPrepend + "Started " + _webCamTexture.width + "x" + _webCamTexture.height + "\n" );

			_webCamAspectEvent.Invoke( _webCamTexture.width / (float) +_webCamTexture.height );
		}


		void OnDestroy()
		{
			_webCamTexture.Stop();
			Destroy( _webCamTexture );
			if( _lensUndistorter != null ) _lensUndistorter.Release();
		}


		void Update()
		{
			if( !_webCamTexture || !_webCamTexture.didUpdateThisFrame ) return;
			
			if( _undistortionEnabled && _lensUndistorter != null ) {
				_lensUndistorter.Update( _webCamTexture, _intrinsics );
				_undistortedWebCamTextureEvent.Invoke( _lensUndistorter.undistortedTexture );
			}
			
			_webCamTextureEvent.Invoke( _webCamTexture );
		}
	}

}
