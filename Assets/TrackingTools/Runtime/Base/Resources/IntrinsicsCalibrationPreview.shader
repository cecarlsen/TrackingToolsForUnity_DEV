Shader "Hidden/IntrinsicsCalibrationPreview"
{
	Properties
	{
		[PerRendererData] _MainTex ("Texture", 2D) = "white" {}
		_Whiteout ("Whiteout", Range( 0.0, 1.0 )) = 0.0
	}
	SubShader
	{
		Tags
		{
			"Queue" = "Transparent"
			"IgnoreProjector" = "True"
			"RenderType" = "Transparent"
			"PreviewType" = "Plane"
			"CanUseSpriteAtlas" = "True"
		}

		Blend SrcAlpha OneMinusSrcAlpha
		Cull Off
		Lighting Off
		ZWrite Off

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv0 : TEXCOORD0;
				float4 color : COLOR;
			};

			struct v2f
			{
				float2 uv0 : TEXCOORD0;
				float4 vertex : SV_POSITION;
				float4 color : COLOR;
			};

			sampler2D _MainTex;
			float _Whiteout;

			
			v2f vert( appdata v )
			{
				v2f o;
				o.vertex = UnityObjectToClipPos( v.vertex );
				o.uv0 = v.uv0;
				o.color = v.color;
				return o;
			}

			fixed4 frag( v2f i ) : SV_Target
			{
				float brightness = tex2D( _MainTex, i.uv0 ).r;
				
				fixed4 col = fixed4( brightness.xxx, 1 );

				if( brightness >= 0.999 ) col.rgb = fixed3( 1, 0, 0 );		// Red for whiteout
				else if( brightness == 0.0 ) col.rgb = fixed3( 0, 0, 1 );	// Blue for blackout

				col.rgb = lerp( col.rgb, fixed3(1,1,1), _Whiteout );

				return col;
			}
			ENDCG
		}
	}
}