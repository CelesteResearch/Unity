Shader "Celeste/MagicGlow"
{
    Properties
    {
        _MainTex("Feather buffer", 2D) = "white"
        {
        }
        _Noise("Glow noise", 2D) = "white"
        {
        }
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always Blend One OneMinusSrcAlpha Pass
        {
            HLSLPROGRAM
#pragma target 3.5
#pragma vertex vert
#pragma fragment frag
            Texture2D _MainTex, _Noise;
            SamplerState sampler_linear_clamp;
            SamplerState sampler_linear_repeat;
            float4x4 _DrawMatrix;
            float2 _Pixel;
            float _NoiseEase, _Direction;
            struct Input
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            struct Output
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            Output vert(Input v)
            {
                Output o;
                o.vertex = mul(_DrawMatrix, v.vertex);
                o.uv = v.uv;
                return o;
            }
            float4 source(float2 nativeUV)
            {
                return _MainTex.Sample(sampler_linear_clamp, nativeUV);
            }
            float4 frag(Output i) : SV_Target
            {
                float4 noise = _Noise.Sample(sampler_linear_repeat, float2(i.uv.x, i.uv.y * .5 + _NoiseEase));
                precise float2 sampleUV = (noise.gb - .5) * _Pixel * 2 + i.uv;
                precise float offset = _Pixel.y * _Direction;
                precise float4 value = source(sampleUV);
                value = source(sampleUV + float2(0, offset)) * .94 + value;
                value = source(sampleUV + float2(0, offset * 2)) * .88 + value;
                value = source(sampleUV + float2(0, offset * 3)) * .82 + value;
                value = source(sampleUV + float2(0, offset * 4)) * .76 + value;
                value = source(sampleUV + float2(0, offset * 5)) * .70 + value;
                value = source(sampleUV + float2(0, offset * 6)) * .64 + value;
                value = source(sampleUV + float2(0, offset * 7)) * .58 + value;
                value = source(sampleUV + float2(0, offset * 8)) * .52 + value;
                value = source(sampleUV + float2(0, offset * 9)) * .46 + value;
                value = source(sampleUV + float2(0, offset * 10)) * .40 + value;
                value = source(sampleUV + float2(0, offset * 11)) * .34 + value;
                value = source(sampleUV + float2(0, offset * 12)) * .28 + value;
                value = source(sampleUV + float2(0, offset * 13)) * .22 + value;
                value = source(sampleUV + float2(0, offset * 14)) * .16 + value;
                value = source(sampleUV + float2(0, offset * 15)) * .10 + value;
                value = source(sampleUV + float2(0, offset * 16)) * .04 + value;
                return value * noise.r * .5;
            }
            ENDHLSL
        }
    }
}
