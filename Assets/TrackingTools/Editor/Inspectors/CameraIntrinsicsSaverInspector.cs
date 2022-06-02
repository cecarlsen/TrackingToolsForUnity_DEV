/*
	Copyright © Carl Emil Carlsen 2022
	http://cec.dk
*/

using UnityEngine;
using UnityEditor;

namespace TrackingTools
{
	[CustomEditor(typeof(CameraIntrinsicsSaver))]
	public class CameraIntrinsicsSaverInspector : Editor
	{
		CameraIntrinsicsSaver _component;


		void OnEnable()
		{
			_component = target as CameraIntrinsicsSaver;
		}


		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			DrawDefaultInspector();

			EditorGUILayout.Space();

			if( GUILayout.Button( "Save" ) ) _component.Save();

			serializedObject.ApplyModifiedProperties();
		}
	}
}