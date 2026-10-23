Shader "Hidden/Celeste/SceneTileGrid"
{
    Properties{_MainTex("Tile page", 2D) = "white" {}[HideInInspector] _SceneBeforeTex("Gameplay before", 2D) =
                   "white" {}[HideInInspector] _SourceTextureOriginTop("Source texture origin", Float) =
                       0 [HideInInspector] _ScenePointAtlas("Small point atlas", Float) = 0} SubShader
    {
        Tags{"Queue" = "Transparent"
                       "IgnoreProjector" = "True"} Pass
        {
            Cull Off ZWrite Off ZTest Always Blend One OneMinusSrcAlpha CGPROGRAM
#pragma target 3.5
#pragma vertex Vert
#pragma fragment Frag
#include "UnityCG.cginc"

                Texture2D _MainTex;
            SamplerState Point_Clamp_sampler;
            SamplerState Point_RepeatU_ClampV_sampler;
            SamplerState Point_ClampU_RepeatV_sampler;
            SamplerState Point_Repeat_sampler;
            float4 _MainTex_TexelSize;
            float _SourcePointAddress;
            float _ScenePointAtlas;
            float _ExcludeTiles;
            float _SourceTextureOriginTop;
            float4 _ExcludedTileRange;
            struct Input
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float2 cell : TEXCOORD1;
                float4 atlasRect : TEXCOORD2;
                fixed4 color : COLOR;
            };
            struct Output
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                float excluded : TEXCOORD1;
                float4 atlasRect : TEXCOORD2;
            };
            Output Vert(Input v)
            {
                Output o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = float2(v.uv.x, 1.0 - v.uv.y);
                o.color = v.color;
                o.atlasRect = v.atlasRect;
                o.excluded = _ExcludeTiles > 0.5 && v.cell.x >= _ExcludedTileRange.x &&
                                     v.cell.x < _ExcludedTileRange.z && v.cell.y >= _ExcludedTileRange.y &&
                                     v.cell.y < _ExcludedTileRange.w
                                 ? 1.0
                                 : 0.0;
                return o;
            }
            fixed4 Frag(Output i) : SV_Target
            {
                clip(0.5 - i.excluded);
                float2 uv = i.uv;
                if (_ScenePointAtlas > 0.5)
                {
                    uv.x = _SourcePointAddress == 1.0 || _SourcePointAddress == 3.0 ? frac(uv.x) : saturate(uv.x);
                    uv.y = _SourcePointAddress >= 2.0 ? frac(uv.y) : saturate(uv.y);
                    float2 size = i.atlasRect.zw;
                    float2 pixel = clamp(floor(uv * size), 0.0, size - 1.0);
                    float2 atlasUv = (i.atlasRect.xy + pixel + 0.5) * _MainTex_TexelSize.xy;
                    return _MainTex.Sample(Point_Clamp_sampler, atlasUv) * i.color;
                }
                if (_SourceTextureOriginTop < 0.5)
                {
                    // CPU snapshot pages are flipped on upload. Address source texels first,
                    // then flip their centers, including negative UVs and integer wrap seams.
                    uv.x = _SourcePointAddress == 1.0 || _SourcePointAddress == 3.0 ? frac(uv.x) : saturate(uv.x);
                    uv.y = _SourcePointAddress >= 2.0 ? frac(uv.y) : saturate(uv.y);
                    float2 pixel = clamp(floor(uv * _MainTex_TexelSize.zw), 0.0, _MainTex_TexelSize.zw - 1.0);
                    uv = (pixel + 0.5) * _MainTex_TexelSize.xy;
                    uv.y = 1.0 - uv.y;
                    return _MainTex.Sample(Point_Clamp_sampler, uv) * i.color;
                }
                if (_SourcePointAddress == 1.0)
                    return _MainTex.Sample(Point_RepeatU_ClampV_sampler, uv) * i.color;
                if (_SourcePointAddress == 2.0)
                    return _MainTex.Sample(Point_ClampU_RepeatV_sampler, uv) * i.color;
                if (_SourcePointAddress == 3.0)
                    return _MainTex.Sample(Point_Repeat_sampler, uv) * i.color;
                return _MainTex.Sample(Point_Clamp_sampler, uv) * i.color;
            }
            ENDCG
        }
        Pass
        {
            Cull Off ZWrite Off ZTest Always Blend SrcAlpha One, Zero One CGPROGRAM
#pragma target 3.5
#pragma vertex VertBloom
#pragma fragment FragBloom
#include "UnityCG.cginc"

                                                                     Texture2D _MainTex;
            SamplerState Linear_Clamp_sampler;
            float _SourceTextureOriginTop;
            struct InputBloom
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };
            struct OutputBloom
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };
            OutputBloom VertBloom(InputBloom v)
            {
                OutputBloom o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = float2(v.uv.x, 1.0 - v.uv.y);
                o.color = v.color;
                return o;
            }
            fixed4 FragBloom(OutputBloom i) : SV_Target
            {
                float2 uv = i.uv;
                if (_SourceTextureOriginTop < 0.5)
                    uv.y = 1.0 - uv.y;
                return _MainTex.Sample(Linear_Clamp_sampler, uv) * i.color;
            }
            ENDCG
        }
        Pass
        {
            Cull Off ZWrite Off ZTest Always Blend One One, Zero One BlendOp RevSub,
                Add CGPROGRAM
#pragma target 3.5
#pragma vertex VertDifference
#pragma fragment FragDifference
#include "UnityCG.cginc"

                    Texture2D _MainTex;
            Texture2D _SceneBeforeTex;
            SamplerState Point_Clamp_sampler;
            struct InputDifference
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            struct OutputDifference
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            OutputDifference VertDifference(InputDifference v)
            {
                OutputDifference o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = float2(v.uv.x, 1.0 - v.uv.y);
                return o;
            }
            float4 FragDifference(OutputDifference i) : SV_Target
            {
                float3 before = _SceneBeforeTex.Sample(Point_Clamp_sampler, i.uv).rgb;
                float3 after = _MainTex.Sample(Point_Clamp_sampler, i.uv).rgb;
                return float4(before - after, 0.0);
            }
            ENDCG
        }
        // Live point image runs only. Existing Mesh passes 0, 1 and 2 retain their indices and bodies.
        Pass
        {
            Cull Off ZWrite Off ZTest Always Blend One OneMinusSrcAlpha CGPROGRAM
#pragma target 4.5
#pragma vertex VertPointQuads
#pragma fragment FragPointQuads
#include "UnityCG.cginc"

                Texture2D _MainTex;
            SamplerState Point_Clamp_sampler;
            SamplerState Point_RepeatU_ClampV_sampler;
            SamplerState Point_ClampU_RepeatV_sampler;
            SamplerState Point_Repeat_sampler;
            float4 _MainTex_TexelSize;
            float _SourcePointAddress;
            float _ScenePointAtlas;
            float _ExcludeTiles;
            float _SourceTextureOriginTop;
            float4 _ExcludedTileRange;
            float4x4 _SceneWorld;

            struct PointQuad
            {
                uint4 a;
                uint4 b;
                uint4 c;
                uint4 d;
                uint4 e;
                uint4 f;
                uint4 g;
                uint4 h;
            };
            StructuredBuffer<PointQuad> _SceneQuads;

            struct PointOutput
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                float excluded : TEXCOORD1;
                float4 atlasRect : TEXCOORD2;
                nointerpolation float pointAddress : TEXCOORD3;
            };

            PointOutput VertPointQuads(uint vertexId : SV_VertexID)
            {
                uint local = vertexId % 6u;
                uint corner =
                    local == 0u ? 0u : (local == 1u || local == 4u ? 1u : (local == 2u || local == 3u ? 2u : 3u));
                PointQuad q = _SceneQuads[vertexId / 6u];
                float3 position;
                float2 uv;
                uint color;
                if (corner == 0u)
                {
                    position = asfloat(q.a.xyz);
                    color = q.a.w;
                    uv = asfloat(q.b.xy);
                }
                else if (corner == 1u)
                {
                    position = asfloat(uint3(q.b.z, q.b.w, q.c.x));
                    color = q.c.y;
                    uv = asfloat(q.c.zw);
                }
                else if (corner == 2u)
                {
                    position = asfloat(q.d.xyz);
                    color = q.d.w;
                    uv = asfloat(q.e.xy);
                }
                else
                {
                    position = asfloat(uint3(q.e.z, q.e.w, q.f.x));
                    color = q.f.y;
                    uv = asfloat(q.f.zw);
                }

                float2 cell = asfloat(q.g.xy);
                float4 atlasRect = asfloat(uint4(q.g.z, q.g.w, q.h.x, q.h.y));
                PointOutput o;
                o.vertex = mul(UNITY_MATRIX_VP, mul(_SceneWorld, float4(position, 1.0)));
                o.uv = float2(uv.x, 1.0 - uv.y);
                o.color = (float4(uint4(color, color >> 8u, color >> 16u, color >> 24u) & 255u)) / 255.0;
                o.atlasRect = atlasRect;
                o.pointAddress = q.h.z != 0u ? 0.0 : _SourcePointAddress;
                o.excluded = _ExcludeTiles > 0.5 && cell.x >= _ExcludedTileRange.x && cell.x < _ExcludedTileRange.z &&
                                     cell.y >= _ExcludedTileRange.y && cell.y < _ExcludedTileRange.w
                                 ? 1.0
                                 : 0.0;
                return o;
            }

            // Keep the point fragment's sampling and addressing decisions identical to pass 0.
            fixed4 FragPointQuads(PointOutput i) : SV_Target
            {
                clip(0.5 - i.excluded);
                float2 uv = i.uv;
                if (_ScenePointAtlas > 0.5)
                {
                    uv.x = i.pointAddress == 1.0 || i.pointAddress == 3.0 ? frac(uv.x) : saturate(uv.x);
                    uv.y = i.pointAddress >= 2.0 ? frac(uv.y) : saturate(uv.y);
                    float2 size = i.atlasRect.zw;
                    float2 pixel = clamp(floor(uv * size), 0.0, size - 1.0);
                    float2 atlasUv = (i.atlasRect.xy + pixel + 0.5) * _MainTex_TexelSize.xy;
                    return _MainTex.Sample(Point_Clamp_sampler, atlasUv) * i.color;
                }
                if (_SourceTextureOriginTop < 0.5)
                {
                    uv.x = i.pointAddress == 1.0 || i.pointAddress == 3.0 ? frac(uv.x) : saturate(uv.x);
                    uv.y = i.pointAddress >= 2.0 ? frac(uv.y) : saturate(uv.y);
                    float2 pixel = clamp(floor(uv * _MainTex_TexelSize.zw), 0.0, _MainTex_TexelSize.zw - 1.0);
                    uv = (pixel + 0.5) * _MainTex_TexelSize.xy;
                    uv.y = 1.0 - uv.y;
                    return _MainTex.Sample(Point_Clamp_sampler, uv) * i.color;
                }
                if (i.pointAddress == 1.0)
                    return _MainTex.Sample(Point_RepeatU_ClampV_sampler, uv) * i.color;
                if (i.pointAddress == 2.0)
                    return _MainTex.Sample(Point_ClampU_RepeatV_sampler, uv) * i.color;
                if (i.pointAddress == 3.0)
                    return _MainTex.Sample(Point_Repeat_sampler, uv) * i.color;
                return _MainTex.Sample(Point_Clamp_sampler, uv) * i.color;
            }
            ENDCG
        }
        Pass
        {
            Cull Off ZWrite Off ZTest Always Blend One One, One One BlendOp RevSub,
                RevSub CGPROGRAM
#pragma target 3.5
#pragma vertex VertForegroundNegative
#pragma fragment FragForegroundNegative
#include "UnityCG.cginc"

                    Texture2D _MainTex;
            Texture2D _SceneBeforeTex;
            SamplerState Point_Clamp_sampler;
            struct InputForegroundNegative
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            struct OutputForegroundNegative
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            OutputForegroundNegative VertForegroundNegative(InputForegroundNegative v)
            {
                OutputForegroundNegative o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = float2(v.uv.x, 1.0 - v.uv.y);
                return o;
            }
            float4 FragForegroundNegative(OutputForegroundNegative i) : SV_Target
            {
                float4 before = _SceneBeforeTex.Sample(Point_Clamp_sampler, i.uv);
                float4 after = _MainTex.Sample(Point_Clamp_sampler, i.uv);
                return max(before - after, 0.0);
            }
            ENDCG
        }
        Pass
        {
            Cull Off ZWrite Off ZTest Always Blend One One, One One BlendOp Add,
                Add CGPROGRAM
#pragma target 3.5
#pragma vertex VertForegroundPositive
#pragma fragment FragForegroundPositive
#include "UnityCG.cginc"

                    Texture2D _MainTex;
            Texture2D _SceneBeforeTex;
            SamplerState Point_Clamp_sampler;
            struct InputForegroundPositive
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            struct OutputForegroundPositive
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            OutputForegroundPositive VertForegroundPositive(InputForegroundPositive v)
            {
                OutputForegroundPositive o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = float2(v.uv.x, 1.0 - v.uv.y);
                return o;
            }
            float4 FragForegroundPositive(OutputForegroundPositive i) : SV_Target
            {
                float4 before = _SceneBeforeTex.Sample(Point_Clamp_sampler, i.uv);
                float4 after = _MainTex.Sample(Point_Clamp_sampler, i.uv);
                return max(after - before, 0.0);
            }
            ENDCG
        }
    }
}
