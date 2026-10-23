Shader "Celeste/Effects/d2b0b42a13855467e6654e739684337ba813524c9db74f1a3bc36a30ca91711c"
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
	float4 _CelesteVS[25];
};

struct CelesteVSInput
{
	float4 m_v0 : POSITION0;
	float4 m_v1 : NORMAL0;
};

struct CelesteVSOutput
{
	float4 m_oD0 : COLOR0;
	float4 m_oD1 : COLOR1;
	float4 m_oPos : SV_Position;
};

CelesteVSOutput CelesteSourceVS(CelesteVSInput input)
{
	CelesteVSOutput output = (CelesteVSOutput) 0;
	const float4 c22 = float4(0.0, 1.0, 0.0, 0.0);
	float4 r0;
	float4 r1;
	float4 r2;
	float4 r3;
	float4 r4;
	float4 r5;
	#define c0 _CelesteVS[0]
	#define c1 _CelesteVS[1]
	#define c2 _CelesteVS[2]
	#define c3 _CelesteVS[3]
	#define c4 _CelesteVS[4]
	#define c5 _CelesteVS[5]
	#define c6 _CelesteVS[6]
	#define c7 _CelesteVS[7]
	#define c8 _CelesteVS[8]
	#define c9 _CelesteVS[9]
	#define c10 _CelesteVS[10]
	#define c11 _CelesteVS[11]
	#define c12 _CelesteVS[12]
	#define c13 _CelesteVS[13]
	#define c14 _CelesteVS[14]
	#define c15 _CelesteVS[15]
	#define c16 _CelesteVS[16]
	#define c17 _CelesteVS[17]
	#define c18 _CelesteVS[18]
	#define c19 _CelesteVS[19]
	#define c20 _CelesteVS[20]
	#define c21 _CelesteVS[21]
	#define c23 _CelesteVS[22]
	#define c24 _CelesteVS[23]
	#define c25 _CelesteVS[24]
	#define v0 input.m_v0
	#define v1 input.m_v1
	#define oPos output.m_oPos
	#define oD0 output.m_oD0
	#define oD1 output.m_oD1
	r0.x = dot(v1.xyz, c23.xyz);
	r0.y = dot(v1.xyz, c24.xyz);
	r0.z = dot(v1.xyz, c25.xyz);
	r1.xyz = normalize(r0.xyz);
	r0.x = dot(-c4.xyz, r1.xyz);
	r0.y = dot(-c7.xyz, r1.xyz);
	r0.z = dot(-c10.xyz, r1.xyz);
	r2.xyz = r0.xyz >= c22.xxx;
	r0.xyz = r0.xyz * r2.xyz;
	r3.xyz = r0.yyy * c8.xyz;
	r0.xyw = (r0.xxx * c5.xyz) + r3.xyz;
	r0.xyz = (r0.zzz * c11.xyz) + r0.xyw;
	r3.xyz = c0.xyz;
	oD0.xyz = (r0.xyz * r3.xyz) + c1.xyz;
	r0.x = dot(v0, c19);
	r0.y = dot(v0, c20);
	r0.z = dot(v0, c21);
	r0.xyz = -r0.xyz + c13.xyz;
	r3.xyz = normalize(r0.xyz);
	r0.xyz = r3.xyz + -c4.xyz;
	r4.xyz = normalize(r0.xyz);
	r0.x = dot(r4.xyz, r1.xyz);
	r4.xyz = r3.xyz + -c7.xyz;
	r3.xyz = r3.xyz + -c10.xyz;
	r5.xyz = normalize(r3.xyz);
	r0.z = dot(r5.xyz, r1.xyz);
	r3.xyz = normalize(r4.xyz);
	r0.y = dot(r3.xyz, r1.xyz);
	r0.xyz = max(r0.xyz, c22.xxx);
	r0.xyz = r2.xyz * r0.xyz;
	r1.x = log2(r0.x);
	r1.y = log2(r0.y);
	r1.z = log2(r0.z);
	r0.xyz = r1.xyz * c3.xxx;
	r0.x = exp2(r0.x);
	r0.y = exp2(r0.y);
	r0.z = exp2(r0.z);
	r1.xyz = r0.yyy * c9.xyz;
	r0.xyw = (r0.xxx * c6.xyz) + r1.xyz;
	r0.xyz = (r0.zzz * c12.xyz) + r0.xyw;
	oD1.xyz = r0.xyz * c2.xyz;
	oPos.x = dot(v0, c15);
	oPos.y = dot(v0, c16);
	oPos.z = dot(v0, c17);
	oPos.w = dot(v0, c18);
	r0.x = dot(v0, c14);
	r0.x = max(r0.x, c22.x);
	oD1.w = min(r0.x, c22.y);
	oD0.w = c0.w;
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
	#undef c21
	#undef c23
	#undef c24
	#undef c25
	#undef v0
	#undef v1
	#undef oPos
	#undef oD0
	#undef oD1
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

struct CelestePSInput
{
	float4 m_oD0 : COLOR0;
	float4 m_oD1 : COLOR1;
	float4 m_oPos : SV_Position;
};

struct CelestePSOutput
{
	float4 m_oC0 : SV_Target0;
};

CelestePSOutput CelestePS(CelestePSInput input)
{
	CelestePSOutput output = (CelestePSOutput) 0;
	float4 r0;
	#define v0 input.m_oD0
	#define v1 input.m_oD1
	#define oC0 output.m_oC0
	r0 = v0;
	r0.xyz = (v1.xyz * r0.www) + r0.xyz;
	r0.w = v0.w;
	oC0 = r0;
	#undef v0
	#undef v1
	#undef oC0
	return output;
}


            ENDHLSL
        }
    }
}
