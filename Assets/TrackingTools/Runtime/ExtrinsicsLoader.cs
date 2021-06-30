/*
	Copyright © Carl Emil Carlsen 2020-2021
	http://cec.dk
*/

using System;
using UnityEngine;

namespace TrackingTools
{
	public class ExtrinsicsLoader : MonoBehaviour
	{
		[SerializeField] string _fileNameWithoutExtension = "DefaultCamera";
		[SerializeField] LoadMode _mode = LoadMode.Awake;
		[SerializeField,Tooltip("Optional")] Transform _anchorTransform = null;
		[SerializeField] bool _inverse = false;
		[SerializeField] bool _isMirrored = false;
		[SerializeField] bool _imbedInAnchorTransform = false;
		[SerializeField] bool _updateContinously = false;
		[SerializeField] Space _space = Space.World;

		Extrinsics _extrinsics;

		[Serializable] enum LoadMode { None, Awake, Start }


		public string fileNameWithoutExtension {
			get { return _fileNameWithoutExtension; }
			set {
				_fileNameWithoutExtension = value;
				Load();
			}
		}


		void Awake()
		{
			if( _mode == LoadMode.Awake ) Load();
		}


		void Start()
		{
			if( _mode == LoadMode.Start ) Load();
		}


		void Update()
		{
			if( _extrinsics == null ) return;

			if( _updateContinously ) _extrinsics.ApplyToTransform( transform, _anchorTransform, _inverse, _isMirrored, _space );
		}



		void Load()
		{
			if( !Extrinsics.TryLoadFromFile( _fileNameWithoutExtension, out _extrinsics ) ) return;

			if( _anchorTransform && _imbedInAnchorTransform ) transform.SetParent( _anchorTransform );

			_extrinsics.ApplyToTransform( transform, _anchorTransform, _inverse, _isMirrored, _space );
		}
	}
}