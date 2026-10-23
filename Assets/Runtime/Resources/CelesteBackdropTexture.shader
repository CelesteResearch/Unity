Shader "Celeste/BackdropTexture"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white"
        {
        }
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always Pass
        {
            Blend One OneMinusSrcAlpha CGPROGRAM
#pragma target 3.5
#pragma vertex vert
#pragma fragment frag
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
            SamplerState sampler_linear_clamp;
            SamplerState sampler_linear_repeat;
            float4x4 _DrawMatrix;
            float _Wrap;
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
                float4 source = _Wrap > 0 ? _MainTex.Sample(sampler_linear_repeat, i.uv)
                                          : _MainTex.Sample(sampler_linear_clamp, i.uv);
                return source * i.color;
            }
            ENDCG
        }
    }
}
