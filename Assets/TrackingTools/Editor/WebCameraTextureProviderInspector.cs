/*
	Copyright © Carl Emil Carlsen 2020-2021
	http://cec.dk
*/

using UnityEngine;
using UnityEditor;

namespace TrackingTools
{
	[CustomEditor( typeof( WebCameraTextureProvider ) )]
	public class WebCameraTextureProviderInspector : Editor
	{
		SerializedProperty _requestedDeviceIndexProp;
		SerializedProperty _requestedWidthProp;
		SerializedProperty _requestedHeightProp;
		SerializedProperty _requestedFrameRateProp;
		SerializedProperty _logStatusProp;
		SerializedProperty _webCamTextureEventProp;
		SerializedProperty _webCamAspectEvent;

		string[] _displayOptions;

		const string cameraSelectionLabel = "Camera";
		const string cameraNotFoundText = "Camera not found";


		void OnEnable()
		{
			_requestedDeviceIndexProp = serializedObject.FindProperty( "_requestedDeviceIndex" );
			_requestedWidthProp = serializedObject.FindProperty( "_requestedWidth" );
			_requestedHeightProp = serializedObject.FindProperty( "_requestedHeight" );
			_requestedFrameRateProp = serializedObject.FindProperty( "_requestedFrameRate" );
			_logStatusProp = serializedObject.FindProperty( "_logStatus" );
			_webCamTextureEventProp = serializedObject.FindProperty( "_webCamTextureEvent" );
			_webCamAspectEvent = serializedObject.FindProperty( "_webCamAspectEvent" );
		}


		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			WebCamDevice[] devices = WebCamTexture.devices;
			int deviceOptionCount = Mathf.Max( devices.Length, _requestedDeviceIndexProp.intValue + 1 );
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

			_requestedDeviceIndexProp.intValue = EditorGUILayout.Popup( cameraSelectionLabel, _requestedDeviceIndexProp.intValue, _displayOptions );
			EditorGUILayout.PropertyField( _requestedWidthProp );
			EditorGUILayout.PropertyField( _requestedHeightProp );
			EditorGUILayout.PropertyField( _requestedFrameRateProp );

			EditorGUILayout.PropertyField( _logStatusProp );

			EditorGUILayout.PropertyField( _webCamTextureEventProp );
			EditorGUILayout.PropertyField( _webCamAspectEvent );

			serializedObject.ApplyModifiedProperties();
		}
	}

}