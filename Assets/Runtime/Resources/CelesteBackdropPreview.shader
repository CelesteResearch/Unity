Shader "Celeste/BackdropPreview"
{
    Properties{_MainTex("Atlas", 2D) = "white" {} _SrcBlend("Source", Float) = 1 _DstBlend("Destination", Float) =
                   10} SubShader
    {
        Tags{"Queue" = "Transparent"
                       "RenderType" = "Transparent"} Cull Off ZWrite Off ZTest Always Blend[_SrcBlend][_DstBlend] Pass
        {
            CGPROGRAM
#pragma target 3.5
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
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
            Texture2D _MainTex;
            SamplerState point_clamp_sampler;
            float4x4 _BackdropMatrix;
            Output vert(Input v)
            {
                Output o;
                o.vertex = UnityObjectToClipPos(mul(_BackdropMatrix, v.vertex));
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }
            float4 frag(Output i) : SV_Target
            {
                return _MainTex.Sample(point_clamp_sampler, i.uv) * i.color;
            }
            ENDCG
        }
    }
}
