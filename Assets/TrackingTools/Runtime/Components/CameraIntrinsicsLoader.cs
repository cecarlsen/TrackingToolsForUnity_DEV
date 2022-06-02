/*
	Copyright © Carl Emil Carlsen 2020-2022
	http://cec.dk
*/

using UnityEngine;

namespace TrackingTools
{
	[RequireComponent( typeof( Camera) ) ]
	public class CameraIntrinsicsLoader : MonoBehaviour
	{
		[SerializeField] string _intrinsicsFileName = "DefaultCamera";
		[SerializeField] bool _loadOnAwake = true;
		[SerializeField] bool _loadOnEnable = false;


		static string logPrepend = "<b>[" + nameof( CameraIntrinsicsLoader) + "]</b> ";


		void Awake()
		{
			if( _loadOnAwake ) LoadAndApply();
		}


		void OnEnable()
		{
			if( _loadOnEnable ) LoadAndApply();
		}


		public void LoadAndApply()
		{
			Intrinsics intrinsics;
			if( !Intrinsics.TryLoadFromFile( _intrinsicsFileName, out intrinsics ) ){
				Debug.LogError( logPrepend + "Intrinsics file '" + _intrinsicsFileName + "' does not exist.\n" );
				return;
			}

			Camera cam = GetComponent<Camera>();
			intrinsics.ApplyToUnityCamera( cam );

			//Debug.Log( intrinsics );
		}
	}
}