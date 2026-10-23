Shader "Celeste/Mountain"
{
    Properties{_MainTex("Original terrain", 2D) = "white" {} _NextTex("Next terrain", 2D) =
                   "white" {} _Cull("Cull", Float) = 1} SubShader
    {
        Cull[_Cull] ZWrite On ZTest LEqual Blend One OneMinusSrcAlpha Pass
        {
            CGPROGRAM
#pragma target 3.5
#pragma vertex vert
#pragma fragment frag
            struct Input
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            struct Output
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 local : TEXCOORD1;
            };
            float4x4 _DrawMatrix;
            Texture2D _MainTex, _NextTex;
            SamplerState sampler_linear_clamp;
            float3 _Fog;
            float _Ease;
            Output vert(Input v)
            {
                Output o;
                o.vertex = mul(_DrawMatrix, v.vertex);
                o.uv = v.uv;
                o.local = v.vertex.xyz;
                return o;
            }
            float4 frag(Output i) : SV_Target
            {
                // Installed MountainRender.xnb Single/Easing pixel programs.
                // Fog uses the untransformed model position, including the core door.
                float fog = saturate((length(i.local.xz) * .055555556 - .25) * 1.33333337);
                float3 color = lerp(_MainTex.Sample(sampler_linear_clamp, i.uv).rgb,
                                    _NextTex.Sample(sampler_linear_clamp, i.uv).rgb, _Ease);
                return float4(color + fog * (_Fog - color), 1);
            }
            ENDCG
        }
    }
}
