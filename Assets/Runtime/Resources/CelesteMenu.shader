Shader "Celeste/Menu"
{
    Properties{_MainTex("Atlas", 2D) = "white" {} _PointSampling("Point sampling", Float) = 0} SubShader
    {
        Cull Off ZWrite Off ZTest Always Blend One OneMinusSrcAlpha Pass
        {
            CGPROGRAM
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
            SamplerState sampler_LinearClamp;
            SamplerState sampler_PointClamp;
            float _PointSampling;
            float4x4 _DrawMatrix;
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
                if (_PointSampling > .5)
                    return _MainTex.Sample(sampler_PointClamp, i.uv) * i.color;
                return _MainTex.Sample(sampler_LinearClamp, i.uv) * i.color;
            }
            ENDCG
        }
    }
}
