Shader "Celeste/LightPrimitive"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always CGINCLUDE struct Input
        {
            float4 vertex : POSITION;
            float4 color : COLOR;
        };
        struct Output
        {
            float4 vertex : SV_POSITION;
            float4 color : COLOR;
        };
        float4x4 _DrawMatrix;
        Output vert(Input v)
        {
            Output o;
            o.vertex = mul(_DrawMatrix, v.vertex);
            o.color = v.color;
            return o;
        }
        float4 frag(Output i) : SV_Target
        {
            return i.color;
        }
        ENDCG
        Pass{Blend One One BlendOp Min CGPROGRAM
#pragma vertex vert
#pragma fragment frag
                 ENDCG} Pass
        {
            Blend One One BlendOp Max CGPROGRAM
#pragma vertex vert
#pragma fragment frag
                ENDCG
        }
    }
}
