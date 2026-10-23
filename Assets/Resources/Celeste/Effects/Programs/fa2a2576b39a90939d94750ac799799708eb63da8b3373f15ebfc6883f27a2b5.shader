Shader "Celeste/Effects/fa2a2576b39a90939d94750ac799799708eb63da8b3373f15ebfc6883f27a2b5"
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
	float4 _CelestePS[6];
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
	const float4 c9 = float4(-0.5, 2.200000047, 0.200000002, 1.0);
	const float4 c10 = float4(0.25, 0.920000016, 0.04, 21.0);
	const float4 c11 = float4(29.0, 31.0, 0.0005, 0.002);
	const float4 c12 = float4(16.0, 0.300000011, 2.660000084, 2.940000058);
	const float4 c13 = float4(0.159154935, 0.5, 6.283185478, -3.141592739);
	const float4 c14 = float4(0.920000016, 0.041999999, 0.05, 0.001);
	const float4 c15 = float4(0.0005, 0.025, -0.021999999, -0.02);
	const float4 c16 = float4(0.03, -0.0135, 0.400000005, 0.600000023);
	const float4 c17 = float4(1.5, 0.349999993, 1.700000047, 2.0);
	const float4 c18 = float4(2.0, 0.5, -2.0, -0.5);
	const float4 c19 = float4(0.0, 1.0, 2.0, -1.0);
	const float4 c20 = float4(0.920000016, 0.039, 0.75, 0.060000001);
	const float4 c21 = float4(0.041999999, -0.02025, 0.041000001, -0.015);
	const float4 c22 = float4(0.349999993, 1.0, 0.0, 0.0);
	float4 r0;
	float4 r1;
	float4 r2;
	float4 r3;
	float4 r4;
	float4 r5;
	float4 r6;
	#define c0 _CelestePS[0]
	#define c1 _CelestePS[1]
	#define c2 _CelestePS[2]
	#define c6 _CelestePS[3]
	#define c7 _CelestePS[4]
	#define c8 _CelestePS[5]
	#define v0 input.m_o1
	#define oC0 output.m_oC0
	r0.xy = c9.xx + v0.xy;
	r0.xy = r0.xy * c9.yy;
	r0.z = abs(r0.y) * c9.z;
	r0.z = (r0.z * r0.z) + c9.w;
	r1.y = r0.x * r0.z;
	r0.x = abs(r1.y) * c10.x;
	r0.x = (r0.x * r0.x) + c9.w;
	r1.z = r0.y * r0.x;
	r0.xy = (r1.yz * -c9.xx) + -c9.xx;
	r1.yz = (r0.xy * c10.yy) + c10.zz;
	r0.w = c10.w;
	r0.z = (r1.z * r0.w) + c0.x;
	r0.z = (r0.z * c13.x) + c13.y;
	r0.z = frac(r0.z);
	r0.z = (r0.z * c13.z) + c13.w;
	r2.y = sin(r0.z);
	r3.xy = c11.xy;
	r0.z = (r1.z * r3.x) + c1.x;
	r0.z = (r0.z * c13.x) + c13.y;
	r0.z = frac(r0.z);
	r0.z = (r0.z * c13.z) + c13.w;
	r4.y = sin(r0.z);
	r0.z = r2.y * r4.y;
	r0.w = (r1.z * r3.y) + c2.x;
	r0.w = (r0.w * c13.x) + c13.y;
	r0.w = frac(r0.w);
	r0.w = (r0.w * c13.z) + c13.w;
	r2.y = sin(r0.w);
	r0.z = r0.z * r2.y;
	r2.xyz = (r0.zzz * c15.xxx) + c15.yzw;
	r1.x = (r0.z * c11.z) + r1.y;
	r3.x = (r2.z * c20.z) + r1.y;
	r0.zw = r2.xy * c20.zz;
	r2.xy = (r0.xx * c10.yy) + r0.wz;
	r3.y = r1.z + c16.y;
	r3 = _CelestePSTex0.Sample(sampler_CelestePSTex0, r3.xy);
	r4 = _CelestePSTex0.Sample(sampler_CelestePSTex0, r1.xz);
	r5 = _CelestePSTex0.Sample(sampler_CelestePSTex0, r1.yz);
	r0.xzw = r5.xyz * -c9.xxx;
	r0.w = (r4.z * -c9.x) + r0.w;
	r0.w = r0.w + c14.z;
	r3.z = (r3.z * c20.w) + r0.w;
	r4.y = (r0.y * c20.x) + c20.y;
	r2.w = (r0.y * c14.x) + c14.y;
	r4.x = r1.x + c14.w;
	r2.z = r1.x + c11.w;
	r5 = _CelestePSTex0.Sample(sampler_CelestePSTex0, r2.zw);
	r0.yw = r2.yw + c21.xy;
	r2.x = r2.x + c21.z;
	r6 = _CelestePSTex0.Sample(sampler_CelestePSTex0, r0.yw);
	r0.x = (r5.x * -c9.x) + r0.x;
	r0.x = r0.x + c14.z;
	r3.x = (r6.x * c20.w) + r0.x;
	r5 = _CelestePSTex0.Sample(sampler_CelestePSTex0, r4.xy);
	r2.y = r4.y + c21.w;
	r2 = _CelestePSTex0.Sample(sampler_CelestePSTex0, r2.xy);
	r0.x = (r5.y * -c9.x) + r0.z;
	r0.x = r0.x + c14.z;
	r3.y = (r2.y * c16.x) + r0.x;
	r0.xyz = r3.xyz * r3.xyz;
	r0.xyz = r0.xyz * c16.zzz;
	r0.xyz = saturate((r3.xyz * c16.www) + r0.xyz);
	r0.w = r1.y * r1.z;
	r0.w = r0.w * c12.x;
	r1.xw = -r1.yz + c9.ww;
	r0.w = r0.w * r1.x;
	r0.w = r1.w * r0.w;
	r1.x = ((r1.x >= 0.0) ? c19.x : c19.y);
	r1.w = ((r1.w >= 0.0) ? c19.x : c19.y);
	r2.x = pow(abs(r0.w), c12.y);
	r0.xyz = r0.xyz * r2.xxx;
	r0.xyz = r0.xyz * c12.zwz;
	r0.w = r1.z * c8.y;
	r1.y = ((r1.y >= 0.0) ? c19.x : c19.y);
	r1.z = ((r1.z >= 0.0) ? c19.x : c19.y);
	r1.xy = r1.xw + r1.yz;
	r2.x = c17.x;
	r0.w = (r0.w * r2.x) + c6.x;
	r0.w = (r0.w * c13.x) + c13.y;
	r0.w = frac(r0.w);
	r0.w = (r0.w * c13.z) + c13.w;
	r2.y = sin(r0.w);
	r0.w = (r2.y * c17.y) + c17.y;
	r1.z = pow(abs(r0.w), c17.z);
	r0.w = (r1.z * -c9.x) + -c9.x;
	r0.xyz = r0.xyz * r0.www;
	r0.xyz = r0.xyz * c7.xxx;
	r0.w = c8.x * v0.x;
	r1.zw = ((r0.w >= 0.0) ? c18.xy : c18.zw);
	r0.w = r0.w * r1.w;
	r0.w = frac(r0.w);
	r0.w = (r1.z * r0.w) + c17.w;
	r0.w = r0.w * -c9.x;
	r0.w = frac(r0.w);
	r0.w = (r0.w * c19.z) + c19.w;
	r0.w = saturate(r0.w + r0.w);
	r0.w = (r0.w * -c22.x) + c22.y;
	r0.xyz = r0.xyz * r0.www;
	r0.xyz = ((-r1.x >= 0.0) ? r0.xyz : c19.xxx);
	oC0.xyz = ((-r1.y >= 0.0) ? r0.xyz : c19.xxx);
	oC0.w = c9.w;
	#undef c0
	#undef c1
	#undef c2
	#undef c6
	#undef c7
	#undef c8
	#undef v0
	#undef oC0
	return output;
}


            ENDHLSL
        }
    }
}
