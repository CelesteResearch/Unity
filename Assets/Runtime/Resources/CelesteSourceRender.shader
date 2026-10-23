Shader "Celeste/SourceRender"
{
    Properties{_MainTex("Atlas", 2D) = "white" {} _SrcBlend("Source blend", Float) =
                   1 _DstBlend("Destination blend", Float) = 10 _SrcBlendAlpha("Source alpha blend", Float) =
                       1 _DstBlendAlpha("Destination alpha blend", Float) =
                           10 _BlendOp("Color blend operation", Float) =
                               0 _BlendOpAlpha("Alpha blend operation", Float) = 0} SubShader
    {
        Cull Off ZWrite Off ZTest Always Blend[_SrcBlend][_DstBlend],
            [_SrcBlendAlpha][_DstBlendAlpha] BlendOp[_BlendOp], [_BlendOpAlpha] Pass {
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
