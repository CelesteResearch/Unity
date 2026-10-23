Shader "Celeste/Effects/52af9042cad5f6a9dbd4b7560ad26ec0eee6387a5c8f467311da0245887dbd6d"
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
	float4 _CelesteVS[8];
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
	float4 r0;
	#define c0 _CelesteVS[0]
	#define c1 _CelesteVS[1]
	#define c2 _CelesteVS[2]
	#define c3 _CelesteVS[3]
	#define c4 _CelesteVS[4]
	#define c5 _CelesteVS[5]
	#define c6 _CelesteVS[6]
	#define c7 _CelesteVS[7]
	#define v0 input.m_v0
	#define v1 input.m_v1
	#define v2 input.m_v2
	#define o0 output.m_o0
	#define o1 output.m_o1
	#define o2 output.m_o2
	r0.x = dot(v2, c4);
	r0.y = dot(v2, c5);
	r0.z = dot(v2, c6);
	r0.w = dot(v2, c7);
	o2.x = dot(r0, c0);
	o2.y = dot(r0, c1);
	o2.z = dot(r0, c2);
	o2.w = dot(r0, c3);
	o0 = v0;
	o1.xy = v1.xy;
	#undef c0
	#undef c1
	#undef c2
	#undef c3
	#undef c4
	#undef c5
	#undef c6
	#undef c7
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
	float4 _CelestePS[2];
};

Texture2D _CelestePSTex0;
SamplerState sampler_CelestePSTex0;

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
	const float4 c2 = float4(6.0, 0.0, 0.007142857, -3.0);
	const float4 c3 = float4(280.0, 0.003571428, -280.0, -0.003571428);
	const float4 c4 = float4(0.003571428, 0.5, -0.5, -1.0);
	const float4 c5 = float4(2.0, 1.0, 0.400000005, 0.900000023);
	const float4 c6 = float4(0.400000005, 1.399999979, 1.066666722, 0.733333349);
	float4 r0;
	float4 r1;
	#define c0 _CelestePS[0]
	#define c1 _CelestePS[1]
	#define v0 input.m_o1
	#define oC0 output.m_oC0
	#define vPos input.m_o2
	#define FLT_MAX 1e38
	r0.xy = c0.xy + vPos.xy;
	r0.xy = r0.xy * c2.xx;
	r0.x = dot(r0.xy, r0.xy) + c2.y;
	r0.x = (r0.x == 0.0) ? FLT_MAX : rsqrt(abs(r0.x));
	r0.x = (r0.x == 0.0) ? FLT_MAX : 1.0 / r0.x;
	r0.x = r0.x + c1.x;
	r0.yz = ((r0.x >= 0.0) ? c3.xy : c3.zw);
	r0.x = r0.x * r0.z;
	r0.x = frac(r0.x);
	r0.x = r0.y * r0.x;
	r0.y = (r0.x * c4.x) + c4.z;
	r0.y = (r0.y * -c5.x) + c5.y;
	r0.z = r0.x * c2.z;
	r0.x = (r0.x * -c4.x) + c4.y;
	r0.x = ((r0.x >= 0.0) ? r0.z : r0.y);
	r0.xyz = (r0.xxx * c6.xxx) + c6.yzw;
	r0.xyz = frac(r0.xyz);
	r0.xyz = (r0.xyz * c2.xxx) + c2.www;
	r0.xyz = saturate(abs(r0.xyz) + c4.www);
	r0.xyz = r0.xyz + c4.www;
	r0.xyz = (r0.xyz * c5.zzz) + c5.yyy;
	r0.xyz = r0.xyz * c5.www;
	r1 = _CelestePSTex0.Sample(sampler_CelestePSTex0, v0.xy);
	r0.w = -c4.w;
	oC0 = ((-r1.w >= 0.0) ? c2.yyyy : r0);
	#undef c0
	#undef c1
	#undef v0
	#undef oC0
	#undef vPos
	return output;
}


            ENDHLSL
        }
    }
}
