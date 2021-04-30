/*
	Copyright © Carl Emil Carlsen 2020
	http://cec.dk
*/

using UnityEngine;

namespace TrackingTools
{
	public static class TrackingToolsConstants
	{
		public static string intrinsicsDirectoryPath { get { return Application.streamingAssetsPath + "/TrackingTools/Intrinsics"; } }
		public static string extrinsicsDirectoryPath { get { return Application.streamingAssetsPath + "/TrackingTools/Extrinsics"; } }
		public static string circleAnchorsDirectoryPath { get { return Application.streamingAssetsPath + "/TrackingTools/CircleAnchors"; } }

		public const string previewShaderName = "Hidden/IntrinsicsCalibrationPreview";

		public const float flashDuration = 0.5f;

		public const float precisionTestDotSize  = 0.005f; // Meters
	}
}