/*
	Copyright © Carl Emil Carlsen 2020
	http://cec.dk

	Holds the rotation and translation that will transform the calibrated pattern 
	to fit in camera view.
*/

using System.IO;
using UnityEngine;
using OpenCVForUnity.CoreModule;

namespace TrackingTools
{
	[System.Serializable]
	public class Extrinsics
	{
		[SerializeField] Quaternion rotation;
		[SerializeField] Vector3 translation;

		static readonly string logPrepend = "<b>[" + nameof( Extrinsics ) + "]</b> ";


		public void ApplyToTransform( Transform transform, Transform achorTransform = null, bool inverse = false, bool isMirrored = false )
		{
			Quaternion r = rotation;
			Vector3 t = translation;

			if( inverse ) {
				r = Quaternion.Inverse( rotation );
				t = r * -translation;
			}

			if( achorTransform ) {
				if( isMirrored ) r.z *= -1;
				r = achorTransform.rotation * r;
				t = achorTransform.position + achorTransform.rotation * t;
			}

			transform.SetPositionAndRotation( t, r );
		}


		public string SaveToFile( string fileName )
		{
			if( !Directory.Exists( TrackingToolsConstants.extrinsicsDirectoryPath ) ) Directory.CreateDirectory( TrackingToolsConstants.extrinsicsDirectoryPath );
			string filePath = TrackingToolsConstants.extrinsicsDirectoryPath + "/" + fileName;
			if( !fileName.EndsWith( ".json" ) ) filePath += ".json";
			File.WriteAllText( filePath, JsonUtility.ToJson( this ) );
			return filePath;
		}


		public static bool TryLoadFromFile( string fileName, out Extrinsics extrinsics )
		{
			extrinsics = null;

			if( !Directory.Exists( TrackingToolsConstants.extrinsicsDirectoryPath ) ) {
				Debug.LogError( logPrepend + "Directory missing.\n" + TrackingToolsConstants.extrinsicsDirectoryPath );
				return false;
			}

			string filePath = TrackingToolsConstants.extrinsicsDirectoryPath + "/" + fileName;
			if( !fileName.EndsWith( ".json" ) ) filePath += ".json";
			if( !File.Exists( filePath ) ) {
				Debug.LogError( logPrepend + "File missing.\n" + filePath );
				return false;
			}

			extrinsics = JsonUtility.FromJson<Extrinsics>( File.ReadAllText( filePath ) );
			return true;
		}


		public void UpdateFromOpenCvSolvePnp( Mat rotationVectorMat, Mat translationVectorMat )
		{
			Vector3 rotationVector = rotationVectorMat.ReadVector3();
			rotation = Quaternion.AngleAxis( rotationVector.magnitude * Mathf.Rad2Deg, rotationVector );
			translation = translationVectorMat.ReadVector3(); 

			// Store the inverse. It seems more intuitive to store the extrinsics of the camera, not the calibration board.
			rotation = Quaternion.Inverse( rotation );
			translation = rotation * -translation;
		}


		public void UpdateFromOpenCvStereoCalibrate( Mat rotation3x3Mat, Mat translationVectorMat )
		{
			Matrix4x4 rotMat = Matrix4x4.identity;
			rotMat.m00 = (float) rotation3x3Mat.ReadValue( 0, 0 );
			rotMat.m01 = (float) rotation3x3Mat.ReadValue( 0, 1 );
			rotMat.m02 = (float) rotation3x3Mat.ReadValue( 0, 2 );
			rotMat.m10 = (float) rotation3x3Mat.ReadValue( 1, 0 );
			rotMat.m11 = (float) rotation3x3Mat.ReadValue( 1, 1 );
			rotMat.m12 = (float) rotation3x3Mat.ReadValue( 1, 2 );
			rotMat.m20 = (float) rotation3x3Mat.ReadValue( 2, 0 );
			rotMat.m21 = (float) rotation3x3Mat.ReadValue( 2, 1 );
			rotMat.m22 = (float) rotation3x3Mat.ReadValue( 2, 2 );
			rotation = rotMat.rotation;
			translation = translationVectorMat.ReadVector3();

			// Store the inverse. It is more intuitive to store the extrinsics of the **camera**, not the calibration board.
			rotation = Quaternion.Inverse( rotation );
			translation = rotation * -translation;
		}


		public override string ToString()
		{
			return "t, r: " + translation + ", " + rotation;
		}
	}
}