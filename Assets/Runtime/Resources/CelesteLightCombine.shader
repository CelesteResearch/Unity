Shader "Celeste/LightCombine"
{
    Properties
    {
        _MainTex("Packed light channels", 2D) = "black"
        {
        }
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always Blend SrcAlpha One Pass
        {
            CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma target 3.0
            struct Input
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 mask : TEXCOORD1;
                float4 color : COLOR;
            };
            struct Output
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 mask : TEXCOORD1;
                float4 color : COLOR;
            };
            sampler2D _MainTex;
            float4x4 _DrawMatrix;
            Output vert(Input v)
            {
                Output o;
                o.vertex = mul(_DrawMatrix, v.vertex);
                o.uv = v.uv;
                o.mask = v.mask;
                o.color = v.color;
                return o;
            }
            float4 frag(Output i) : SV_Target
            {
                return dot(tex2D(_MainTex, i.uv), i.mask) * i.color;
            }
            ENDCG
        }
    }
}
