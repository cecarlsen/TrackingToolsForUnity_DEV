/*
	Copyright © Carl Emil Carlsen 2020
	http://cec.dk
*/

﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class MaterialPropFlasher
{
	Material _material;
	int _shaderPropId;
	float _duration;
	bool _isFlashing;
	float _flashStartTime;
	float _value;
	bool _changed;

	public bool changed {  get { return _changed; } }
	public bool isFlashing {  get { return _isFlashing; } }
	public float value { get { return _value; } }


	public MaterialPropFlasher( Material material, string shaderPropName, float duration )
	{
		_material = material;
		_shaderPropId = Shader.PropertyToID( shaderPropName );
		_duration = duration;
	}


	public void Update()
	{
		if( !_isFlashing ){
			_changed = false;
			return;
		}

		float timeElapsed = Time.time - _flashStartTime;
		if( timeElapsed > _duration ) {
			_isFlashing = false;
			timeElapsed = _duration;
		}
		_value = 1 - timeElapsed / _duration;
		_material.SetFloat( _shaderPropId, _value );
		_changed = true;
	}


	public void Start()
	{
		_isFlashing = true;
		_changed = true;
		_flashStartTime = Time.time;
	}
}
