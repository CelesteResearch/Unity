Shader "Celeste/Effects/533bfa6f5b876a179c667b8147b6f46a05e7a8280d86a388ccf45975da8d4dec"
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
	float4 m_v0 : POSITION0;
	float4 m_v1 : TEXCOORD0;
};

struct CelesteVSOutput
{
	float4 m_o1 : TEXCOORD0;
	float4 m_o2 : TEXCOORD1;
	float4 m_o0 : SV_Position;
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
	#define o0 output.m_o0
	#define o1 output.m_o1
	#define o2 output.m_o2
	o0.x = dot(v0, c0);
	o0.y = dot(v0, c1);
	o0.z = dot(v0, c2);
	o0.w = dot(v0, c3);
	o1.xy = v1.xy;
	o2 = v0;
	#undef c0
	#undef c1
	#undef c2
	#undef c3
	#undef v0
	#undef v1
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
    output.m_o0.y = -output.m_o0.y;
#endif
#if defined(UNITY_REVERSED_Z)
    output.m_o0.z = output.m_o0.w - output.m_o0.z;
#elif defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3)
    output.m_o0.z = 2.0 * output.m_o0.z - output.m_o0.w;
#endif
    return output;
}

cbuffer CelestePSUniforms
{
	float4 _CelestePS[1];
};

Texture2D _CelestePSTex0;
SamplerState sampler_CelestePSTex0;

struct CelestePSInput
{
	float4 m_o1 : TEXCOORD0;
	float4 m_o2 : TEXCOORD1;
	float4 m_o0 : SV_Position;
};

struct CelestePSOutput
{
	float4 m_oC0 : SV_Target0;
};

CelestePSOutput CelestePS(CelestePSInput input)
{
	CelestePSOutput output = (CelestePSOutput) 0;
	const float4 c1 = float4(0.055555556, -0.25, 0.0, 0.0);
	const float4 c2 = float4(1.0, -1.0, 0.0, 1.333333372);
	float4 r0;
	float4 r1;
	#define c0 _CelestePS[0]
	#define v0 input.m_o1
	#define v1 input.m_o2
	#define oC0 output.m_oC0
	#define FLT_MAX 1e38
	r0.x = dot(v1.xz, v1.xz) + c2.z;
	r0.x = (r0.x == 0.0) ? FLT_MAX : rsqrt(abs(r0.x));
	r0.x = (r0.x == 0.0) ? FLT_MAX : 1.0 / r0.x;
	r0.x = (r0.x * c1.x) + c1.y;
	r0.x = r0.x * c2.w;
	r1.x = min(r0.x, c2.x);
	r0.xy = (v0.xy * c2.xy) + c2.zx;
	r0 = _CelestePSTex0.Sample(sampler_CelestePSTex0, r0.xy);
	r1.yzw = -r0.xyz + c0.xyz;
	r1.yzw = r1.xxx * r1.yzw;
	r1.xyz = ((r1.x >= 0.0) ? r1.yzw : c2.zzz);
	oC0.xyz = r0.xyz + r1.xyz;
	oC0.w = c2.x;
	#undef c0
	#undef v0
	#undef v1
	#undef oC0
	return output;
}


            ENDHLSL
        }
    }
}
