/*
	Copyright © Carl Emil Carlsen 2022
	http://cec.dk
*/

using UnityEngine;
using UnityEditor;

namespace TrackingTools
{
	[CustomEditor(typeof(CameraIntrinsicsLoader))]
	public class CameraIntrinsicsLoaderInspector : Editor
	{
		CameraIntrinsicsLoader _component;


		void OnEnable()
		{
			_component = target as CameraIntrinsicsLoader;
		}


		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			DrawDefaultInspector();

			EditorGUILayout.Space();

			if( GUILayout.Button( "Load" ) ) _component.LoadAndApply();

			serializedObject.ApplyModifiedProperties();
		}
	}
}