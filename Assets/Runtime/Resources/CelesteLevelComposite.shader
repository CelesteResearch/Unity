Shader "Celeste/LevelComposite"
{
    Properties
    {
        _MainTex("Level", 2D) = "black"
        {
        }
        _GradeFrom("Grade from", 2D) = "white"
        {
        }
        _GradeTo("Grade to", 2D) = "white"
        {
        }
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always CGINCLUDE
#include "UnityCG.cginc"
            float4x4 _DrawMatrix;
        v2f_img vertViewport(appdata_img v)
        {
            v2f_img o;
            o.pos = mul(_DrawMatrix, v.vertex);
            o.uv = v.texcoord;
            return o;
        }
        sampler2D _MainTex, _GradeFrom, _GradeTo;
        float2 _Origin, _Padding;
        float _Scale, _Mirror, _GradeEnabled, _GradePercent;
        float3 Grade(sampler2D lut, float3 source)
        {
            float blue = source.b * 16;
            float slice = min(floor(blue), 15);
            float red = source.r * .05859375 + .001953125;
            float green = 1 - (source.g + .03125);
            float3 a = tex2D(lut, float2(slice * .0625 + red, green)).rgb;
            float3 b = tex2D(lut, float2(min(slice + 1, 15) * .0625 + red, green)).rgb;
            return lerp(a, b, frac(blue));
        }
        float4 frag(v2f_img i) : SV_Target
        {
            float2 dimensions = float2(320, 180);
            float2 at =
                ((float2(i.uv.x, 1 - i.uv.y) * dimensions - _Origin - _Padding) / _Scale + _Origin) / dimensions;
            if (any(at < 0) || any(at >= 1))
                return float4(0, 0, 0, 1);
            if (_Mirror > 0)
                at.x = 1 - at.x;
            float4 source = tex2D(_MainTex, float2(at.x, 1 - at.y));
            if (_GradeEnabled > 0)
                source.rgb = lerp(Grade(_GradeFrom, source.rgb), Grade(_GradeTo, source.rgb), _GradePercent) * source.a;
            // The original draws over a freshly cleared opaque black backbuffer.
            return float4(source.rgb, 1);
        }
        ENDCG
        Pass{CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
                 ENDCG} Pass
        {
            Tags{"LightMode" = "CelesteViewport"} CGPROGRAM
#pragma vertex vertViewport
#pragma fragment frag
                ENDCG
        }
    }
}
