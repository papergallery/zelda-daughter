// D-08: the toon material of every model (ModelLook) and of the ground / primitives (SceneBuilder).
// 2-4 light steps with a soft border, warm (not grey) shadows, main-light shadows received, additional (point) lights per pixel,
// fog by eye depth (works for the orthographic iso camera, unlike the stock fog macro). Forward rendering only (no Forward+).
// _BaseMap × _BaseColor (× vertex colour when _VERTEXCOLOR) — Kenney palette / Quaternius trim sheets / flat colours.
Shader "Zelda/Toon"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Colour", Color) = (1,1,1,1)
        _ShadowTint("Shadow Tint (warm)", Color) = (0.66,0.52,0.52,1)
        _Steps("Light Steps", Range(2, 4)) = 3
        _Softness("Step Softness", Range(0.005, 0.5)) = 0.07
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clip (sprites)", Float) = 0
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        [Toggle(_VERTEXCOLOR)] _UseVertexColor("Vertex Colour", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _ShadowTint;
            half _Steps;
            half _Softness;
            half _Cutoff;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local _VERTEXCOLOR
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // 0..1 -> stepped 0..1 with soft borders between the steps.
            half Ramp(half x)
            {
                half t = saturate(x) * _Steps;
                half w = _Softness * _Steps * 0.5h;
                half s = smoothstep(1.0h - w, 1.0h + w, t) + smoothstep(2.0h - w, 2.0h + w, t);
                s += _Steps > 3.5h ? smoothstep(3.0h - w, 3.0h + w, t) : 0.0h;
                return s / (_Steps - 1.0h);
            }

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                VertexPositionInputs vp = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = vp.positionCS;
                o.positionWS = vp.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.shadowCoord = GetShadowCoord(vp);
                // Eye depth along the view axis: right for both the orthographic iso camera and a perspective one.
                o.fogFactor = ComputeFogFactorZ0ToFar(-vp.positionVS.z);
                o.color = half4(input.color);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half4 albedo = tex * _BaseColor;
                #if defined(_VERTEXCOLOR)
                albedo.rgb *= i.color.rgb;
                #endif
                #if defined(_ALPHATEST_ON)
                clip(albedo.a - _Cutoff);
                #endif

                half3 n = normalize(i.normalWS);
                half3 ambient = SampleSH(n);
                half3 shadowTint = _ShadowTint.rgb;

                Light main = GetMainLight(i.shadowCoord);
                half shadow = smoothstep(0.3h, 0.7h, main.shadowAttenuation);
                half band = Ramp(dot(n, main.direction) * 0.5h + 0.5h) * shadow;
                // Shadows keep the warm tint of the ambient light instead of going grey; the night ambient is blue, so are night shadows.
                half3 light = ambient * lerp(shadowTint, half3(1, 1, 1), band) + main.color * (band * main.distanceAttenuation);

                #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                for (uint li = 0u; li < count; li++)
                {
                    Light l = GetAdditionalLight(li, i.positionWS);
                    half atten = l.distanceAttenuation * l.shadowAttenuation;
                    half ring = lerp(atten, Ramp(atten), 0.6h);
                    half nd = dot(n, l.direction) * 0.5h + 0.5h;
                    light += l.color * (Ramp(nd) * ring);
                }
                #endif

                half3 c = albedo.rgb * light;
                c = MixFog(c, i.fogFactor);
                return half4(c, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirectionWS = _LightDirection;
                #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE * cs.w);
                #else
                cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE * cs.w);
                #endif
                o.positionCS = cs;
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half frag(Varyings i) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a - _Cutoff);
                #endif
                return i.positionCS.z;
            }
            ENDHLSL
        }

        // The prepass the outline of the watercolor pass reads (depth + world normals).
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a - _Cutoff);
                #endif
                float3 n = normalize(i.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct = PackNormalOctQuadEncode(n);
                float2 remapped = saturate(oct * 0.5 + 0.5);
                return half4(PackFloat2To888(remapped), 0.0);
                #else
                return half4(n, 0.0);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack Off
}
