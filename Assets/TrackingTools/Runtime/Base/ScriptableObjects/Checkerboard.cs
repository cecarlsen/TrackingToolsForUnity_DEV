/*
	Copyright © Carl Emil Carlsen 2022
	http://cec.dk
*/

using UnityEngine;

namespace TrackingTools
{
	[CreateAssetMenu(fileName = "Checkerboard", menuName = "ScriptableObjects/TrackingTools/Checkerboard", order = 1)]
	public class Checkerboard : ScriptableObject
	{
		[Tooltip( "Number of inner corners" )] public Vector2Int checkerPatternSize;
		[Tooltip( "Millimeters" )] public int checkerTileSize;
		[Tooltip( "See OpenCV docs on 'CALIB_CB_MARKER'" )] public bool hasMaker;
	}
}