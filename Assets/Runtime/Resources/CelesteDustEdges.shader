Shader "Celeste/DustEdges"
{
    Properties
    {
        _MainTex("Dust mask", 2D) = "white"
        {
        }
        _NoiseFrom("Noise A", 2D) = "white"
        {
        }
        _NoiseTo("Noise B", 2D) = "white"
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
            Texture2D _MainTex, _NoiseFrom, _NoiseTo;
            SamplerState sampler_point_clamp;
            SamplerState sampler_linear_repeat;
            float4x4 _DrawMatrix;
            float3 _Colors[3];
            float _NoiseEase;
            float2 _NoiseFromPos, _NoiseToPos, _Pixel;
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
            float4 frag(Output i) : SV_Target
            {
                float2 nativeUV = i.uv;
                float n0 = _NoiseFrom.Sample(sampler_linear_repeat, fmod(nativeUV + 1 + _NoiseFromPos, 1)).r;
                float n1 = _NoiseTo.Sample(sampler_linear_repeat, fmod(nativeUV + 1 + _NoiseToPos, 1)).r;
                float index = floor(lerp(n0, n1, _NoiseEase) * 3);
                float3 color = index == 0   ? _Colors[0]
                               : index == 1 ? _Colors[1]
                               : index == 2 ? _Colors[2]
                                            : float3(0, 0, 0);
                float2 uv = float2(nativeUV.x, 1 - nativeUV.y), pixel = _Pixel;
                float alpha = max(max(_MainTex.Sample(sampler_point_clamp, uv + float2(0, pixel.y)).a,
                                      _MainTex.Sample(sampler_point_clamp, uv - float2(0, pixel.y)).a),
                                  max(_MainTex.Sample(sampler_point_clamp, uv - float2(pixel.x, 0)).a,
                                      _MainTex.Sample(sampler_point_clamp, uv + float2(pixel.x, 0)).a));
                return float4(color, 1) * alpha;
            }
            ENDHLSL
        }
    }
}
