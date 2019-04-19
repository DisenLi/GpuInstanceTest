Shader "WindStorm/Vegetation/Grass_Instance"
{
	Properties
	{
		_Color   ("Color", Color) = (1, 1, 1, 1)
		_Power ("Power", Range(0, 2)) = 1
		_MainTex ("Texture", 2D) = "white" {}
		_WindTex ("风贴图", 2D) = "white" {}
		_WindSpeed("风速",Range(0, 8))=2
        _WindSize("风尺寸",Float)=20
		_DissolveColor("Color", Color) = (1,1,1,0)

		_StampVector("移动", vector) = (0,0,0,0)
	}
	SubShader
	{
		Tags { "Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="TransparentCutout" }
		LOD 100

		Pass
		{
			Cull off
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			// make fog work
			#pragma multi_compile_fog
			#pragma multi_compile_instancing
			#pragma instancing_options lodfade
			#pragma target 3.0
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				UNITY_FOG_COORDS(1)
				float4 vertex : SV_POSITION;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			UNITY_INSTANCING_CBUFFER_START(props)
			UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color1) // Make _Color an instanced property (i.e. an array)
			UNITY_INSTANCING_CBUFFER_END

			sampler2D _MainTex;
			float4 _MainTex_ST;
			sampler2D _WindTex;
			float _WindSpeed;
			float _WindSize;
			fixed _Power;
			fixed4 _DissolveColor;
			fixed4 _Color;
			float4 _StampVector;

			float GetWindWave(float2 position, float speed)
			{
				float4 p=tex2Dlod(_WindTex,float4(position/_WindSize+float2(_Time.x*speed,0),0.0,0.0)); 
				return p.r-0.5;
			}
			
			v2f vert (appdata v)
			{
				v2f o;
				//UNITY_INITIALIZE_OUTPUT(v2f, o);
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				fixed3 t_pos = mul(unity_ObjectToWorld, v.vertex).xyz;
				float dis = floor(saturate(distance(_StampVector.xz, t_pos.xz)/_StampVector.w)) ;
				float speed = lerp(8* _StampVector.y, _WindSpeed, dis);
				float w = GetWindWave(t_pos.xz, speed);
				v.vertex.x += w * v.vertex.y * speed * 0.15;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				UNITY_TRANSFER_FOG(o,o.vertex);
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(i);
				// sample the texture
				fixed4 col = tex2D(_MainTex, i.uv) ;
				fixed clipValue = saturate(col.a-0.5) - 0.001;
				clip(clipValue);
				col.rgb = col.rgb * lerp(_Color, _DissolveColor.rgb, (1-clipValue)*_DissolveColor.a)*_Power;
				// apply fog
				UNITY_APPLY_FOG(i.fogCoord, col);
				return col;
			}
			ENDCG
		}
	}
}
