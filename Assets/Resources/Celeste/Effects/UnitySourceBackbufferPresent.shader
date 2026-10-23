Shader "Hidden/Celeste/SourceBackbufferPresent"
{
    SubShader
    {
        Pass
        {
            Cull Off ZWrite Off ZTest Always Blend Off ColorMask RGBA HLSLPROGRAM
#pragma target 3.5
#pragma vertex Vertex
#pragma fragment Fragment
#include "UnityCG.cginc"

                sampler2D _SourceBackbufferPresentTexture;
            float4 _SourceBackbufferPresentSource;

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vertex(uint id : SV_VertexID)
            {
                float2 corner = float2((id << 1) & 2, id & 2);
                Varyings result;
                result.position = float4(corner.x * 2 - 1, 1 - corner.y * 2, 0, 1);
                result.uv = _SourceBackbufferPresentSource.xy + corner * _SourceBackbufferPresentSource.zw;
                return result;
            }

            float4 Fragment(Varyings input) : SV_Target
            {
                return tex2D(_SourceBackbufferPresentTexture, input.uv);
            }
            ENDHLSL
        }
    }
}
