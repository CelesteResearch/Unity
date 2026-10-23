Shader "Celeste/Effects/1639c4f91394ff6b2d7032473389dc65bd21c7757890c7008758c06b38983141"
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
	float4 _CelestePS[30];
};

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
	const float4 c31 = float4(-0.5, 0.0, 0.5, 0.159154935);
	const float4 c32 = float4(6.283185478, -3.141592739, 1.0, 0.0);
	float4 r0;
	float4 r1;
	float4 r2;
	float4 r3;
	float4 r4;
	#define c0 _CelestePS[0]
	#define c2 _CelestePS[1]
	#define c3 _CelestePS[2]
	#define c4 _CelestePS[3]
	#define c5 _CelestePS[4]
	#define c6 _CelestePS[5]
	#define c7 _CelestePS[6]
	#define c8 _CelestePS[7]
	#define c9 _CelestePS[8]
	#define c10 _CelestePS[9]
	#define c11 _CelestePS[10]
	#define c12 _CelestePS[11]
	#define c13 _CelestePS[12]
	#define c14 _CelestePS[13]
	#define c15 _CelestePS[14]
	#define c16 _CelestePS[15]
	#define c17 _CelestePS[16]
	#define c18 _CelestePS[17]
	#define c19 _CelestePS[18]
	#define c20 _CelestePS[19]
	#define c21 _CelestePS[20]
	#define c22 _CelestePS[21]
	#define c23 _CelestePS[22]
	#define c24 _CelestePS[23]
	#define c25 _CelestePS[24]
	#define c26 _CelestePS[25]
	#define c27 _CelestePS[26]
	#define c28 _CelestePS[27]
	#define c29 _CelestePS[28]
	#define c30 _CelestePS[29]
	#define v0 input.m_o1
	#define oC0 output.m_oC0
	r0.x = c3.x;
	r0.y = c2.x;
	r0.zw = c31.xx + v0.xy;
	r0.y = dot(r0.zw, r0.xy) + c31.y;
	r1.y = c31.y;
	r0.x = dot(r0.zw, c2.xy) + r1.y;
	r0.xy = r0.xy + c31.zz;
	r2.xy = c4.xy;
	r0.xy = (r0.xy * r2.xy) + -c5.xy;
	r0.xy = r0.xy + c6.xy;
	r1.x = c8.x;
	r2.xy = lerp(c7.xy, r0.xy, r1.xx);
	r0.xy = r2.xy + -c23.xy;
	r2.xy = c26.xy;
	r0.zw = (v0.xy * r2.xy) + c0.xy;
	r0.zw = r0.zw * c8.xx;
	r1.x = c12.x;
	r1.x = (r0.w * r1.x) + c13.x;
	r1.x = r1.x + c29.y;
	r1.x = (r1.x * c31.w) + c31.z;
	r1.x = frac(r1.x);
	r1.x = (r1.x * c32.x) + c32.y;
	r2.y = sin(r1.x);
	r1.x = r2.y * c14.x;
	r2.x = c9.x;
	r1.z = (r0.z * r2.x) + c10.x;
	r1.z = r1.z + c29.x;
	r1.z = (r1.z * c31.w) + c31.z;
	r1.z = frac(r1.z);
	r1.z = (r1.z * c32.x) + c32.y;
	r2.y = sin(r1.z);
	r2.x = (r2.y * c11.x) + r1.x;
	r1.x = c18.x;
	r0.w = (r0.w * r1.x) + c19.x;
	r1.x = c15.x;
	r0.z = (r0.z * r1.x) + c16.x;
	r0.z = r0.z + c29.z;
	r0.z = (r0.z * c31.w) + c31.z;
	r0.z = frac(r0.z);
	r0.z = (r0.z * c32.x) + c32.y;
	r3.y = sin(r0.z);
	r0.z = r0.w + c29.w;
	r0.z = (r0.z * c31.w) + c31.z;
	r0.z = frac(r0.z);
	r0.z = (r0.z * c32.x) + c32.y;
	r4.y = sin(r0.z);
	r0.z = r4.y * c20.x;
	r2.y = (r3.y * c17.x) + r0.z;
	r3.x = c22.x;
	r3.y = c21.x;
	r3.y = dot(r2.xy, r3.xy) + c31.y;
	r3.x = dot(r2.xy, c21.xy) + r1.y;
	r0.xy = (r3.xy * -c24.xy) + r0.xy;
	r1.xy = c28.xy;
	r0.xy = (r0.xy * r1.xy) + c25.xy;
	r0.zw = -r0.xy + c32.zz;
	r0.z = ((r0.z >= 0.0) ? c32.w : c32.z);
	r0.w = ((r0.w >= 0.0) ? c32.w : c32.z);
	r1.x = ((r0.x >= 0.0) ? c32.w : c32.z);
	r1.y = ((r0.y >= 0.0) ? c32.w : c32.z);
	r2 = _CelestePSTex2.Sample(sampler_CelestePSTex2, r0.xy);
	r0.xy = r0.zw + r1.xy;
	r1 = r2 * c27.xxxx;
	r1 = ((-r0.x >= 0.0) ? r2 : r1);
	r2 = r1 * c27.yyyy;
	r0 = ((-r0.y >= 0.0) ? r1 : r2);
	oC0 = r0 * c30;
	#undef c0
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
	#undef c12
	#undef c13
	#undef c14
	#undef c15
	#undef c16
	#undef c17
	#undef c18
	#undef c19
	#undef c20
	#undef c21
	#undef c22
	#undef c23
	#undef c24
	#undef c25
	#undef c26
	#undef c27
	#undef c28
	#undef c29
	#undef c30
	#undef v0
	#undef oC0
	return output;
}


            ENDHLSL
        }
    }
}
