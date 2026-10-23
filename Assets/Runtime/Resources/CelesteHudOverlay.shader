Shader "Celeste/HudOverlay"
{
    Properties
    {
        _MainTex("HUD", 2D) = "black"
        {
        }
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always Blend One OneMinusSrcAlpha CGINCLUDE
#include "UnityCG.cginc"
            float4x4 _DrawMatrix;
        v2f_img vertViewport(appdata_img v)
        {
            v2f_img o;
            o.pos = mul(_DrawMatrix, v.vertex);
            o.uv = v.texcoord;
            return o;
        }
        sampler2D _MainTex;
        float2 _HudUvOffset;
        float2 _HudUvScale;
        float4 frag(v2f_img i) : SV_Target
        {
            float2 uv = i.uv * _HudUvScale + _HudUvOffset;
            if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1)
                return 0;
            return tex2D(_MainTex, uv);
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
