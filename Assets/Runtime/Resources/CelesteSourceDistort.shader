Shader "Celeste/SourceDistort"
{
    Properties{_MainTex("Gameplay", 2D) = "black" {} _Displacement("Displacement", 2D) =
                   "black" {} _WaterSine("Water sine", Float) = 0 _WaterCameraY("Water camera Y", Float) =
                       0 _WaterAlpha("Water alpha", Float) = 1 _Anxiety("Anxiety", Float) =
                           0 _GameRate("Game rate", Float) = 1 _AnxietyOrigin("Anxiety origin", Vector) =
                               (0, 0, 0, 0)_DistortTechnique("Distort technique", Float) =
                                   0 _SrcBlend("Source blend", Float) = 1 _DstBlend("Destination blend", Float) =
                                       10 _SrcBlendAlpha("Source alpha blend", Float) =
                                           1 _DstBlendAlpha("Destination alpha blend", Float) =
                                               10 _BlendOp("Color function", Float) =
                                                   0 _BlendOpAlpha("Alpha function", Float) = 0} SubShader
    {
        Cull Off ZWrite Off ZTest Always Blend[_SrcBlend][_DstBlend],
            [_SrcBlendAlpha][_DstBlendAlpha] BlendOp[_BlendOp], [_BlendOpAlpha] Pass {
                CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
                sampler2D _MainTex, _Displacement;
                float _WaterSine, _WaterCameraY, _WaterAlpha;
                float _Anxiety, _GameRate, _DistortTechnique;
                float2 _AnxietyOrigin;
                float4x4 _DrawMatrix;
                struct SourceInput
                {
                    float4 vertex : POSITION;
                    float2 uv : TEXCOORD0;
                    float4 color : COLOR;
                };
                struct SourceOutput
                {
                    float4 vertex : SV_POSITION;
                    float2 uv : TEXCOORD0;
                    float4 color : COLOR;
                };
                SourceOutput vert(SourceInput value)
                {
                    SourceOutput output;
                    output.vertex = mul(_DrawMatrix, value.vertex);
                    output.uv = value.uv;
                    output.color = value.color;
                    return output;
                }
                float4 frag(SourceOutput i) : SV_Target
                {
                    float2 uv = i.uv;
                    // Original Distort.xnb ps_2_0 instructions use top-down UVs.
                    float2 at = float2(uv.x, 1 - uv.y);

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
                    if (_DistortTechnique > 0)
                    {
                        float shift = length(at - _AnxietyOrigin) * _Anxiety * .04;
                        float4 left = tex2D(_MainTex, sampleUV - float2(shift, 0));
                        float4 right = tex2D(_MainTex, sampleUV + float2(shift, 0));
                        float3 rgb = float3(right.r, center.g, left.b);
                        float gray = dot(rgb, float3(.3, .59, .11));
                        return float4(lerp(rgb, gray.xxx, 1 - _GameRate), left.a) * i.color;
                    }
                    return center * i.color;
                }
                ENDCG
            }
    }
}
