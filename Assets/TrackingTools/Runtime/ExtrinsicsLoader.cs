/*
	Copyright © Carl Emil Carlsen 2020
	http://cec.dk
*/

using UnityEngine;

namespace TrackingTools
{
	public class ExtrinsicsLoader : MonoBehaviour
	{
		[SerializeField] string _extrinsicsFileName = "DefaultCamera";
		[SerializeField,Tooltip("Optional")] Transform _anchorTransform = null;
		[SerializeField] bool _inverse = false;
		[SerializeField] bool _isMirrored = false;
		[SerializeField] bool _imbedInAnchorTransform = false;
		[SerializeField] bool _updateContinously = false;

		Extrinsics _extrinsics;


		void Awake()
		{
			if( !Extrinsics.TryLoadFromFile( _extrinsicsFileName, out _extrinsics ) ) {
				enabled = false;
				return;
			}

			_extrinsics.ApplyToTransform( transform, _anchorTransform, _inverse, _isMirrored );

			if( _anchorTransform && _imbedInAnchorTransform ) transform.SetParent( _anchorTransform );
		}


		void Update()
		{
			if( _updateContinously ) _extrinsics.ApplyToTransform( transform, _anchorTransform, _inverse, _isMirrored );
		}
	}
}