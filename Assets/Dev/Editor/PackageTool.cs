using UnityEngine;
using UnityEditor;

public class PackageTool
{
	[MenuItem("Package/Update Package")]
	static void UpdatePackage()
	{
		AssetDatabase.ExportPackage( "Assets/TrackingTools", "TrackingTools.unitypackage", ExportPackageOptions.Recurse );
		AssetDatabase.ExportPackage( "Assets/TrackingTools.KinectAzure", "TrackingTools.KinectAzure.unitypackage", ExportPackageOptions.Recurse );
		Debug.Log( "Updated packages\n" );
	}
}
