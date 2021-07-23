/*
	Copyright © Carl Emil Carlsen 2020-2021
	http://cec.dk
*/

using UnityEngine;

namespace TrackingTools
{
	[RequireComponent( typeof( Camera) ) ]
	public class CameraIntrinsicsLoader : MonoBehaviour
	{
		[SerializeField] string _intrinsicsFileName = "DefaultCamera";

		public string intrinsicsFileName {
			get { return _intrinsicsFileName; }
			set { _intrinsicsFileName = value; }
		}


		void Start()
		{
			Intrinsics intrinsics;
			if( !Intrinsics.TryLoadFromFile( _intrinsicsFileName, out intrinsics ) ) return;

			Camera cam = GetComponent<Camera>();
			intrinsics.ApplyToCamera( cam );
		}
	}
}