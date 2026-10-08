// D-16: the flat, unlit, alpha-blended shader of the elements' effects: rain streaks (particles), mud puddles, burnt patches (instanced),
// flame and smoke puffs. Colour = _Color × the particle / vertex colour; _Disc 1 — a soft round blob by the UVs (decals, puffs),
// 0 — a plain rectangle (rain streaks). No light: effects are drawn after the opaque world, depth-tested, never written.
Shader "Zelda/NatureFx"
{
    Properties
    {
        _Color("Colour", Color) = (1,1,1,1)
        _Disc("Soft disc (1) or plain (0)", Range(0, 1)) = 1
        _Edge("Disc edge softness", Range(0.02, 1)) = 0.45
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst", Float) = 10
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "NatureFx"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Disc;
                half _Edge;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                o.color = input.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 c = _Color * i.color;
                half r = length(i.uv * 2.0h - 1.0h);
                half blob = saturate((1.0h - r) / max(_Edge, 0.02h));
                c.a *= lerp(1.0h, blob, _Disc);
                return c;
            }
            ENDHLSL
        }
    }
}
