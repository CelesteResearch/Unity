Shader "Celeste/SourceMirrors"
{
    Properties{_MainTex("Masks", 2D) = "black" {} _Sources("Reflected objects", 2D) =
                   "black" {} _Pixel("Pixel and preshader reciprocal", Vector) =
                       (.00260416674, .00409836043, 384, 244.000015)_SrcBlend("Source blend", Float) =
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
#pragma target 4.0
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
                sampler2D _MainTex, _Sources;
                float4 _Pixel;
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
                    float4 mask = tex2D(_MainTex, i.uv);
                    // Source mesh UV already includes the original Draw at (-32,-32).
                    // Preserve each multiply and floor in Mirrors-628.asm, not an offset simplification.
                    precise float2 packedOffset = (mask.rg - .5) * _Pixel.xy;
                    packedOffset = packedOffset * 64;
                    packedOffset = packedOffset * _Pixel.zw;
                    float2 offset = floor(packedOffset) * _Pixel.xy;
                    float2 at = i.uv + float2(offset.x, -offset.y);
                    float4 source = tex2D(_Sources, at);
                    return source * mask.a * float4(.75, .75, 1.39999998, 1);
                }
                ENDCG
            }
    }
}
