/*
	Copyright © Carl Emil Carlsen 2020
	http://cec.dk
*/

using UnityEngine;
using UnityEditor;

namespace TrackingTools
{
	[CustomPropertyDrawer( typeof( LayerAttribute ) )]
	public class LayerAttributeDrawer : PropertyDrawer
	{
		public override void OnGUI( Rect position, SerializedProperty property, GUIContent label )
		{
			property.intValue = EditorGUI.LayerField( position, label, property.intValue );
		}

	}
}