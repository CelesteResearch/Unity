Shader "Celeste/Effects/e34fe7b0f757c908b0332c41c5fa8fffe942bee08ec153cb4c26cbb627065ce3"
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
	float4 _CelestePS[21];
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
	const float4 c21 = float4(-0.5, 2.0, 0.94, 0.880000001);
	const float4 c22 = float4(0.819999992, 0.759999991, 0.700000011, 0.639999983);
	const float4 c23 = float4(0.579999983, 0.519999981, 0.460000009, 0.400000005);
	const float4 c24 = float4(0.340000002, 0.280000001, 0.22, 0.159999995);
	const float4 c25 = float4(0.100000001, 0.04, 0.0, 0.0);
	float4 r0;
	float4 r1;
	float4 r2;
	float4 r3;
	float4 r4;
	float4 r5;
	float4 r6;
	float4 r7;
	float4 r8;
	float4 r9;
	float4 r10;
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
	#define c15 _CelestePS[15]
	#define c16 _CelestePS[16]
	#define c17 _CelestePS[17]
	#define c18 _CelestePS[18]
	#define c19 _CelestePS[19]
	#define c20 _CelestePS[20]
	#define t0 input.m_oT0
	#define oC0 output.m_oC0
	r0.x = t0.x * c19.x;
	r1.w = c19.y;
	r0.y = (t0.y * r1.w) + c16.x;
	r0 = _CelestePSTex1.Sample(sampler_CelestePSTex1, r0.xy);
	r0.yz = r0.yz + c21.xx;
	r0.yz = r0.yz * c18.xy;
	r0.yz = r0.yz * c20.xy;
	r1.xy = (r0.yz * c21.yy) + t0.xy;
	r2.y = r1.y + c0.x;
	r2.x = r1.x;
	r3.x = r2.x;
	r3.y = r1.y + c1.x;
	r4.x = r3.x;
	r4.y = r1.y + c2.x;
	r5.x = r4.x;
	r5.y = r1.y + c3.x;
	r6.x = r5.x;
	r6.y = r1.y + c4.x;
	r7.x = r6.x;
	r7.y = r1.y + c5.x;
	r8.x = r7.x;
	r8.y = r1.y + c6.x;
	r2 = _CelestePSTex0.Sample(sampler_CelestePSTex0, r2.xy);
	r9 = _CelestePSTex0.Sample(sampler_CelestePSTex0, r1.xy);
	r3 = _CelestePSTex0.Sample(sampler_CelestePSTex0, r3.xy);
	r4 = _CelestePSTex0.Sample(sampler_CelestePSTex0, r4.xy);
	r5 = _CelestePSTex0.Sample(sampler_CelestePSTex0, r5.xy);
	r6 = _CelestePSTex0.Sample(sampler_CelestePSTex0, r6.xy);
	r7 = _CelestePSTex0.Sample(sampler_CelestePSTex0, r7.xy);
	r10 = _CelestePSTex0.Sample(sampler_CelestePSTex0, r8.xy);
	r2 = (r2 * c21.zzzz) + r9;
	r2 = (r3 * c21.wwww) + r2;
	r2 = (r4 * c22.xxxx) + r2;
	r2 = (r5 * c22.yyyy) + r2;
	r2 = (r6 * c22.zzzz) + r2;
	r2 = (r7 * c22.wwww) + r2;
	r2 = (r10 * c23.xxxx) + r2;
	r8.y = r1.y + c7.x;
	r3.x = r8.x;
	r3.y = r1.y + c8.x;
	r4.x = r3.x;
	r4.y = r1.y + c9.x;
	r5.x = r4.x;
	r5.y = r1.y + c10.x;
	r6.x = r5.x;
	r6.y = r1.y + c11.x;
	r7.x = r6.x;
	r7.y = r1.y + c12.x;
	r9.x = r7.x;
	r9.y = r1.y + c13.x;
	r10.x = r9.x;
	r10.y = r1.y + c14.x;
	r1.y = r1.y + c15.x;
	r1.x = r10.x;
	r8 = _CelestePSTex0.Sample(sampler_CelestePSTex0, r8.xy);
	r1 = _CelestePSTex0.Sample(sampler_CelestePSTex0, r1.xy);
	r3 = _CelestePSTex0.Sample(sampler_CelestePSTex0, r3.xy);
	r4 = _CelestePSTex0.Sample(sampler_CelestePSTex0, r4.xy);
	r5 = _CelestePSTex0.Sample(sampler_CelestePSTex0, r5.xy);
	r6 = _CelestePSTex0.Sample(sampler_CelestePSTex0, r6.xy);
	r7 = _CelestePSTex0.Sample(sampler_CelestePSTex0, r7.xy);
	r9 = _CelestePSTex0.Sample(sampler_CelestePSTex0, r9.xy);
	r10 = _CelestePSTex0.Sample(sampler_CelestePSTex0, r10.xy);
	r2 = (r8 * c23.yyyy) + r2;
	r2 = (r3 * c23.zzzz) + r2;
	r2 = (r4 * c23.wwww) + r2;
	r2 = (r5 * c24.xxxx) + r2;
	r2 = (r6 * c24.yyyy) + r2;
	r2 = (r7 * c24.zzzz) + r2;
	r2 = (r9 * c24.wwww) + r2;
	r2 = (r10 * c25.xxxx) + r2;
	r1 = (r1 * c25.yyyy) + r2;
	r0 = r0.xxxx * r1;
	r0 = r0 * c17.xxxx;
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
	#undef c15
	#undef c16
	#undef c17
	#undef c18
	#undef c19
	#undef c20
	#undef t0
	#undef oC0
	return output;
}


            ENDHLSL
        }
    }
}
