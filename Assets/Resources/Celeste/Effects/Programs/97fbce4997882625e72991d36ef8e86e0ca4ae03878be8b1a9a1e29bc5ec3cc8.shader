Shader "Celeste/Effects/97fbce4997882625e72991d36ef8e86e0ca4ae03878be8b1a9a1e29bc5ec3cc8"
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
	float4 _CelesteVS[12];
};

struct CelesteVSInput
{
	float4 m_v0 : POSITION0;
	float4 m_v1 : NORMAL0;
	float4 m_v2 : TEXCOORD0;
};

struct CelesteVSOutput
{
	float4 m_oD0 : COLOR0;
	float4 m_oT0 : TEXCOORD0;
	float4 m_oT1 : TEXCOORD1;
	float4 m_oT2 : TEXCOORD2;
	float4 m_oPos : SV_Position;
};

CelesteVSOutput CelesteSourceVS(CelesteVSInput input)
{
	CelesteVSOutput output = (CelesteVSOutput) 0;
	const float4 c1 = float4(0.0, 1.0, 0.0, 0.0);
	float4 r0;
	#define c0 _CelesteVS[0]
	#define c14 _CelesteVS[1]
	#define c15 _CelesteVS[2]
	#define c16 _CelesteVS[3]
	#define c17 _CelesteVS[4]
	#define c18 _CelesteVS[5]
	#define c19 _CelesteVS[6]
	#define c20 _CelesteVS[7]
	#define c21 _CelesteVS[8]
	#define c23 _CelesteVS[9]
	#define c24 _CelesteVS[10]
	#define c25 _CelesteVS[11]
	#define v0 input.m_v0
	#define v1 input.m_v1
	#define v2 input.m_v2
	#define oPos output.m_oPos
	#define oD0 output.m_oD0
	#define oT0 output.m_oT0
	#define oT1 output.m_oT1
	#define oT2 output.m_oT2
	#define FLT_MAX 1e38
	oPos.x = dot(v0, c15);
	oPos.y = dot(v0, c16);
	oPos.z = dot(v0, c17);
	oPos.w = dot(v0, c18);
	oT1.x = dot(v0, c19);
	oT1.y = dot(v0, c20);
	oT1.z = dot(v0, c21);
	r0.x = dot(v1.xyz, c23.xyz);
	r0.y = dot(v1.xyz, c24.xyz);
	r0.z = dot(v1.xyz, c25.xyz);
	r0.w = dot(r0.xyz, r0.xyz);
	r0.w = (r0.w == 0.0) ? FLT_MAX : rsqrt(abs(r0.w));
	oT2.xyz = r0.www * r0.xyz;
	r0.x = dot(v0, c14);
	r0.x = max(r0.x, c1.x);
	oT1.w = min(r0.x, c1.y);
	oT0.xy = v2.xy;
	oD0.xyz = c1.yyy;
	oD0.w = c0.w;
	#undef c0
	#undef c14
	#undef c15
	#undef c16
	#undef c17
	#undef c18
	#undef c19
	#undef c20
	#undef c21
	#undef c23
	#undef c24
	#undef c25
	#undef v0
	#undef v1
	#undef v2
	#undef oPos
	#undef oD0
	#undef oT0
	#undef oT1
	#undef oT2
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
	float4 _CelestePS[15];
};

Texture2D _CelestePSTex0;
SamplerState sampler_CelestePSTex0;

struct CelestePSInput
{
	float4 m_oD0 : COLOR0;
	float4 m_oT0 : TEXCOORD0;
	float4 m_oT1 : TEXCOORD1;
	float4 m_oT2 : TEXCOORD2;
	float4 m_oPos : SV_Position;
};

struct CelestePSOutput
{
	float4 m_oC0 : SV_Target0;
};

CelestePSOutput CelestePS(CelestePSInput input)
{
	CelestePSOutput output = (CelestePSOutput) 0;
	const float4 c15 = float4(1.0, 0.0, 0.0, 0.0);
	float4 r0;
	float4 r1;
	float4 r2;
	float4 r3;
	float4 r4;
	float4 r5;
	float4 r6;
	float4 r7;
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
	#define c12 _CelestePS[12]
	#define c13 _CelestePS[13]
	#define c14 _CelestePS[14]
	#define v0 input.m_oD0
	#define t0 input.m_oT0
	#define t1 input.m_oT1
	#define t2 input.m_oT2
	#define oC0 output.m_oC0
	#define FLT_MAX 1e38
	r0 = _CelestePSTex0.Sample(sampler_CelestePSTex0, t0.xy);
	r1.xyz = normalize(t2.xyz);
	r2.x = dot(-c5.xyz, r1.xyz);
	r2.y = dot(-c8.xyz, r1.xyz);
	r2.z = dot(-c11.xyz, r1.xyz);
	r3.x = ((r2.x >= 0.0) ? c15.x : c15.y);
	r3.y = ((r2.y >= 0.0) ? c15.x : c15.y);
	r3.z = ((r2.z >= 0.0) ? c15.x : c15.y);
	r2.xyz = r2.xyz * r3.xyz;
	r4.xyz = -t1.xyz + c14.xyz;
	r1.w = dot(r4.xyz, r4.xyz);
	r1.w = (r1.w == 0.0) ? FLT_MAX : rsqrt(abs(r1.w));
	r5.xyz = (r4.xyz * r1.www) + -c5.xyz;
	r6.xyz = normalize(r5.xyz);
	r5.x = dot(r6.xyz, r1.xyz);
	r6.xyz = (r4.xyz * r1.www) + -c8.xyz;
	r4.xyz = (r4.xyz * r1.www) + -c11.xyz;
	r7.xyz = normalize(r4.xyz);
	r5.z = dot(r7.xyz, r1.xyz);
	r4.xyz = normalize(r6.xyz);
	r5.y = dot(r4.xyz, r1.xyz);
	r1.xyz = r3.xyz * r5.xyz;
	r1.x = ((r5.x >= 0.0) ? r1.x : c15.y);
	r1.y = ((r5.y >= 0.0) ? r1.y : c15.y);
	r1.z = ((r5.z >= 0.0) ? r1.z : c15.y);
	r3.x = log2(r1.x);
	r3.y = log2(r1.y);
	r3.z = log2(r1.z);
	r1.xyz = r3.xyz * c4.xxx;
	r2.w = exp2(r1.y);
	r3.xyz = r2.www * c10.xyz;
	r2.w = exp2(r1.x);
	r3.w = exp2(r1.z);
	r1.xyz = (r2.www * c7.xyz) + r3.xyz;
	r1.xyz = (r3.www * c13.xyz) + r1.xyz;
	r1.xyz = r1.xyz * c3.xyz;
	r0 = r0 * v0;
	r1.xyz = r0.www * r1.xyz;
	r3.xyz = r2.yyy * c9.xyz;
	r3.xyz = (r2.xxx * c6.xyz) + r3.xyz;
	r2.xyz = (r2.zzz * c12.xyz) + r3.xyz;
	r3.xyz = c1.xyz;
	r2.xyz = (r2.xyz * r3.xyz) + c2.xyz;
	r1.xyz = (r0.xyz * r2.xyz) + r1.xyz;
	r2.xyz = (c0.xyz * r0.www) + -r1.xyz;
	r0.xyz = (t1.www * r2.xyz) + r1.xyz;
	oC0 = r0;
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
	#undef c12
	#undef c13
	#undef c14
	#undef v0
	#undef t0
	#undef t1
	#undef t2
	#undef oC0
	return output;
}


            ENDHLSL
        }
    }
}
