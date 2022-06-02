/*
	Copyright © Carl Emil Carlsen 2020
	http://cec.dk
*/

using UnityEngine;
using UnityEditor;

namespace TrackingTools
{
	[CustomEditor( typeof( WebCameraTextureProvider ) )]
	public class WebCameraTextureProviderInspector : Editor
	{
		SerializedProperty _requestedDeviceIndex;
		SerializedProperty _requestedWidth;
		SerializedProperty _requestedHeight;
		SerializedProperty _requestedFrameRate;
		SerializedProperty _webCamTextureEvent;
		SerializedProperty _webCamAspectEvent;

		string[] _displayOptions;

		const string cameraSelectionLabel = "Camera";
		const string cameraNotFoundText = "Camera not found";


		void OnEnable()
		{
			_requestedDeviceIndex = serializedObject.FindProperty( "_requestedDeviceIndex" );
			_requestedWidth = serializedObject.FindProperty( "_requestedWidth" );
			_requestedHeight = serializedObject.FindProperty( "_requestedHeight" );
			_requestedFrameRate = serializedObject.FindProperty( "_requestedFrameRate" );
			_webCamTextureEvent = serializedObject.FindProperty( "_webCamTextureEvent" );
			_webCamAspectEvent = serializedObject.FindProperty( "_webCamAspectEvent" );
		}


		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			WebCamDevice[] devices = WebCamTexture.devices;
			int deviceOptionCount = Mathf.Max( devices.Length, _requestedDeviceIndex.intValue + 1 );
			if( _displayOptions == null || _displayOptions.Length != deviceOptionCount ) _displayOptions = new string[ deviceOptionCount ];
			for( int d = 0; d < devices.Length; d++ ) {
				WebCamDevice device = devices[d];
				_displayOptions[d] = device.name;
			}
			if( deviceOptionCount > devices.Length ) {
				for( int d = devices.Length; d < deviceOptionCount; d++ ) _displayOptions[d] = cameraNotFoundText;
			}
			
			// TODO: WebCamDevice seems to be missing availableResolutions. Docs says "Available on iOS and Android only" (Unity 2020.1).
			/*
			if( _requestedDeviceIndex.intValue < devices.Length ) {
				WebCamDevice device = devices[ _requestedDeviceIndex.intValue ];
				Debug.Log( device.availableResolutions );
				Resolution[] resolutions = device.availableResolutions;
				if( resolutions != null ) {
					for( int r = 0; r < resolutions.Length; r++ ){
						Resolution res = resolutions[ r ];
						Debug.Log( res.width + "x" + res.height + " @ " + res.width );
					}
				}
			}
			*/

			_requestedDeviceIndex.intValue = EditorGUILayout.Popup( cameraSelectionLabel, _requestedDeviceIndex.intValue, _displayOptions );
			EditorGUILayout.PropertyField( _requestedWidth );
			EditorGUILayout.PropertyField( _requestedHeight );
			EditorGUILayout.PropertyField( _requestedFrameRate );
			EditorGUILayout.PropertyField( _webCamTextureEvent );
			EditorGUILayout.PropertyField( _webCamAspectEvent );

			serializedObject.ApplyModifiedProperties();
		}
	}

}