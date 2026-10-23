Shader "Celeste/Backdrop"
{
    Properties
    {
        _MainTex("Atlas", 2D) = "white"
        {
        }
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always CGINCLUDE
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
        SamplerState point_clamp_sampler;
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
            return _MainTex.Sample(point_clamp_sampler, i.uv) * i.color;
        }
        ENDCG
        Pass{Blend One OneMinusSrcAlpha CGPROGRAM
#pragma vertex vert
#pragma fragment frag
                 ENDCG} Pass
        {
            Blend SrcAlpha One CGPROGRAM
#pragma vertex vert
#pragma fragment frag
                ENDCG
        }
    }
}
