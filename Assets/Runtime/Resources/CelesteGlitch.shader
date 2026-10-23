Shader "Celeste/Glitch"
{
    Properties
    {
        _MainTex("Level", 2D) = "black"
        {
        }
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always Pass
        {
            CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Glitch, _Timer, _Seed;
            float4 frag(v2f_img i) : SV_Target
            {
                float2 at = float2(i.uv.x, 1 - i.uv.y);
                float angle = frac((at.y * 6.28318548 + _Timer) * .159154937 + .5) * 6.28318548 - 3.14159274;
                float strength = abs(max(-1, sin(angle))) * _Glitch;
                float block = max(1, floor(strength * 16));
                float noiseAngle = dot(float2(_Seed, block), float2(12.9898005, 78.2330017));
                noiseAngle = frac(noiseAngle * .159154937 + .5) * 6.28318548 - 3.14159274;
                float noise = frac(sin(noiseAngle) * 43758.5469);
                at.x = saturate(at.x + (noise * .100000001 - .0500000007) * strength);
                float2 cells = float2(320, 180) / block;
                at = floor(at * cells) / cells + block / float2(320, 180) * .5;
                float4 source = tex2D(_MainTex, float2(at.x, 1 - at.y));
                float4 tint = float4(noise, frac(noise + .340000004), frac(noise + .660000026), 1) * source.a;
                return source + strength * tint * .25;
            }
            ENDCG
        }
    }
}
