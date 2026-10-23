Shader "Celeste/WaveDashQuad"
{
    Properties
    {
        _MainTex("Page", 2D) = "white"
        {
        }
    }
    SubShader
    {
        Cull Off ZWrite On ZTest LEqual Blend One OneMinusSrcAlpha Pass
        {
            CGPROGRAM
#pragma vertex vert
#pragma fragment frag
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
            sampler2D _MainTex;
            float4x4 _DrawMatrix;
            Output vert(Input v)
            {
                Output o;
                o.vertex = mul(_DrawMatrix, v.vertex);
                o.uv = v.uv;
                return o;
            }
            float4 frag(Output i) : SV_Target
            {
                return tex2D(_MainTex, i.uv);
            }
            ENDCG
        }
    }
}
