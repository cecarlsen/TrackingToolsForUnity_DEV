/*
	Copyright © Carl Emil Carlsen 2020
	http://cec.dk
*/

using UnityEngine;
using UnityEngine.Events;

namespace TrackingTools
{
	public class FramesPerSecond : MonoBehaviour
	{
		[SerializeField] UnityEvent<string> fpsTextEvent = null;

		float _smootherFps;


		void Awake()
		{
			_smootherFps = 1 / Time.fixedDeltaTime;
		}


		void Update()
		{
			if( Time.smoothDeltaTime <= 0 ){
				fpsTextEvent.Invoke( 0.ToString( "F1" ) );
				return;
			}

			_smootherFps = Mathf.Lerp( _smootherFps, 1 / Time.smoothDeltaTime, 0.2f );
			fpsTextEvent.Invoke( _smootherFps.ToString( "F1" ) );
		}
	}
}