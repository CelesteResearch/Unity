Shader "Celeste/PostEffect"
{
    Properties
    {
        _MainTex("Source", 2D) = "white"
        {
        }
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always CGINCLUDE
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
        SamplerState sampler_point_clamp;
        SamplerState sampler_linear_clamp;
        float4x4 _DrawMatrix;
        float4 _Taps[9];
        int _TapCount;
        float _Blur, _Fade, _Linear;
        Output vert(Input v)
        {
            Output o;
            o.vertex = mul(_DrawMatrix, v.vertex);
            o.uv = v.uv;
            o.color = v.color;
            return o;
        }
        float4 frag(Output i) : SV_Target
        {
            if (_Blur > 0)
            {
                float4 value = _MainTex.Sample(sampler_point_clamp, i.uv + _Taps[0].xy) * _Taps[0].z;
                [unroll] for (int tap = 1; tap < 9; tap++) if (tap < _TapCount) value =
                    _MainTex.Sample(sampler_point_clamp, i.uv + _Taps[tap].xy) * _Taps[tap].z + value;
                float fade = (1 - length(i.uv - .5)) * _Fade;
                return (fade * -value + value) * i.color;
            }
            float4 source =
                _Linear > 0 ? _MainTex.Sample(sampler_linear_clamp, i.uv) : _MainTex.Sample(sampler_point_clamp, i.uv);
            return source * i.color;
        }
        ENDCG
        Pass{Blend One OneMinusSrcAlpha CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma target 3.5
                 ENDCG} Pass{Blend One Zero, Zero One CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma target 3.5
                                                 ENDCG} Pass{Blend SrcAlpha One, Zero One CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma target 3.5
                                                                                     ENDCG} Pass
        {
            Blend One One BlendOp Min CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma target 3.5
                ENDCG
        }
    }
}
