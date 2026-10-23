Shader "Celeste/Displace"
{
    Properties
    {
        _MainTex("Gameplay", 2D) = "black"
        {
        }
        _Displacement("Displacement", 2D) = "black"
        {
        }
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always Blend One OneMinusSrcAlpha Pass
        {
            CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#include "UnityCG.cginc"
            sampler2D _MainTex, _Displacement;
            float _WaterSine, _WaterCameraY, _WaterAlpha;
            float _Anxiety, _GameRate, _HasDisplacement;
            float2 _AnxietyOrigin;
            float4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                // Original Distort.xnb ps_2_0 Displace/Distort instructions use top-down UVs.
                float2 at = float2(uv.x, 1 - uv.y);
                if (_HasDisplacement > 0 || _Anxiety > 0 || _GameRate < 1)
                {
                    float3 map = tex2D(_Displacement, uv).rgb;
                    float angle = (at.y * 180 + _WaterCameraY) * .3 - map.b * _WaterSine;
                    angle = frac(angle * .159154937 + .5) * 6.28318548 - 3.14159274;
                    float wave = sin(angle);
                    at += (map.rg * 2 - 1) * float2(.044, .078);
                    at.x += wave * _WaterAlpha * .004 * (map.b + frac(-map.b));
                }
                float2 sampleUV = float2(at.x, 1 - at.y);
                float4 center = tex2D(_MainTex, sampleUV);
                if (_Anxiety > 0 || _GameRate < 1)
                {
                    float shift = length(at - _AnxietyOrigin) * _Anxiety * .04;
                    float4 left = tex2D(_MainTex, sampleUV - float2(shift, 0));
                    float4 right = tex2D(_MainTex, sampleUV + float2(shift, 0));
                    float3 rgb = float3(right.r, center.g, left.b);
                    float gray = dot(rgb, float3(.3, .59, .11));
                    return float4(lerp(rgb, gray.xxx, 1 - _GameRate), left.a);
                }
                return center;
            }
            ENDCG
        }
    }
}
