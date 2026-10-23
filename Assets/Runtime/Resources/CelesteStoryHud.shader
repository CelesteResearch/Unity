Shader "Celeste/StoryHud"
{
    Properties{_MainTex("Atlas", 2D) = "white" {} _PointSampling("Point sampling", Float) = 0} SubShader
    {
        Cull Off ZWrite Off ZTest Always CGINCLUDE struct Input
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
            return _MainTex.Sample(sampler_LinearClamp, i.uv) * i.color;
        }
        ENDCG
        Pass{Blend One OneMinusSrcAlpha CGPROGRAM
#pragma target 3.5
#pragma vertex vert
#pragma fragment frag
                 ENDCG} Pass
        {
            // XNA BlendState.Additive: Color and Alpha SourceAlpha / One.
            Blend SrcAlpha One, SrcAlpha One CGPROGRAM
#pragma target 3.5
#pragma vertex vert
#pragma fragment frag
                ENDCG
        }
    }
}
