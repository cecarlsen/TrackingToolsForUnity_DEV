/*
	Copyright © Carl Emil Carlsen 2022
	http://cec.dk
*/

using UnityEngine;

namespace TrackingTools
{
	[RequireComponent( typeof( Camera) ) ]
	public class CameraIntrinsicsSaver : MonoBehaviour
	{
		[SerializeField] string _intrinsicsFileName = "DefaultCamera";
		[SerializeField] bool _saveOnDestroy = true;
		[SerializeField] bool _saveOnDisable = false;

		//static string logPrepend = "<b>[" + nameof( CameraIntrinsicsLoader) + "]</b> ";


		void OnDisable()
		{
			if( _saveOnDisable ) Save();
		}

		void OnDestroy()
		{
			if( _saveOnDestroy ) Save();
		}


		public void Save()
		{
			Camera cam = GetComponent<Camera>();

			Intrinsics intrinsics = new Intrinsics();
			intrinsics.UpdateFromUnityCamera( cam );
			intrinsics.SaveToFile( _intrinsicsFileName );

			//Debug.Log( intrinsics );
		}
	}
}