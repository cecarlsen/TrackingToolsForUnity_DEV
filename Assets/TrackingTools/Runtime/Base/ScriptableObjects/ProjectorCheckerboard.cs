/*
	Copyright © Carl Emil Carlsen 2022
	http://cec.dk
*/

using UnityEngine;

namespace TrackingTools
{
	[CreateAssetMenu(fileName = "ProjectorCheckerboard", menuName = "ScriptableObjects/TrackingTools/ProjectorCheckerboard", order = 1)]
	public class ProjectorCheckerboard : Checkerboard
	{
		[Tooltip( "Millimeters" )] public int dotPatternCenterOffset;
	}
}