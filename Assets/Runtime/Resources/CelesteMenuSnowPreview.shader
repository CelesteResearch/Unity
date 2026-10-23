Shader "Celeste/Menu Snow Preview"
{
    Properties
    {
        _MainTex("Original snow texture", 2D) = "white"
        {
        }
    }
    SubShader
    {
        Tags{"Queue" = "Transparent"
                       "RenderType" = "Transparent"} Cull Off ZWrite Off ZTest Always Blend SrcAlpha One Pass
        {
            CGPROGRAM
#pragma target 3.5
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
            struct Input
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            struct Output
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            Texture2D _MainTex;
            SamplerState linear_repeat_sampler;
            Output vert(Input v)
            {
                Output o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }
            float4 frag(Output i) : SV_Target
            {
                return _MainTex.Sample(linear_repeat_sampler, i.uv) * i.color;
            }
            ENDCG
        }
    }
}
