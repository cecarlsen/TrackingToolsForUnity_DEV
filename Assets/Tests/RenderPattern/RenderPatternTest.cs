/*
	Copyright © Carl Emil Carlsen 2020
	http://cec.dk
*/

using UnityEngine;
using UnityEngine.Events;
using TrackingTools;

public class RenderPatternTest : MonoBehaviour
{
	[SerializeField] Vector2Int _patternSize = new Vector2Int( 7, 11 );
	[SerializeField] bool _forceValidPatternSize = true;
	[SerializeField] TrackingToolsHelper.PatternType _patternType = TrackingToolsHelper.PatternType.Checkerboard;
	[SerializeField] int _resolutionMax = 2014;
	[SerializeField] bool _invert = false;
	[SerializeField] float _border = 1;

	[SerializeField] UnityEvent<RenderTexture> _renderTextureEvent = null;
	[SerializeField] UnityEvent<float> _aspectEvent = null;

	RenderTexture _renderTexture;
	Material _material;


	void Awake()
	{
		/*
		TrackingToolsHelper.RenderPattern( _patternSize, _patternType, _resolutionMax, ref _renderTexture, ref _material, _border, _invert );
		_renderTextureEvent.Invoke( _renderTexture );
		_aspectEvent.Invoke( _renderTexture.width / (float) _renderTexture.height );
		*/
	}


	void OnValidate()
	{
		if( _forceValidPatternSize ) _patternSize = TrackingToolsHelper.GetClosestValidPatternSize( _patternSize, _patternType );
	}



	void Update()
	{
		TrackingToolsHelper.RenderPattern( _patternSize, _patternType, _resolutionMax, ref _renderTexture, ref _material, _border, _invert );
		_renderTextureEvent.Invoke( _renderTexture );
		_aspectEvent.Invoke( _renderTexture.width / (float) _renderTexture.height );
	}


	void OnDestroy()
	{
		if( _renderTexture ) _renderTexture.Release();
		if( _material ) Destroy( _material );
	}
}