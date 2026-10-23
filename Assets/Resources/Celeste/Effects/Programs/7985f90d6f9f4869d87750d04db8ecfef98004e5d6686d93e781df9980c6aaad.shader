Shader "Celeste/Effects/7985f90d6f9f4869d87750d04db8ecfef98004e5d6686d93e781df9980c6aaad"
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
	float4 _CelesteVS[4];
};

struct CelesteVSInput
{
	float4 m_v0 : POSITION0;
	float4 m_v1 : COLOR0;
};

struct CelesteVSOutput
{
	float4 m_o1 : COLOR0;
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
	o0.x = dot(v0, c0);
	o0.y = dot(v0, c1);
	o0.z = dot(v0, c2);
	o0.w = dot(v0, c3);
	o1 = v1;
	#undef c0
	#undef c1
	#undef c2
	#undef c3
	#undef v0
	#undef v1
	#undef o0
	#undef o1
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
	float4 _CelestePS[12];
};

Texture2D _CelestePSTex0;
SamplerState sampler_CelestePSTex0;

struct CelestePSInput
{
	float4 m_o1 : COLOR0;
	float4 m_o0 : SV_Position;
};

struct CelestePSOutput
{
	float4 m_oC0 : SV_Target0;
	float4 m_oC1 : SV_Target1;
};

CelestePSOutput CelestePS(CelestePSInput input)
{
	CelestePSOutput output = (CelestePSOutput) 0;
	const float4 c12 = float4(1.0, 0.5, 0.0, 16.0);
	const float4 c13 = float4(0.5, 0.649999974, 0.349999993, 0.0);
	const float4 c14 = float4(-0.25, 4.0, 0.0, 0.0);
	const float4 c15 = float4(2.0, -1.0, -0.449999989, 0.0);
	const float4 c16 = float4(-1.0, -0.98000002, -0.930000006, -0.819999992);
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
	#define c11 _CelestePS[11]
	#define v0 input.m_o1
	#define oC0 output.m_oC0
	#define oC1 output.m_oC1
	#define vPos input.m_o0
	r0.x = (v0.y * c15.x) + c15.y;
	r0.y = abs(r0.x) + c14.x;
	r0.x = r0.x * c12.w;
	r0.y = saturate(r0.y * c14.y);
	r1.xy = c11.xy + vPos.xy;
	r0.z = r1.y + -c10.x;
	r0.y = (r0.x * -r0.y) + r0.z;
	r0.x = r1.x + c4.x;
	r0.xy = r0.xy * c5.xy;
	r0 = _CelestePSTex0.Sample(sampler_CelestePSTex0, r0.xy);
	r0.xz = (v0.xx * c13.xy) + c13.xz;
	r0.x = r0.x * r0.y;
	r0.x = r0.x * c9.y;
	r1.z = r1.y + c6.x;
	r0.yw = frac(r1.xz);
	r0.yw = -r0.yw + r1.xz;
	r0.yw = r0.yw + c12.yy;
	r0.yw = r0.yw * c3.xy;
	r2 = _CelestePSTex0.Sample(sampler_CelestePSTex0, r0.yw);
	r0.y = r0.z * r2.z;
	r0.x = (r0.y * c9.z) + r0.x;
	r2.x = r1.x + c1.x;
	r2.y = r1.y + c2.x;
	r0.yz = r2.xy * c3.xy;
	r1 = _CelestePSTex0.Sample(sampler_CelestePSTex0, r0.yz);
	r0.y = r1.x + c15.z;
	r1 = c16 + v0.zxxx;
	r1.y = ((r1.y >= 0.0) ? c12.x : c12.z);
	r1.z = ((r1.z >= 0.0) ? c12.y : c12.z);
	r1.w = ((r1.w >= 0.0) ? c12.y : c12.z);
	r0.y = ((r0.y >= 0.0) ? r1.w : c15.w);
	r0.y = r0.y + r1.z;
	r2.x = max(r1.y, r0.y);
	r1.y = max(r2.x, r0.x);
	r0 = c0;
	r0 = (v0.xxxx * r0) + c8;
	r2 = lerp(r0, c7, r1.yyyy);
	r0 = r2 * v0.wwww;
	oC0 = ((r1.x >= 0.0) ? c7 : r0);
	r0.w = c15.w;
	oC1.z = ((r1.x >= 0.0) ? r0.w : c9.w);
	oC1.xyw = c12.yyx;
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
	#undef c11
	#undef v0
	#undef oC0
	#undef oC1
	#undef vPos
	return output;
}


            ENDHLSL
        }
    }
}
