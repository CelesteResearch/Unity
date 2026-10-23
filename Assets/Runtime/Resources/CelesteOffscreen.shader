Shader "Celeste/Offscreen"
{
    Properties
    {
        _MainTex("Atlas", 2D) = "white"
        {
        }
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always Blend One OneMinusSrcAlpha Pass
        {
            CGPROGRAM
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
            sampler2D _MainTex;
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
                return tex2D(_MainTex, i.uv) * i.color;
            }
            ENDCG
        }
    }
}
