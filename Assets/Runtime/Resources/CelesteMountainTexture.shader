Shader "Celeste/MountainTexture"
{
    Properties{_MainTex("Original texture", 2D) = "white" {} _Cull("Cull", Float) =
                   0 _DepthWrite("Depth write", Float) = 1 _DepthTest("Depth test", Float) = 4} SubShader
    {
        Cull[_Cull] ZWrite[_DepthWrite] ZTest[_DepthTest] Blend One OneMinusSrcAlpha Pass
        {
            CGPROGRAM
#pragma target 3.5
#pragma vertex vert
#pragma fragment frag
            struct Input
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            struct Output
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            float4x4 _DrawMatrix;
            Texture2D _MainTex;
            SamplerState sampler_linear_clamp, sampler_linear_repeat;
            float _Wrap;
            float4 _Tint;
            Output vert(Input v)
            {
                Output o;
                o.vertex = mul(_DrawMatrix, v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Tint;
                return o;
            }
            float4 frag(Output i) : SV_Target
            {
                return (_Wrap > 0 ? _MainTex.Sample(sampler_linear_repeat, i.uv)
                                  : _MainTex.Sample(sampler_linear_clamp, i.uv)) *
                       i.color;
            }
            ENDCG
        }
    }
}
