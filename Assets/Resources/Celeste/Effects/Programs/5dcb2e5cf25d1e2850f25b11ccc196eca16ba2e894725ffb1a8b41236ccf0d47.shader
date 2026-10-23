Shader "Celeste/Effects/5dcb2e5cf25d1e2850f25b11ccc196eca16ba2e894725ffb1a8b41236ccf0d47"
{
    Properties
    {
        _SrcBlend ("_SrcBlend", Integer) = 1
        _DstBlend ("_DstBlend", Integer) = 0
        _SrcBlendAlpha ("_SrcBlendAlpha", Integer) = 1
        _DstBlendAlpha ("_DstBlendAlpha", Integer) = 0
        _BlendOp ("_BlendOp", Integer) = 0
        _BlendOpAlpha ("_BlendOpAlpha", Integer) = 0
        _Cull ("_Cull", Integer) = 0
        _ZWrite ("_ZWrite", Integer) = 0
        _ZTest ("_ZTest", Integer) = 8
        _ColorMask ("_ColorMask", Integer) = 15
        _StencilRef ("_StencilRef", Integer) = 0
        _StencilReadMask ("_StencilReadMask", Integer) = 255
        _StencilWriteMask ("_StencilWriteMask", Integer) = 0
        _StencilCompFront ("_StencilCompFront", Integer) = 8
        _StencilCompBack ("_StencilCompBack", Integer) = 8
        _StencilPassFront ("_StencilPassFront", Integer) = 0
        _StencilPassBack ("_StencilPassBack", Integer) = 0
        _StencilFailFront ("_StencilFailFront", Integer) = 0
        _StencilFailBack ("_StencilFailBack", Integer) = 0
        _StencilZFailFront ("_StencilZFailFront", Integer) = 0
        _StencilZFailBack ("_StencilZFailBack", Integer) = 0
        _DepthBias ("Depth bias", Float) = 0
        _SlopeScaleBias ("Slope scale bias", Float) = 0
        _CelestePSTex0 ("_CelestePSTex0", 2D) = "white" {}
        _CelestePSTex1 ("_CelestePSTex1", 2D) = "white" {}
        _CelestePSTex2 ("_CelestePSTex2", 2D) = "white" {}
    }
    SubShader
    {
        Pass
        {
            Blend [_SrcBlend] [_DstBlend], [_SrcBlendAlpha] [_DstBlendAlpha]
            BlendOp [_BlendOp], [_BlendOpAlpha]
            Cull [_Cull]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            ColorMask [_ColorMask]
            Offset [_SlopeScaleBias], [_DepthBias]
            Stencil
            {
                Ref [_StencilRef]
                ReadMask [_StencilReadMask]
                WriteMask [_StencilWriteMask]
                CompFront [_StencilCompFront]
                CompBack [_StencilCompBack]
                PassFront [_StencilPassFront]
                PassBack [_StencilPassBack]
                FailFront [_StencilFailFront]
                FailBack [_StencilFailBack]
                ZFailFront [_StencilZFailFront]
                ZFailBack [_StencilZFailBack]
            }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CelesteVS
            #pragma fragment CelestePS
            #include "HLSLSupport.cginc"
            // Keep source SM4 target semantics; the include also defines legacy DX9 aliases.
            #undef SV_Target0
cbuffer CelesteVSUniforms
{
	float4 _CelesteVS[4];
};

struct CelesteVSInput
{
	float4 m_v0 : COLOR0;
	float4 m_v1 : TEXCOORD0;
	float4 m_v2 : POSITION0;
};

struct CelesteVSOutput
{
	float4 m_o0 : COLOR0;
	float4 m_o1 : TEXCOORD0;
	float4 m_o2 : SV_Position;
};

CelesteVSOutput CelesteSourceVS(CelesteVSInput input)
{
	CelesteVSOutput output = (CelesteVSOutput) 0;
	#define c0 _CelesteVS[0]
	#define c1 _CelesteVS[1]
	#define c2 _CelesteVS[2]
	#define c3 _CelesteVS[3]
	#define v0 input.m_v0
	#define v1 input.m_v1
	#define v2 input.m_v2
	#define o0 output.m_o0
	#define o1 output.m_o1
	#define o2 output.m_o2
	o2.x = dot(v2, c0);
	o2.y = dot(v2, c1);
	o2.z = dot(v2, c2);
	o2.w = dot(v2, c3);
	o0 = v0;
	o1.xy = v1.xy;
	#undef c0
	#undef c1
	#undef c2
	#undef c3
	#undef v0
	#undef v1
	#undef v2
	#undef o0
	#undef o1
	#undef o2
	return output;
}



CelesteVSOutput CelesteVS(CelesteVSInput input)
{
    CelesteVSOutput output = CelesteSourceVS(input);
#if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3)
    // Every source attachment, including the faux backbuffer, uses logical row zero at GL storage row zero.
    output.m_o2.y = -output.m_o2.y;
#endif
#if defined(UNITY_REVERSED_Z)
    output.m_o2.z = output.m_o2.w - output.m_o2.z;
#elif defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3)
    output.m_o2.z = 2.0 * output.m_o2.z - output.m_o2.w;
#endif
    return output;
}

cbuffer CelestePSUniforms
{
	float4 _CelestePS[7];
};

Texture2D _CelestePSTex0;
SamplerState sampler_CelestePSTex0;
Texture2D _CelestePSTex1;
SamplerState sampler_CelestePSTex1;
Texture2D _CelestePSTex2;
SamplerState sampler_CelestePSTex2;

struct CelestePSInput
{
	float4 m_o0 : COLOR0;
	float4 m_o1 : TEXCOORD0;
	float4 m_o2 : SV_Position;
};

struct CelestePSOutput
{
	float4 m_oC0 : SV_Target0;
};

CelestePSOutput CelestePS(CelestePSInput input)
{
	CelestePSOutput output = (CelestePSOutput) 0;
	const float4 c7 = float4(2.0, -1.0, 0.5, -0.01);
	const float4 c8 = float4(100.0, -2.0, 3.0, 50.0);
	const float4 c9 = float4(0.25, 0.002499999, 0.0, 1.0);
	const float4 c10 = float4(1.0, 0.0, 2.0, 4.0);
	const float4 c11 = float4(-0.100000001, 0.159154935, 0.5, 10.0);
	const float4 c12 = float4(6.283185478, -3.141592739, -1.0, 1.0);
	const float4 c13 = float4(-0.000000252, 0.00002476, -0.001388839, 0.041666641);
	float4 r0;
	float4 r1;
	float4 r2;
	float4 r3;
	float4 r4;
	float4 r5;
	#define c0 _CelestePS[0]
	#define c1 _CelestePS[1]
	#define c2 _CelestePS[2]
	#define c3 _CelestePS[3]
	#define c4 _CelestePS[4]
	#define c5 _CelestePS[5]
	#define c6 _CelestePS[6]
	#define v0 input.m_o0
	#define v1 input.m_o1
	#define oC0 output.m_oC0
	r0.xy = c1.xy + v1.xy;
	r1 = _CelestePSTex2.Sample(sampler_CelestePSTex2, r0.xy);
	r0 = _CelestePSTex1.Sample(sampler_CelestePSTex1, r0.xy);
	r0.yzw = (r1.xyz * c7.xxx) + c7.yyy;
	r1 = _CelestePSTex2.Sample(sampler_CelestePSTex2, v1.xy);
	r1.xyz = (r1.xyz * c7.xxx) + c7.yyy;
	r2.xyz = -r0.yzw + r1.xyz;
	r0.y = dot(r1.xyz, r0.yzw);
	r0.z = dot(r2.xyz, c5.xyz);
	r0.z = r0.z + -c7.w;
	r0.z = saturate(r0.z * c8.w);
	r0.w = (r0.z * c8.y) + c8.z;
	r0.z = r0.z * r0.z;
	r0.z = saturate(r0.z * r0.w);
	r2.xy = c0.xy + v1.xy;
	r3 = _CelestePSTex2.Sample(sampler_CelestePSTex2, r2.xy);
	r2 = _CelestePSTex1.Sample(sampler_CelestePSTex1, r2.xy);
	r2.yzw = (r3.xyz * c7.xxx) + c7.yyy;
	r3.xyz = r1.xyz + -r2.yzw;
	r0.w = dot(r1.xyz, r2.yzw);
	r0.yw = -r0.yw + -c7.yy;
	r1.w = dot(r3.xyz, c5.xyz);
	r1.w = r1.w + -c7.w;
	r1.w = saturate(r1.w * c8.w);
	r2.y = (r1.w * c8.y) + c8.z;
	r1.w = r1.w * r1.w;
	r1.w = saturate(r1.w * r2.y);
	r3 = _CelestePSTex1.Sample(sampler_CelestePSTex1, v1.xy);
	r2.y = r2.x + -r3.x;
	r2.x = -r2.x + r3.x;
	r2.y = (r2.y * c9.x) + c9.y;
	r2.z = ((-r2.y >= 0.0) ? c9.z : c9.w);
	r2.y = ((r2.y >= 0.0) ? -c9.z : -c9.w);
	r2.y = r2.y + r2.z;
	r4.x = max(r2.y, c9.z);
	r0.w = r0.w * r4.x;
	r0.w = r1.w * r0.w;
	r1.w = r0.x + -r3.x;
	r0.x = -r0.x + r3.x;
	r0.x = r0.x + r2.x;
	r1.w = (r1.w * c9.x) + c9.y;
	r2.x = ((-r1.w >= 0.0) ? c9.z : c9.w);
	r1.w = ((r1.w >= 0.0) ? -c9.z : -c9.w);
	r1.w = r1.w + r2.x;
	r2.x = max(r1.w, c9.z);
	r0.y = r0.y * r2.x;
	r0.y = (r0.y * r0.z) + r0.w;
	r0.zw = c3.xy + v1.xy;
	r2 = _CelestePSTex2.Sample(sampler_CelestePSTex2, r0.zw);
	r4 = _CelestePSTex1.Sample(sampler_CelestePSTex1, r0.zw);
	r2.xyz = (r2.xyz * c7.xxx) + c7.yyy;
	r4.yzw = r1.xyz + -r2.xyz;
	r0.z = dot(r1.xyz, r2.xyz);
	r0.z = -r0.z + -c7.y;
	r0.w = dot(r4.yzw, c5.xyz);
	r0.w = r0.w + -c7.w;
	r0.w = saturate(r0.w * c8.w);
	r1.w = (r0.w * c8.y) + c8.z;
	r0.w = r0.w * r0.w;
	r0.w = saturate(r0.w * r1.w);
	r1.w = -r3.x + r4.x;
	r2.x = r3.x + -r4.x;
	r1.w = (r1.w * c9.x) + c9.y;
	r2.y = ((-r1.w >= 0.0) ? c9.z : c9.w);
	r1.w = ((r1.w >= 0.0) ? -c9.z : -c9.w);
	r1.w = r1.w + r2.y;
	r2.y = max(r1.w, c9.z);
	r0.z = r0.z * r2.y;
	r0.y = (r0.z * r0.w) + r0.y;
	r0.zw = c2.xy + v1.xy;
	r4 = _CelestePSTex2.Sample(sampler_CelestePSTex2, r0.zw);
	r5 = _CelestePSTex1.Sample(sampler_CelestePSTex1, r0.zw);
	r2.yzw = (r4.xyz * c7.xxx) + c7.yyy;
	r4.xyz = r1.xyz + -r2.yzw;
	r0.z = dot(r1.xyz, r2.yzw);
	r0.z = -r0.z + -c7.y;
	r0.w = dot(r4.xyz, c5.xyz);
	r0.w = r0.w + -c7.w;
	r0.w = saturate(r0.w * c8.w);
	r1.x = (r0.w * c8.y) + c8.z;
	r0.w = r0.w * r0.w;
	r0.w = saturate(r0.w * r1.x);
	r1.x = -r3.x + r5.x;
	r1.y = r3.x + -r5.x;
	r0.x = r0.x + r1.y;
	r0.x = r2.x + r0.x;
	r1.x = (r1.x * c9.x) + c9.y;
	r1.y = ((-r1.x >= 0.0) ? c9.z : c9.w);
	r1.x = ((r1.x >= 0.0) ? -c9.z : -c9.w);
	r1.x = r1.x + r1.y;
	r2.x = max(r1.x, c9.z);
	r0.z = r0.z * r2.x;
	r0.y = (r0.z * r0.w) + r0.y;
	r0.y = r0.y + c11.x;
	r0.z = r3.z + -c7.y;
	r0.y = ((r0.y >= 0.0) ? r0.z : -c7.y);
	r0.zw = c4.xy + v1.xy;
	r1 = _CelestePSTex1.Sample(sampler_CelestePSTex1, r0.zw);
	r0.z = -r1.x + r3.x;
	r0.x = (r0.z * c7.z) + r0.x;
	r1.xy = -c4.xy + v1.xy;
	r1 = _CelestePSTex1.Sample(sampler_CelestePSTex1, r1.xy);
	r0.w = -r1.x + r3.x;
	r0.x = (r0.w * c7.z) + r0.x;
	r0.x = (r0.z * c7.z) + r0.x;
	r0.x = (r0.w * c7.z) + r0.x;
	r0.x = r0.x + c7.w;
	r0.x = saturate(r0.x * c8.x);
	r0.z = (r0.x * c8.y) + c8.z;
	r0.x = r0.x * r0.x;
	r0.x = r0.x * r0.z;
	r0.z = (r0.x * -r3.y) + -c7.y;
	r0.x = ((-r0.x >= 0.0) ? r0.y : r0.z);
	r1 = _CelestePSTex0.Sample(sampler_CelestePSTex0, v1.xy);
	r1.xyz = r0.xxx * r1.xyz;
	r0.x = -v1.y + v1.x;
	r0.x = (r0.x * c11.y) + c11.z;
	r0.x = frac(r0.x);
	r0.x = (r0.x * c12.x) + c12.y;
	r2.xy = float2(cos(r0.x), sin(r0.x));
	r0.xyz = r2.yxy * c12.zwz;
	r0.xyz = r0.xyz * v1.yyy;
	r0.xyz = (v1.xxx * r2.xyx) + r0.xyz;
	r2.zw = c9.zw;
	r2.xyz = r2.zww * c6.xxx;
	r0.xyz = (r0.xyz * c11.www) + r2.xyz;
	r2.x = c6.x;
	r2.xyz = (r2.xxx * c10.xyy) + c10.yzw;
	r0.xyz = r0.xyz + r2.xyz;
	r0.xyz = (r0.xyz * c11.yyy) + c11.zzz;
	r0.xyz = frac(r0.xyz);
	r0.xyz = (r0.xyz * c12.xxx) + c12.yyy;
	r0.xyz = r0.xyz * r0.xyz;
	r2.xyz = (r0.xyz * c13.xxx) + c13.yyy;
	r2.xyz = (r0.xyz * r2.xyz) + c13.zzz;
	r2.xyz = (r0.xyz * r2.xyz) + c13.www;
	r2.xyz = (r0.xyz * r2.xyz) + -c7.zzz;
	r0.xyz = (r0.xyz * r2.xyz) + -c7.yyy;
	r0.xyz = (r0.xyz * c7.zzz) + c7.zzz;
	r0.w = -c7.y;
	r0 = (r1 * -v0) + r0;
	r1 = r1 * v0;
	r0 = (r3.wwww * r0) + r1;
	oC0 = ((-r3.w >= 0.0) ? r1 : r0);
	#undef c0
	#undef c1
	#undef c2
	#undef c3
	#undef c4
	#undef c5
	#undef c6
	#undef v0
	#undef v1
	#undef oC0
	return output;
}


            ENDHLSL
        }
    }
}
