/*
	Copyright © Carl Emil Carlsen 2020
	http://cec.dk
*/

using UnityEngine;

namespace TrackingTools
{
	public static class RectTrasnformExtensions
	{

		public static void FitParent( this RectTransform t )
		{
			t.anchorMin = Vector2.zero;
			t.anchorMax = Vector2.one;
			t.offsetMin = Vector2.zero;
			t.offsetMax = Vector2.zero;
		}

	}
}