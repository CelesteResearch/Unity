Shader "Celeste/Effects/75058890946b6f75bb531c10efafd296a3cd34f10b9b3c5997f9e176ed955124"
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
	const float4 c5 = float4(2.0, 0.333333312, 0.166666699, 1.0);
	const float4 c6 = float4(1.0, 0.0, 0.333333402, -0.499999912);
	const float4 c7 = float4(0.600000023, 17.0, 59.400001525, 15.0);
	const float4 c8 = float4(262144.0, 32768.0, 2097152.0, 52.0);
	const float4 c9 = float4(0.159154935, 0.5, 6.283185478, -3.141592739);
	const float4 c10 = float4(16.0, 0.062499999, 0.0, 0.0);
	const float4 c11 = float4(4.0, 0.25, 8.0, 0.125);
	float4 r0;
	float4 r1;
	float4 r2;
	float4 r3;
	float4 r4;
	float4 r5;
	#define c0 _CelestePS[0]
	#define c1 _CelestePS[1]
	#define c2 _CelestePS[2]
	#define c3 _CelestePS[3]
	#define c4 _CelestePS[4]
	#define v0 input.m_o1
	#define oC0 output.m_oC0
	r0.xy = c11.xx * v0.xy;
	r0.z = c1.x;
	r0.w = dot(r0.xyz, c5.yyy);
	r1.xyz = r0.xyz + r0.www;
	r2.xyz = frac(r1.xyz);
	r1.xyz = r1.xyz + -r2.xyz;
	r0.xyz = r0.xyz + -r1.xyz;
	r0.w = dot(r1.xyz, c5.zzz);
	r0.xyz = r0.xyz + r0.www;
	r2.xyz = -r0.yzx + r0.xyz;
	r3.x = ((r2.z >= 0.0) ? c6.x : c6.y);
	r3.y = ((r2.x >= 0.0) ? c6.x : c6.y);
	r3.z = ((r2.y >= 0.0) ? c6.x : c6.y);
	r2.x = ((r2.x >= 0.0) ? c6.y : c6.x);
	r2.y = ((r2.y >= 0.0) ? c6.y : c6.x);
	r2.z = ((r2.z >= 0.0) ? c6.y : c6.x);
	r4.xyz = (r3.yzx * r2.zxy) + r1.xyz;
	r0.w = dot(r4.xyz, c7.yzw);
	r0.w = (r0.w * c9.x) + c9.y;
	r0.w = frac(r0.w);
	r0.w = (r0.w * c9.z) + c9.w;
	r4.y = sin(r0.w);
	r4.xyz = r4.yyy * c8.xyz;
	r4.xyz = frac(r4.xyz);
	r4.xyz = r4.xyz + -c9.yyy;
	r5.xyz = (r3.yzx * -r2.zxy) + r0.xyz;
	r2.xyz = (r3.xyz * -r2.xyz) + c5.www;
	r3.xyz = r5.xyz + c5.zzz;
	r4.y = dot(r4.xyz, r3.xyz);
	r3.y = dot(r3.xyz, r3.xyz);
	r5.xyz = r0.xyz + -r2.xyz;
	r2.xyz = r1.xyz + r2.xyz;
	r0.w = dot(r2.xyz, c7.yzw);
	r0.w = (r0.w * c9.x) + c9.y;
	r0.w = frac(r0.w);
	r0.w = (r0.w * c9.z) + c9.w;
	r2.y = sin(r0.w);
	r2.xyz = r2.yyy * c8.xyz;
	r2.xyz = frac(r2.xyz);
	r2.xyz = r2.xyz + -c9.yyy;
	r5.xyz = r5.xyz + c6.zzz;
	r3.z = dot(r5.xyz, r5.xyz);
	r4.z = dot(r2.xyz, r5.xyz);
	r3.x = dot(r0.xyz, r0.xyz);
	r2.xyz = r0.xyz + c6.www;
	r3.w = dot(r2.xyz, r2.xyz);
	r3 = -r3 + c7.xxxx;
	r5 = max(r3, c6.yyyy);
	r3 = r5 * r5;
	r3 = r3 * r3;
	r0.w = dot(r1.xyz, c7.yzw);
	r1.xyz = r1.xyz + c5.www;
	r1.x = dot(r1.xyz, c7.yzw);
	r1.x = (r1.x * c9.x) + c9.y;
	r1.x = frac(r1.x);
	r1.x = (r1.x * c9.z) + c9.w;
	r5.y = sin(r1.x);
	r1.xyz = r5.yyy * c8.xyz;
	r1.xyz = frac(r1.xyz);
	r1.xyz = r1.xyz + -c9.yyy;
	r4.w = dot(r1.xyz, r2.xyz);
	r0.w = (r0.w * c9.x) + c9.y;
	r0.w = frac(r0.w);
	r0.w = (r0.w * c9.z) + c9.w;
	r1.y = sin(r0.w);
	r1.xyz = r1.yyy * c8.xyz;
	r1.xyz = frac(r1.xyz);
	r1.xyz = r1.xyz + -c9.yyy;
	r4.x = dot(r1.xyz, r0.xyz);
	r0 = r4 * r3;
	r0.x = dot(r0, c8.wwww);
	r0.x = abs(r0.x) * c11.y;
	r1.xy = c5.xx * v0.xy;
	r1.z = c0.x;
	r0.y = dot(r1.xyz, c5.yyy);
	r0.yzw = r1.xyz + r0.yyy;
	r2.xyz = frac(r0.yzw);
	r0.yzw = r0.yzw + -r2.xyz;
	r1.xyz = r1.xyz + -r0.yzw;
	r1.w = dot(r0.yzw, c5.zzz);
	r1.xyz = r1.xyz + r1.www;
	r2.xyz = -r1.yzx + r1.xyz;
	r3.x = ((r2.z >= 0.0) ? c6.x : c6.y);
	r3.y = ((r2.x >= 0.0) ? c6.x : c6.y);
	r3.z = ((r2.y >= 0.0) ? c6.x : c6.y);
	r2.x = ((r2.x >= 0.0) ? c6.y : c6.x);
	r2.y = ((r2.y >= 0.0) ? c6.y : c6.x);
	r2.z = ((r2.z >= 0.0) ? c6.y : c6.x);
	r4.xyz = (r3.yzx * r2.zxy) + r0.yzw;
	r1.w = dot(r4.xyz, c7.yzw);
	r1.w = (r1.w * c9.x) + c9.y;
	r1.w = frac(r1.w);
	r1.w = (r1.w * c9.z) + c9.w;
	r4.y = sin(r1.w);
	r4.xyz = r4.yyy * c8.xyz;
	r4.xyz = frac(r4.xyz);
	r4.xyz = r4.xyz + -c9.yyy;
	r5.xyz = (r3.yzx * -r2.zxy) + r1.xyz;
	r2.xyz = (r3.xyz * -r2.xyz) + c5.www;
	r3.xyz = r5.xyz + c5.zzz;
	r4.y = dot(r4.xyz, r3.xyz);
	r3.y = dot(r3.xyz, r3.xyz);
	r5.xyz = r1.xyz + -r2.xyz;
	r2.xyz = r0.yzw + r2.xyz;
	r1.w = dot(r2.xyz, c7.yzw);
	r1.w = (r1.w * c9.x) + c9.y;
	r1.w = frac(r1.w);
	r1.w = (r1.w * c9.z) + c9.w;
	r2.y = sin(r1.w);
	r2.xyz = r2.yyy * c8.xyz;
	r2.xyz = frac(r2.xyz);
	r2.xyz = r2.xyz + -c9.yyy;
	r5.xyz = r5.xyz + c6.zzz;
	r3.z = dot(r5.xyz, r5.xyz);
	r4.z = dot(r2.xyz, r5.xyz);
	r3.x = dot(r1.xyz, r1.xyz);
	r2.xyz = r1.xyz + c6.www;
	r3.w = dot(r2.xyz, r2.xyz);
	r3 = -r3 + c7.xxxx;
	r5 = max(r3, c6.yyyy);
	r3 = r5 * r5;
	r3 = r3 * r3;
	r1.w = dot(r0.yzw, c7.yzw);
	r0.yzw = r0.yzw + c5.www;
	r0.y = dot(r0.yzw, c7.yzw);
	r0.y = (r0.y * c9.x) + c9.y;
	r0.y = frac(r0.y);
	r0.y = (r0.y * c9.z) + c9.w;
	r5.y = sin(r0.y);
	r0.yzw = r5.yyy * c8.xyz;
	r0.yzw = frac(r0.yzw);
	r0.yzw = r0.yzw + -c9.yyy;
	r4.w = dot(r0.yzw, r2.xyz);
	r0.y = (r1.w * c9.x) + c9.y;
	r0.y = frac(r0.y);
	r0.y = (r0.y * c9.z) + c9.w;
	r2.y = sin(r0.y);
	r0.yzw = r2.yyy * c8.xyz;
	r0.yzw = frac(r0.yzw);
	r0.yzw = r0.yzw + -c9.yyy;
	r4.x = dot(r0.yzw, r1.xyz);
	r1 = r4 * r3;
	r0.y = dot(r1, c8.wwww);
	r0.x = (abs(r0.y) * c9.y) + r0.x;
	r1.xy = c11.zz * v0.xy;
	r1.z = c2.x;
	r0.y = dot(r1.xyz, c5.yyy);
	r0.yzw = r1.xyz + r0.yyy;
	r2.xyz = frac(r0.yzw);
	r0.yzw = r0.yzw + -r2.xyz;
	r1.xyz = r1.xyz + -r0.yzw;
	r1.w = dot(r0.yzw, c5.zzz);
	r1.xyz = r1.xyz + r1.www;
	r2.xyz = -r1.yzx + r1.xyz;
	r3.x = ((r2.z >= 0.0) ? c6.x : c6.y);
	r3.y = ((r2.x >= 0.0) ? c6.x : c6.y);
	r3.z = ((r2.y >= 0.0) ? c6.x : c6.y);
	r2.x = ((r2.x >= 0.0) ? c6.y : c6.x);
	r2.y = ((r2.y >= 0.0) ? c6.y : c6.x);
	r2.z = ((r2.z >= 0.0) ? c6.y : c6.x);
	r4.xyz = (r3.yzx * r2.zxy) + r0.yzw;
	r1.w = dot(r4.xyz, c7.yzw);
	r1.w = (r1.w * c9.x) + c9.y;
	r1.w = frac(r1.w);
	r1.w = (r1.w * c9.z) + c9.w;
	r4.y = sin(r1.w);
	r4.xyz = r4.yyy * c8.xyz;
	r4.xyz = frac(r4.xyz);
	r4.xyz = r4.xyz + -c9.yyy;
	r5.xyz = (r3.yzx * -r2.zxy) + r1.xyz;
	r2.xyz = (r3.xyz * -r2.xyz) + c5.www;
	r3.xyz = r5.xyz + c5.zzz;
	r4.y = dot(r4.xyz, r3.xyz);
	r3.y = dot(r3.xyz, r3.xyz);
	r5.xyz = r1.xyz + -r2.xyz;
	r2.xyz = r0.yzw + r2.xyz;
	r1.w = dot(r2.xyz, c7.yzw);
	r1.w = (r1.w * c9.x) + c9.y;
	r1.w = frac(r1.w);
	r1.w = (r1.w * c9.z) + c9.w;
	r2.y = sin(r1.w);
	r2.xyz = r2.yyy * c8.xyz;
	r2.xyz = frac(r2.xyz);
	r2.xyz = r2.xyz + -c9.yyy;
	r5.xyz = r5.xyz + c6.zzz;
	r3.z = dot(r5.xyz, r5.xyz);
	r4.z = dot(r2.xyz, r5.xyz);
	r3.x = dot(r1.xyz, r1.xyz);
	r2.xyz = r1.xyz + c6.www;
	r3.w = dot(r2.xyz, r2.xyz);
	r3 = -r3 + c7.xxxx;
	r5 = max(r3, c6.yyyy);
	r3 = r5 * r5;
	r3 = r3 * r3;
	r1.w = dot(r0.yzw, c7.yzw);
	r0.yzw = r0.yzw + c5.www;
	r0.y = dot(r0.yzw, c7.yzw);
	r0.y = (r0.y * c9.x) + c9.y;
	r0.y = frac(r0.y);
	r0.y = (r0.y * c9.z) + c9.w;
	r5.y = sin(r0.y);
	r0.yzw = r5.yyy * c8.xyz;
	r0.yzw = frac(r0.yzw);
	r0.yzw = r0.yzw + -c9.yyy;
	r4.w = dot(r0.yzw, r2.xyz);
	r0.y = (r1.w * c9.x) + c9.y;
	r0.y = frac(r0.y);
	r0.y = (r0.y * c9.z) + c9.w;
	r2.y = sin(r0.y);
	r0.yzw = r2.yyy * c8.xyz;
	r0.yzw = frac(r0.yzw);
	r0.yzw = r0.yzw + -c9.yyy;
	r4.x = dot(r0.yzw, r1.xyz);
	r1 = r4 * r3;
	r0.y = dot(r1, c8.wwww);
	r0.x = (abs(r0.y) * c11.w) + r0.x;
	r1.xy = c10.xx * v0.xy;
	r1.z = c3.x;
	r0.y = dot(r1.xyz, c5.yyy);
	r0.yzw = r1.xyz + r0.yyy;
	r2.xyz = frac(r0.yzw);
	r0.yzw = r0.yzw + -r2.xyz;
	r1.xyz = r1.xyz + -r0.yzw;
	r1.w = dot(r0.yzw, c5.zzz);
	r1.xyz = r1.xyz + r1.www;
	r2.xyz = -r1.yzx + r1.xyz;
	r3.x = ((r2.z >= 0.0) ? c6.x : c6.y);
	r3.y = ((r2.x >= 0.0) ? c6.x : c6.y);
	r3.z = ((r2.y >= 0.0) ? c6.x : c6.y);
	r2.x = ((r2.x >= 0.0) ? c6.y : c6.x);
	r2.y = ((r2.y >= 0.0) ? c6.y : c6.x);
	r2.z = ((r2.z >= 0.0) ? c6.y : c6.x);
	r4.xyz = (r3.yzx * r2.zxy) + r0.yzw;
	r1.w = dot(r4.xyz, c7.yzw);
	r1.w = (r1.w * c9.x) + c9.y;
	r1.w = frac(r1.w);
	r1.w = (r1.w * c9.z) + c9.w;
	r4.y = sin(r1.w);
	r4.xyz = r4.yyy * c8.xyz;
	r4.xyz = frac(r4.xyz);
	r4.xyz = r4.xyz + -c9.yyy;
	r5.xyz = (r3.yzx * -r2.zxy) + r1.xyz;
	r2.xyz = (r3.xyz * -r2.xyz) + c5.www;
	r3.xyz = r5.xyz + c5.zzz;
	r4.y = dot(r4.xyz, r3.xyz);
	r3.y = dot(r3.xyz, r3.xyz);
	r5.xyz = r1.xyz + -r2.xyz;
	r2.xyz = r0.yzw + r2.xyz;
	r1.w = dot(r2.xyz, c7.yzw);
	r1.w = (r1.w * c9.x) + c9.y;
	r1.w = frac(r1.w);
	r1.w = (r1.w * c9.z) + c9.w;
	r2.y = sin(r1.w);
	r2.xyz = r2.yyy * c8.xyz;
	r2.xyz = frac(r2.xyz);
	r2.xyz = r2.xyz + -c9.yyy;
	r5.xyz = r5.xyz + c6.zzz;
	r3.z = dot(r5.xyz, r5.xyz);
	r4.z = dot(r2.xyz, r5.xyz);
	r3.x = dot(r1.xyz, r1.xyz);
	r2.xyz = r1.xyz + c6.www;
	r3.w = dot(r2.xyz, r2.xyz);
	r3 = -r3 + c7.xxxx;
	r5 = max(r3, c6.yyyy);
	r3 = r5 * r5;
	r3 = r3 * r3;
	r1.w = dot(r0.yzw, c7.yzw);
	r0.yzw = r0.yzw + c5.www;
	r0.y = dot(r0.yzw, c7.yzw);
	r0.y = (r0.y * c9.x) + c9.y;
	r0.y = frac(r0.y);
	r0.y = (r0.y * c9.z) + c9.w;
	r5.y = sin(r0.y);
	r0.yzw = r5.yyy * c8.xyz;
	r0.yzw = frac(r0.yzw);
	r0.yzw = r0.yzw + -c9.yyy;
	r4.w = dot(r0.yzw, r2.xyz);
	r0.y = (r1.w * c9.x) + c9.y;
	r0.y = frac(r0.y);
	r0.y = (r0.y * c9.z) + c9.w;
	r2.y = sin(r0.y);
	r0.yzw = r2.yyy * c8.xyz;
	r0.yzw = frac(r0.yzw);
	r0.yzw = r0.yzw + -c9.yyy;
	r4.x = dot(r0.yzw, r1.xyz);
	r1 = r4 * r3;
	r0.y = dot(r1, c8.wwww);
	r0.x = (abs(r0.y) * c10.y) + r0.x;
	r0.x = r0.x + -c4.x;
	oC0.w = ((r0.x >= 0.0) ? c6.y : c6.x);
	oC0.xyz = c6.yyy;
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
