Shader "Celeste/SourceGaussianBlur"
{
    Properties{_MainTex("Source", 2D) = "white" {} _SrcBlend("Source color", Float) =
                   1 _DstBlend("Destination color", Float) = 10 _SrcBlendAlpha("Source alpha", Float) =
                       1 _DstBlendAlpha("Destination alpha", Float) = 10 _BlendOp("Color operation", Float) =
                           0 _BlendOpAlpha("Alpha operation", Float) = 0} SubShader
    {
        Cull Off ZWrite Off ZTest Always Blend[_SrcBlend][_DstBlend],
            [_SrcBlendAlpha][_DstBlendAlpha] BlendOp[_BlendOp], [_BlendOpAlpha] Pass {
                CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma target 3.5
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
                float4x4 _DrawMatrix;
                float4 _Taps[9];
                int _TapCount;
                float _Fade;
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
                    float4 value = _MainTex.Sample(sampler_point_clamp, i.uv + _Taps[0].xy) * _Taps[0].z;
                    [unroll] for (int tap = 1; tap < 9; tap++) if (tap < _TapCount) value =
                        _MainTex.Sample(sampler_point_clamp, i.uv + _Taps[tap].xy) * _Taps[tap].z + value;
                    float fade = (1 - length(i.uv - .5)) * _Fade;
                    return (fade * -value + value) * i.color;
                }
                ENDCG
            }
    }
}
