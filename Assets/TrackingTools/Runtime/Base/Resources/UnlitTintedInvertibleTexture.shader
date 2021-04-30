Shader "Unlit/TintedInvertibleTexture"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_Color ("Tint", Color) = (1,1,1,1)
		_Invert ("Invert", Int) = 0
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

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
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			fixed4 _Color;
			float _Invert;


			ToFrag Vert( ToVert v )
			{
				ToFrag o;
				o.vertex = UnityObjectToClipPos( v.vertex );
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				return o;
			}


			fixed4 Frag( ToFrag i ) : SV_Target
			{
				fixed4 col = tex2D( _MainTex, i.uv ) * _Color;
				col.rgb = _Invert * ( 1 - col.rgb ) + ( 1 - _Invert ) * col.rgb;
				return col;
			}
			ENDCG
		}
	}
}