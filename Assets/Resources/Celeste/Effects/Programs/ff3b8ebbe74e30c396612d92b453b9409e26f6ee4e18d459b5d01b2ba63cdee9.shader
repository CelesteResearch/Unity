Shader "Celeste/Effects/ff3b8ebbe74e30c396612d92b453b9409e26f6ee4e18d459b5d01b2ba63cdee9"
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
	float4 _CelesteVS[6];
};

struct CelesteVSInput
{
	float4 m_v0 : POSITION0;
	float4 m_v1 : COLOR0;
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
	const float4 c1 = float4(0.0, 1.0, 0.0, 0.0);
	float4 r0;
	#define c0 _CelesteVS[0]
	#define c14 _CelesteVS[1]
	#define c15 _CelesteVS[2]
	#define c16 _CelesteVS[3]
	#define c17 _CelesteVS[4]
	#define c18 _CelesteVS[5]
	#define v0 input.m_v0
	#define v1 input.m_v1
	#define oPos output.m_oPos
	#define oD0 output.m_oD0
	#define oD1 output.m_oD1
	oPos.x = dot(v0, c15);
	oPos.y = dot(v0, c16);
	oPos.z = dot(v0, c17);
	oPos.w = dot(v0, c18);
	r0.x = dot(v0, c14);
	r0.x = max(r0.x, c1.x);
	oD1.w = min(r0.x, c1.y);
	oD0 = v1 * c0;
	oD1.xyz = c1.xxx;
	#undef c0
	#undef c14
	#undef c15
	#undef c16
	#undef c17
	#undef c18
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

cbuffer CelestePSUniforms
{
	float4 _CelestePS[1];
};

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
	float4 r1;
	#define c0 _CelestePS[0]
	#define v0 input.m_oD0
	#define v1 input.m_oD1
	#define oC0 output.m_oC0
	r0.xyz = (c0.xyz * v0.www) + -v0.xyz;
	r1.xyz = v0.xyz;
	r0.xyz = (v1.www * r0.xyz) + r1.xyz;
	r0.w = v0.w;
	oC0 = r0;
	#undef c0
	#undef v0
	#undef v1
	#undef oC0
	return output;
}


            ENDHLSL
        }
    }
}
