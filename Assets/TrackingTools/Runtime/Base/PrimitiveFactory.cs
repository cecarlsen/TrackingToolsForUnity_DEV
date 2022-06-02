/*
	Copyright © Carl Emil Carlsen 2022
	http://cec.dk
*/

using UnityEngine;

namespace TrackingTools
{
	public static class PrimitiveFactory
	{

		public static Mesh Quad()
		{
			Mesh mesh = new Mesh();
			mesh.name = "Quad";
			mesh.hideFlags = HideFlags.HideAndDontSave;
			mesh.vertices = new Vector3[]{
				new Vector3( -0.5f, -0.5f ),
				new Vector3( -0.5f,  0.5f ),
				new Vector3(  0.5f,  0.5f ),
				new Vector3(  0.5f, -0.5f )
			};
			mesh.uv = new Vector2[]{
				new Vector2( 0f, 0f ),
				new Vector2( 0f, 1f ),
				new Vector2( 1f, 1f ),
				new Vector2( 1f, 0f )
			};
			mesh.normals = new Vector3[]{
				Vector3.forward,
				Vector3.forward,
				Vector3.forward,
				Vector3.forward
			};
			mesh.triangles = new int[]{ 0, 1, 2, 2, 3, 0 };
			return mesh;
		}
	}
}