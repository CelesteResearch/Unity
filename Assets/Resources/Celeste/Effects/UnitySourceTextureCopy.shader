Shader "Hidden/Celeste/SourceTextureCopy"
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

                Texture2D<float4>
                    _SourceResourceCopyTexture;
            float4 _SourceResourceCopyDimensions;

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vertex(uint id : SV_VertexID)
            {
                Varyings result;
                result.uv = float2((id << 1) & 2, id & 2);
                result.position = float4(result.uv * 2 - 1, 0, 1);
#if UNITY_UV_STARTS_AT_TOP
                result.position.y = -result.position.y;
#endif
                return result;
            }

            float4 Fragment(Varyings input) : SV_Target
            {
                return _SourceResourceCopyTexture.Load(int3(input.uv * _SourceResourceCopyDimensions.xy, 0));
            }
            ENDHLSL
        }
    }
}
