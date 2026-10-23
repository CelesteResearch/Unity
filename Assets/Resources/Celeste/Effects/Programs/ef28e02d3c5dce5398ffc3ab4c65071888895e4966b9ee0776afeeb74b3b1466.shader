Shader "Celeste/Effects/ef28e02d3c5dce5398ffc3ab4c65071888895e4966b9ee0776afeeb74b3b1466"
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
        _CelestePSTex3 ("_CelestePSTex3", 2D) = "white" {}
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
	float4 _CelestePS[5];
};

Texture2D _CelestePSTex3;
SamplerState sampler_CelestePSTex3;

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
	const float4 c5 = float4(-8.0, 0.125, 1.0, 0.05);
	const float4 c6 = float4(0.200000002, 0.159154935, 0.5, 2.0);
	const float4 c7 = float4(6.283185478, -3.141592739, 0.5, 0.759999991);
	const float4 c8 = float4(0.333333312, 0.166666699, 1.0, 0.0);
	const float4 c9 = float4(0.333333402, -0.499999912, 0.600000023, 52.0);
	const float4 c10 = float4(17.0, 59.400001525, 15.0, 0.0);
	const float4 c11 = float4(5.0, 0.5, 0.100000001, 0.0);
	const float4 c12 = float4(262144.0, 32768.0, 2097152.0, 0.0);
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
	#define c3 _CelestePS[3]
	#define c4 _CelestePS[4]
	#define v0 input.m_o1
	#define oC0 output.m_oC0
	r0.xy = c4.xy * v0.xy;
	r1.xy = r0.xy * c5.ww;
	r1.z = c2.x;
	r0.z = dot(r1.xyz, c8.xxx);
	r2.xyz = r1.xyz + r0.zzz;
	r3.xyz = frac(r2.xyz);
	r2.xyz = r2.xyz + -r3.xyz;
	r1.xyz = r1.xyz + -r2.xyz;
	r0.z = dot(r2.xyz, c8.yyy);
	r1.xyz = r1.xyz + r0.zzz;
	r3.xyz = -r1.yzx + r1.xyz;
	r4.x = ((r3.z >= 0.0) ? c8.z : c8.w);
	r4.y = ((r3.x >= 0.0) ? c8.z : c8.w);
	r4.z = ((r3.y >= 0.0) ? c8.z : c8.w);
	r3.x = ((r3.x >= 0.0) ? c8.w : c8.z);
	r3.y = ((r3.y >= 0.0) ? c8.w : c8.z);
	r3.z = ((r3.z >= 0.0) ? c8.w : c8.z);
	r5.xyz = (r4.yzx * r3.zxy) + r2.xyz;
	r0.z = dot(r5.xyz, c10.xyz);
	r0.z = (r0.z * c6.y) + c6.z;
	r0.z = frac(r0.z);
	r0.z = (r0.z * c7.x) + c7.y;
	r5.y = sin(r0.z);
	r5.xyz = r5.yyy * c12.xyz;
	r5.xyz = frac(r5.xyz);
	r5.xyz = r5.xyz + -c6.zzz;
	r6.xyz = (r4.yzx * -r3.zxy) + r1.xyz;
	r3.xyz = (r4.xyz * -r3.xyz) + c5.zzz;
	r4.xyz = r6.xyz + c8.yyy;
	r5.y = dot(r5.xyz, r4.xyz);
	r4.y = dot(r4.xyz, r4.xyz);
	r6.xyz = r1.xyz + -r3.xyz;
	r3.xyz = r2.xyz + r3.xyz;
	r0.z = dot(r3.xyz, c10.xyz);
	r0.z = (r0.z * c6.y) + c6.z;
	r0.z = frac(r0.z);
	r0.z = (r0.z * c7.x) + c7.y;
	r3.y = sin(r0.z);
	r3.xyz = r3.yyy * c12.xyz;
	r3.xyz = frac(r3.xyz);
	r3.xyz = r3.xyz + -c6.zzz;
	r6.xyz = r6.xyz + c9.xxx;
	r4.z = dot(r6.xyz, r6.xyz);
	r5.z = dot(r3.xyz, r6.xyz);
	r4.x = dot(r1.xyz, r1.xyz);
	r3.xyz = r1.xyz + c9.yyy;
	r4.w = dot(r3.xyz, r3.xyz);
	r4 = -r4 + c9.zzzz;
	r6 = max(r4, c8.wwww);
	r4 = r6 * r6;
	r4 = r4 * r4;
	r6.xyz = r2.xyz + c5.zzz;
	r0.z = dot(r2.xyz, c10.xyz);
	r0.z = (r0.z * c6.y) + c6.z;
	r0.z = frac(r0.z);
	r0.z = (r0.z * c7.x) + c7.y;
	r2.y = sin(r0.z);
	r2.xyz = r2.yyy * c12.xyz;
	r2.xyz = frac(r2.xyz);
	r2.xyz = r2.xyz + -c6.zzz;
	r5.x = dot(r2.xyz, r1.xyz);
	r0.z = dot(r6.xyz, c10.xyz);
	r0.z = (r0.z * c6.y) + c6.z;
	r0.z = frac(r0.z);
	r0.z = (r0.z * c7.x) + c7.y;
	r1.y = sin(r0.z);
	r1.xyz = r1.yyy * c12.xyz;
	r1.xyz = frac(r1.xyz);
	r1.xyz = r1.xyz + -c6.zzz;
	r5.w = dot(r1.xyz, r3.xyz);
	r1 = r5 * r4;
	r0.z = dot(r1, c9.wwww);
	r0.z = (r0.z * c11.x) + c11.y;
	r0.w = frac(r0.z);
	r0.z = r0.z + -r0.w;
	r0.z = (r0.z * c11.z) + c11.y;
	r1.xy = c4.xy;
	r1.xy = (v0.xy * r1.xy) + -c0.xy;
	r1.xy = -abs(r1.xy) + c0.xy;
	r1.zw = r1.xy + c5.xx;
	r1.xy = r1.xy * c5.yy;
	r1.x = ((r1.z >= 0.0) ? c5.z : r1.x);
	r1.y = ((r1.w >= 0.0) ? c5.z : r1.y);
	r0.w = r1.y * r1.x;
	r1.x = r0.w * r0.w;
	r0.w = r0.w * r1.x;
	r1.x = c6.x;
	r1.xy = (r0.yx * r1.xx) + c3.xx;
	r1.zw = r1.xy + c7.zw;
	r1.xy = (r1.yx * c6.yy) + c6.zz;
	r1.xy = frac(r1.xy);
	r1.xy = (r1.xy * c7.xx) + c7.yy;
	r1.zw = (r1.zw * c6.yy) + c6.zz;
	r1.zw = frac(r1.zw);
	r1.zw = (r1.zw * c7.xx) + c7.yy;
	r2.x = cos(r1.w);
	r3.y = sin(r1.z);
	r1.z = r2.x + r2.x;
	r2.y = (r3.y * c6.w) + r1.z;
	r3.x = cos(r1.y);
	r4.y = sin(r1.x);
	r1.x = r3.x + r3.x;
	r2.x = (r4.y * c6.w) + r1.x;
	r0.xy = (r2.xy * r0.ww) + r0.xy;
	r0.xy = r0.xy * c1.xy;
	r1 = _CelestePSTex3.Sample(sampler_CelestePSTex3, r0.xy);
	oC0 = r0.zzzz * r1;
	#undef c0
	#undef c1
	#undef c2
	#undef c3
	#undef c4
	#undef v0
	#undef oC0
	return output;
}


            ENDHLSL
        }
    }
}
