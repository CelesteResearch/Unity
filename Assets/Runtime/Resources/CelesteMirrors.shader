Shader "Celeste/Mirrors"
{
    Properties{_MainTex("Masks", 2D) = "black" {} _Sources("Reflected objects", 2D) = "black" {} _Pixel(
                   "Pixel and preshader reciprocal", Vector) = (.00260416674, .00409836043, 384, 244.000015)} SubShader
    {
        Cull Off ZWrite Off ZTest Always Pass
        {
            CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#pragma target 4.0
#include "UnityCG.cginc"
            sampler2D _MainTex, _Sources;
            float4 _Pixel;
            float4 frag(v2f_img i) : SV_Target
            {
                float2 dimensions = float2(384, 244);
                float2 pixel = _Pixel.xy;
                float2 at = (float2(i.uv.x, 1 - i.uv.y) * float2(320, 180) + 32) * pixel;
                float4 mask = tex2D(_MainTex, float2(at.x, 1 - at.y));
                // Preserve the ps_2_0 multiply/floor order for byte-packed offsets.
                precise float2 packedOffset = (mask.rg - .5) * pixel;
                packedOffset = packedOffset * 64;
                packedOffset = packedOffset * _Pixel.zw;
                float2 offset = floor(packedOffset) * pixel;
                at += offset;
                float4 source = tex2D(_Sources, float2(at.x, 1 - at.y));
                return source * mask.a * float4(.75, .75, 1.39999998, 1);
            }
            ENDCG
        }
    }
}
