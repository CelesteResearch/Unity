Shader "Celeste/Effects/9218bb5d6f35463621514f42f2c92d6cad13da01632bebca27404faf3c78fa9f"
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
	float4 m_oD0 : COLOR0;
	float4 m_oT0 : TEXCOORD0;
	float4 m_oPos : SV_Position;
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
	#define oPos output.m_oPos
	#define oD0 output.m_oD0
	#define oT0 output.m_oT0
	oPos.x = dot(v2, c0);
	oPos.y = dot(v2, c1);
	oPos.z = dot(v2, c2);
	oPos.w = dot(v2, c3);
	oD0 = v0;
	oT0.xy = v1.xy;
	#undef c0
	#undef c1
	#undef c2
	#undef c3
	#undef v0
	#undef v1
	#undef v2
	#undef oPos
	#undef oD0
	#undef oT0
	return output;
}



CelesteVSOutput CelesteVS(CelesteVSInput input)
{
    CelesteVSOutput output = CelesteSourceVS(input);
#if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3)
    // Every source attachment, including the faux backbuffer, uses logical row zero at GL storage row zero.
    output.m_oPos.y = -output.m_oPos.y;
#endif
#if defined(UNITY_REVERSED_Z)
    output.m_oPos.z = output.m_oPos.w - output.m_oPos.z;
#elif defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3)
    output.m_oPos.z = 2.0 * output.m_oPos.z - output.m_oPos.w;
#endif
    return output;
}

cbuffer CelestePSUniforms
{
	float4 _CelestePS[3];
};

Texture2D _CelestePSTex0;
SamplerState sampler_CelestePSTex0;
Texture2D _CelestePSTex1;
SamplerState sampler_CelestePSTex1;

struct CelestePSInput
{
	float4 m_oD0 : COLOR0;
	float4 m_oT0 : TEXCOORD0;
	float4 m_oPos : SV_Position;
};

struct CelestePSOutput
{
	float4 m_oC0 : SV_Target0;
};

CelestePSOutput CelestePS(CelestePSInput input)
{
	CelestePSOutput output = (CelestePSOutput) 0;
	const float4 c3 = float4(2.0, -1.0, 0.043999999, 0.078000001);
	const float4 c4 = float4(180.0, 0.300000011, 0.159154935, 0.5);
	const float4 c5 = float4(6.283185478, -3.141592739, 0.004, 0.0);
	const float4 c6 = float4(-0.00000155, -0.000021701, 0.002604166, 0.000260416);
	const float4 c7 = float4(-0.020833333, -0.125, 1.0, 0.5);
	float4 r0;
	float4 r1;
	#define c0 _CelestePS[0]
	#define c1 _CelestePS[1]
	#define c2 _CelestePS[2]
	#define t0 input.m_oT0
	#define oC0 output.m_oC0
	r0 = _CelestePSTex1.Sample(sampler_CelestePSTex1, t0.xy);
	r0.w = c4.x;
	r0.w = (t0.y * r0.w) + c1.x;
	r1.w = r0.z * c0.x;
	r0.w = (r0.w * c4.y) + -r1.w;
	r0.w = (r0.w * c4.z) + c4.w;
	r0.w = frac(r0.w);
	r0.w = (r0.w * c5.x) + c5.y;
	r1.y = sin(r0.w);
	r0.w = r1.y * c2.x;
	r0.w = r0.w * c5.z;
	r1.x = frac(-r0.z);
	r0.z = r0.z + r1.x;
	r0.x = (r0.x * c3.x) + c3.y;
	r0.y = (r0.y * c3.x) + c3.y;
	r1.y = (r0.y * c3.w) + t0.y;
	r0.x = (r0.x * c3.z) + t0.x;
	r1.x = (r0.w * r0.z) + r0.x;
	r0 = _CelestePSTex0.Sample(sampler_CelestePSTex0, r1.xy);
	oC0 = r0;
	#undef c0
	#undef c1
	#undef c2
	#undef t0
	#undef oC0
	return output;
}


            ENDHLSL
        }
    }
}
