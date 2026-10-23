Shader "Celeste/TrailMask"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass{Blend One Zero CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
                 float4 vert(float4 vertex : POSITION) : SV_POSITION{return UnityObjectToClipPos(vertex);
    }
    fixed4 frag() : SV_Target
    {
        return 0;
    }
    ENDCG
}
Pass
{
    Blend DstAlpha Zero CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
        float4
        vert(float4 vertex : POSITION)
        : SV_POSITION
    {
        return UnityObjectToClipPos(vertex);
    }
    fixed4 frag() : SV_Target
    {
        return 1;
    }
    ENDCG
}
}
}
