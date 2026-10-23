Shader "Celeste/LightSubtract"
{
    Properties
    {
        _MainTex("Blurred lighting", 2D) = "black"
        {
        }
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always Blend One One BlendOp RevSub ColorMask RGB Pass
        {
            CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#pragma target 3.0
#include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Alpha;
            float4 frag(v2f_img i) : SV_Target
            {
                float2 at = floor(float2(i.uv.x, 1 - i.uv.y) * float2(320, 180));
                int x = (int)at.x % 4, y = (int)at.y % 4;
                // Dither.xnb InvertDither: this is the original matrix, not a
                // generic Bayer approximation or a frame-varying noise texture.
                float4 row = y == 0   ? float4(.0625, .8125, .25, 1)
                             : y == 1 ? float4(.5625, .3125, .75, .5)
                             : y == 2 ? float4(.1875, .9375, .125, .875)
                                      : float4(.6875, .4375, .625, .375);
                float threshold = x == 0 ? row.x : x == 1 ? row.y : x == 2 ? row.z : row.w;
                float noise = (threshold - .5) * .00390625;
                float4 light = tex2D(_MainTex, i.uv);
                return float4(1 - light.rgb, light.a) * _Alpha + noise;
            }
            ENDCG
        }
    }
}
