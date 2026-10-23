Shader "Celeste/MountainOutput"
{
    Properties
    {
        _MainTex("Original image", 2D) = "white"
        {
        }
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always Blend One OneMinusSrcAlpha CGINCLUDE
#include "UnityCG.cginc"
            float4x4 _DrawMatrix;
        Texture2D _MainTex;
        SamplerState sampler_linear_clamp;
        float _Vignette;
        v2f_img vertViewport(appdata_img v)
        {
            v2f_img o;
            o.pos = mul(_DrawMatrix, v.vertex);
            o.uv = v.texcoord;
            return o;
        }
        float4 frag(v2f_img i) : SV_Target
        {
            float4 color = _MainTex.Sample(sampler_linear_clamp, i.uv);
            if (_Vignette > 0)
            {
                color *= .2;
                color.rgb *= color.a;
            }
            return color;
        }
        ENDCG
        Pass{CGPROGRAM
#pragma target 3.5
#pragma vertex vert_img
#pragma fragment frag
                 ENDCG} Pass
        {
            CGPROGRAM
#pragma target 3.5
#pragma vertex vertViewport
#pragma fragment frag
            ENDCG
        }
    }
}
