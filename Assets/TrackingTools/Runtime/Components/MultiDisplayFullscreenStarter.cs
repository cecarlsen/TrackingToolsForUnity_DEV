/*
	Copyright © Carl Emil Carlsen 2022
	http://cec.dk
*/

using System.Collections.Generic;
using UnityEngine;

namespace TrackingTools
{
	public class MultiDisplayFullscreenStarter : MonoBehaviour
	{
		[SerializeField] FullScreenMode _fullscreenMode = FullScreenMode.FullScreenWindow;
		[SerializeField] ResolutionRequest _mainDisplayResolutionRequests;
		[SerializeField] ResolutionRequest[] _additionalDisplayResolutionRequests = new ResolutionRequest[ 1 ];

		List<Display> _aditionalDisplays = new List<Display>();

		static string logPrepend = "<b>" + nameof( MultiDisplayFullscreenStarter ) + "</b> ";


		public Display main { get { return Display.main; } }

		public Display second{ get { return hasSecond ? _aditionalDisplays[0] : null ; } }

		public Display third{ get { return hasThird ? _aditionalDisplays[1] : null ; } }

		public bool hasSecond { get { return _aditionalDisplays.Count > 0; } }

		public bool hasThird{ get { return _aditionalDisplays.Count > 1; } }

		// Unity's Resolution is not displayed in the inspector, so we define our own.
		[System.Serializable]
		public class ResolutionRequest {
			public Vector2Int resolution = new Vector2Int( 1920, 1080 );
			public int frequency = 60;
		}


		void Start()
		{
			if( Application.isEditor ) return;

			Screen.fullScreenMode = _fullscreenMode;
			Screen.fullScreen = true;

			Display[] displays = Display.displays;
			int displayCount = displays.Length;
			int	expectedAdditionalDisplayCount = _additionalDisplayResolutionRequests.Length;

			Debug.Log( logPrepend + " Activating displays ...\nTotal: " + displayCount + ", Expected: " + ( 1 + expectedAdditionalDisplayCount ) + "\n" );

			for( int d = 0; d < displays.Length; d++ )
			{
				Display display = displays[ d ];
				if( d == 0 ){ // First is always Main display.
					display.Activate( _mainDisplayResolutionRequests.resolution.x, _mainDisplayResolutionRequests.resolution.y, _mainDisplayResolutionRequests.frequency );
					continue;
				}
				if( d > expectedAdditionalDisplayCount ) return;

				// Activate at native display resolution.
				ResolutionRequest request = _additionalDisplayResolutionRequests[ d-1 ];
				display.Activate( request.resolution.x, request.resolution.y, request.frequency );

				// Store additional displays.
				_aditionalDisplays.Add( display );

				// Log.
				Debug.Log( logPrepend + " Additional display: System " + display.systemWidth + "x" + display.systemHeight + ", Render " + display.renderingWidth + "x" + display.renderingHeight + "\n" );
			}
		}
	}
}