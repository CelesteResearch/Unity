Shader "Celeste/MountainPreview"
{
    Properties
    {
        _MainTex("Original terrain", 2D) = "white"
        {
        }
        _NextTex("Next terrain", 2D) = "white"
        {
        }
    }
    SubShader
    {
        Tags{"RenderType" = "Opaque"} Cull Back ZWrite On ZTest LEqual Pass
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
            };
            struct Output
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 local : TEXCOORD1;
            };
            Texture2D _MainTex, _NextTex;
            SamplerState sampler_linear_clamp;
            float _Ease;
            float3 _Fog;
            Output vert(Input v)
            {
                Output o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.local = v.vertex.xyz;
                return o;
            }
            float4 frag(Output i) : SV_Target
            {
                float fog = saturate((length(i.local.xz) * .055555556 - .25) * 1.33333337);
                float3 color = lerp(_MainTex.Sample(sampler_linear_clamp, i.uv).rgb,
                                    _NextTex.Sample(sampler_linear_clamp, i.uv).rgb, _Ease);
                return float4(color + fog * (_Fog - color), 1);
            }
            ENDCG
        }
    }
}
