Shader "Hidden/CameraLensUndistorterGpu"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}
	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			CGPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag

			#include "UnityCG.cginc"

			struct ToVert
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct ToFrag
			{
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			sampler2D _MainTex;
			sampler2D _UndistortLutRead;
			float2 _Resolution;


			ToFrag Vert( ToVert v )
			{
				ToFrag o;
				o.vertex = UnityObjectToClipPos( v.vertex );
				o.uv = v.uv;
				return o;
			}


			half4 Frag( ToFrag i ) : SV_Target
			{
				// Read undistort map and convert from pixel to uv space.
				float2 uv = ( tex2D( _UndistortLutRead, float2( i.uv.x, 1.0 - i.uv.y ) ).rg + 0.5 ) / _Resolution;

				// We need to flip the distortion effect to undistort.
				float2 diff = ( i.uv - uv );// * 10; // DEBUG: scale up to see the effect more clearly.
				uv = i.uv - diff;

				// Compute a alpha fade value for reading outside texture.
				float2 excess = 1.0 - saturate( ( abs( uv - 0.5 ) - 0.5 ) * _Resolution );
				float alpha = excess.x * excess.y; 

				// Read and apply alpha.
				half4 col = tex2D( _MainTex, uv );
				col.a = alpha;

				// Done.
				return col;
			}
			ENDCG
		}
	}
}
