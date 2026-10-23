Shader "Celeste/Complete"
{
    Properties{_MainTex("Original texture", 2D) = "white" {} _WrapSampling("Linear wrap", Float) = 0} SubShader
    {
        Tags{"Queue" = "Transparent"
                       "RenderType" =
                           "Transparent"} Cull Off ZWrite Off ZTest Always Blend One OneMinusSrcAlpha CGINCLUDE
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
        SamplerState linear_clamp_sampler;
        SamplerState linear_repeat_sampler;
        float _WrapSampling;
        float4x4 _DrawMatrix;
        Output vertBuffer(Input v)
        {
            Output o;
            o.vertex = mul(_DrawMatrix, v.vertex);
            o.uv = v.uv;
            o.color = v.color;
            return o;
        }
        Output vertScene(Input v)
        {
            Output o;
            o.vertex = UnityObjectToClipPos(v.vertex);
            o.uv = v.uv;
            o.color = v.color;
            return o;
        }
        float4 frag(Output i) : SV_Target
        {
            if (_WrapSampling > .5)
                return _MainTex.Sample(linear_repeat_sampler, i.uv) * i.color;
            return _MainTex.Sample(linear_clamp_sampler, i.uv) * i.color;
        }
        ENDCG
        Pass{Tags{"LightMode" = "CelesteViewport"} CGPROGRAM
#pragma target 3.5
#pragma vertex vertBuffer
#pragma fragment frag
                 ENDCG} Pass
        {
            CGPROGRAM
#pragma target 3.5
#pragma vertex vertScene
#pragma fragment frag
            ENDCG
        }
    }
}
