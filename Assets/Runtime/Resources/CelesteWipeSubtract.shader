Shader "Celeste/WipeSubtract"
{
    Properties
    {
        _MainTex("Wipe mask", 2D) = "black"
        {
        }
    }
    SubShader
    {
        Tags{"Queue" = "Overlay"
                       "RenderType" = "Transparent"} Cull Off ZWrite Off ZTest Always Blend One One,
            One One BlendOp RevSub,
            Add CGINCLUDE
#include "UnityCG.cginc"
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
        sampler2D _MainTex;
        float4x4 _DrawMatrix;
        Output vert(Input v)
        {
            Output o;
            o.vertex = UnityObjectToClipPos(v.vertex);
            o.uv = v.uv;
            return o;
        }
        Output vertViewport(Input v)
        {
            Output o;
            o.vertex = mul(_DrawMatrix, v.vertex);
            o.uv = v.uv;
            return o;
        }
        fixed4 frag(Output i) : SV_Target
        {
            return tex2D(_MainTex, i.uv);
        }
        ENDCG
        Pass{CGPROGRAM
#pragma vertex vert
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
