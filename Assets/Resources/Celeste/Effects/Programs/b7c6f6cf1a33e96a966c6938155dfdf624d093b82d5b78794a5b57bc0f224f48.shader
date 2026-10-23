Shader "Celeste/Effects/b7c6f6cf1a33e96a966c6938155dfdf624d093b82d5b78794a5b57bc0f224f48"
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
        _ColorMask1 ("_ColorMask1", Integer) = 15
        _ColorMask2 ("_ColorMask2", Integer) = 15
        _ColorMask3 ("_ColorMask3", Integer) = 15
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
            ColorMask [_ColorMask1] 1
            ColorMask [_ColorMask2] 2
            ColorMask [_ColorMask3] 3
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
	float4 _CelesteVS[6];
};

struct CelesteVSInput
{
	float4 m_v0 : POSITION0;
	float4 m_v1 : COLOR0;
	float4 m_v2 : TEXCOORD0;
	float4 m_v3 : NORMAL0;
};

struct CelesteVSOutput
{
	float4 m_o1 : COLOR0;
	float4 m_o2 : TEXCOORD0;
	float4 m_o3 : NORMAL0;
	float4 m_o4 : TEXCOORD1;
	float4 m_o5 : TEXCOORD2;
	float4 m_o6 : TEXCOORD3;
	float4 m_o0 : SV_Position;
};

CelesteVSOutput CelesteSourceVS(CelesteVSInput input)
{
	CelesteVSOutput output = (CelesteVSOutput) 0;
	float4 r0;
	#define c0 _CelesteVS[0]
	#define c1 _CelesteVS[1]
	#define c2 _CelesteVS[2]
	#define c3 _CelesteVS[3]
	#define c4 _CelesteVS[4]
	#define c5 _CelesteVS[5]
	#define v0 input.m_v0
	#define v1 input.m_v1
	#define v2 input.m_v2
	#define v3 input.m_v3
	#define o0 output.m_o0
	#define o1 output.m_o1
	#define o2 output.m_o2
	#define o3 output.m_o3
	#define o4 output.m_o4
	#define o5 output.m_o5
	#define o6 output.m_o6
	o0.x = dot(v0, c0);
	o0.y = dot(v0, c1);
	r0.z = dot(v0, c2);
	r0.w = dot(v0, c3);
	o0.zw = r0.zw;
	o4.xy = r0.zw;
	o1 = v1;
	o2.xy = v2.xy;
	o3.xyz = v3.xyz;
	o5.xy = c4.xy;
	o6.x = c5.x;
	#undef c0
	#undef c1
	#undef c2
	#undef c3
	#undef c4
	#undef c5
	#undef v0
	#undef v1
	#undef v2
	#undef v3
	#undef o0
	#undef o1
	#undef o2
	#undef o3
	#undef o4
	#undef o5
	#undef o6
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
	float4 _CelestePS[11];
};

Texture2D _CelestePSTex0;
SamplerState sampler_CelestePSTex0;

struct CelestePSInput
{
	float4 m_o1 : COLOR0;
	float4 m_o2 : TEXCOORD0;
	float4 m_o3 : NORMAL0;
	float4 m_o4 : TEXCOORD1;
	float4 m_o5 : TEXCOORD2;
	float4 m_o6 : TEXCOORD3;
	float4 m_o0 : SV_Position;
};

struct CelestePSOutput
{
	float4 m_oC0 : SV_Target0;
	float4 m_oC1 : SV_Target1;
	float4 m_oC2 : SV_Target2;
};

CelestePSOutput CelestePS(CelestePSInput input)
{
	CelestePSOutput output = (CelestePSOutput) 0;
	const float4 c11 = float4(-2.0, 3.0, 0.0, 1.0);
	const float4 c12 = float4(0.5, 0.0, 0.0, 0.0);
	float4 r0;
	float4 r1;
	float4 r2;
	#define c0 _CelestePS[0]
	#define c1 _CelestePS[1]
	#define c2 _CelestePS[2]
	#define c3 _CelestePS[3]
	#define c4 _CelestePS[4]
	#define c5 _CelestePS[5]
	#define c6 _CelestePS[6]
	#define c7 _CelestePS[7]
	#define c8 _CelestePS[8]
	#define c9 _CelestePS[9]
	#define c10 _CelestePS[10]
	#define v0 input.m_o1
	#define v1 input.m_o2
	#define v2 input.m_o3
	#define v3 input.m_o4
	#define v4 input.m_o5
	#define oC0 output.m_oC0
	#define oC1 output.m_oC1
	#define oC2 output.m_oC2
	#define FLT_MAX 1e38
	r0.x = dot(v2.xyz, c0.xyz);
	r0.y = dot(v2.xyz, c1.xyz);
	r0.z = dot(v2.xyz, c2.xyz);
	r0.w = dot(r0.xyz, r0.xyz);
	r0.w = (r0.w == 0.0) ? FLT_MAX : rsqrt(abs(r0.w));
	r1.x = (r0.z * -r0.w) + -c7.x;
	r1.x = saturate(r1.x * c4.x);
	r1.y = (r1.x * c11.x) + c11.y;
	r1.x = r1.x * r1.x;
	r1.x = r1.y * r1.x;
	r1.x = r1.x * c8.x;
	r1.z = c11.z;
	r1.x = ((-c8.x >= 0.0) ? r1.z : r1.x);
	r1.y = r0.z * r0.w;
	r0.xyz = (r0.xyz * r0.www) + c11.www;
	oC2.xyz = r0.xyz * c12.xxx;
	r0.x = -r1.y * c3.x;
	r2.x = c3.x;
	r0.y = (r2.x * -r1.y) + c9.x;
	r0.x = ((r0.x >= 0.0) ? r0.y : c9.x);
	r1.y = min(c10.x, r0.x);
	r0.x = r1.x + r1.y;
	r1 = _CelestePSTex0.Sample(sampler_CelestePSTex0, v1.xy);
	r1 = r1 * v0;
	r1 = r1 * c6;
	oC0.xyz = r0.xxx * r1.xyz;
	oC0.w = r1.w;
	r0.x = (v3.y == 0.0) ? FLT_MAX : 1.0 / v3.y;
	oC1.x = r0.x * v3.x;
	oC1.yz = v4.xy;
	oC1.w = c5.x;
	oC2.w = c11.w;
	#undef c0
	#undef c1
	#undef c2
	#undef c3
	#undef c4
	#undef c5
	#undef c6
	#undef c7
	#undef c8
	#undef c9
	#undef c10
	#undef v0
	#undef v1
	#undef v2
	#undef v3
	#undef v4
	#undef oC0
	#undef oC1
	#undef oC2
	return output;
}


            ENDHLSL
        }
    }
}
