Shader "Celeste/MenuPreview"
{
    Properties{_MainTex("Atlas", 2D) = "white" {} _PointSampling("Point sampling", Float) = 0} SubShader
    {
        Tags{"Queue" = "Transparent"
                       "RenderType" = "Transparent"} Cull Off ZWrite Off ZTest Always Blend One OneMinusSrcAlpha Pass
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
            SamplerState sampler_LinearClamp;
            SamplerState sampler_PointClamp;
            float _PointSampling;
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
                if (_PointSampling > .5)
                    return _MainTex.Sample(sampler_PointClamp, i.uv) * i.color;
                return _MainTex.Sample(sampler_LinearClamp, i.uv) * i.color;
            }
            ENDCG
        }
    }
}
