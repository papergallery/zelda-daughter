// D-28 (ADR-0010, пилот «уголок f1»): рисованные куски мира и рисованная земля.
// Приёмы — ref2game (MIT, github.com/studioigor/ref2game, references/art.md §1, §8; effects.md): картинка — только «сырая краска»,
// свет, тень, туман, огонь и движение — код. Код свой.
// Карточка (по умолчанию): billboard, повёрнутый к ортокамере (SceneBuilder.Painted), одна глубина — глубина корня, поэтому
//   перекрытие с героиней (тоже billboard) — по стопам. Alpha clip + запись глубины, сортировка прозрачных не нужна.
//   Свет — как у земли (нормаль вверх) + точечные огни со всех сторон (как _SPRITELIT Zelda/Toon), без приёма теней (своя 3D-модель —
//   невидимый заместитель тени, ShadowsOnly, иначе она затеняет собственный рисунок). Ночью окружение синее — синеют и рисунки.
//   Крона, которая стоит перед героиней и закрывает её, растворяется дизерингом (радиус _Fade.x м вокруг груди, выше _Fade.y м по карточке).
//   В DepthNormals — нормаль вверх, как у земли: перо постпрохода видит только силуэт карточки (по глубине), без второй линии по рисунку.
// Земля (_PAINTED_GROUND): две бесшовные акварельные заливки по мировым XZ (луг, дорога) с разбивкой повтора, маска дороги — запечённая
//   сборщиком текстура по `paths` сцены, край — рваный (шум) и чуть темнее; тени и свет — как у Zelda/Toon.
Shader "Zelda/Painted"
{
    Properties
    {
        [MainTexture] _BaseMap("Atlas / meadow", 2D) = "white" {}
        _RoadMap("Road wash (ground)", 2D) = "white" {}
        _MaskMap("Road mask (ground)", 2D) = "black" {}
        _MaskRect("Mask rect xz min, size", Vector) = (-32, -32, 64, 64)
        _Tile("Tile m: meadow, road", Vector) = (4, 2.5, 0, 0)
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        _ShadowTint("Shadow Tint (cool umber)", Color) = (0.56,0.55,0.66,1)
        _Steps("Light Steps", Range(2, 4)) = 3
        _Softness("Step Softness", Range(0.005, 0.5)) = 0.07
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        _Fade("Fade: radius m, from height m, strength, chest m", Vector) = (1.3, 1.3, 0.92, 0.9)
        _Wind("Wind sway m at 4 m", Float) = 0.03
        [Toggle(_PAINTED_GROUND)] _Ground("Painted ground", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_RoadMap); SAMPLER(sampler_RoadMap);
        TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _MaskRect;
            float4 _Tile;
            half4 _BaseColor;
            half4 _ShadowTint;
            half _Steps;
            half _Softness;
            half _Cutoff;
            float4 _Fade;
            float _Wind;
        CBUFFER_END

        float4 _ZD_HeroPos; // xyz — стопы героини (HeroView, D-25)

        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv : TEXCOORD0;
            float4 card : TEXCOORD1;   // x — высота точки над основанием карточки, м; yzw — корень карточки в осях объекта
            float4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        // Корень карточки в мире и сдвиг ветром (выше — сильнее, фаза по месту).
        float3 CardWorld(Attributes v, out float3 rootWS)
        {
            rootWS = TransformObjectToWorld(v.card.yzw);
            float3 p = TransformObjectToWorld(v.positionOS.xyz);
            #if !defined(_PAINTED_GROUND)
            float h = max(v.card.x, 0.0);
            float k = h * h / 16.0;
            float ph = rootWS.x * 0.37 + rootWS.z * 0.53;
            p.x += _Wind * k * (sin(_Time.y * 1.3 + ph) + 0.4 * sin(_Time.y * 2.9 + ph * 1.7));
            #endif
            return p;
        }

        // 4×4 Байер: порог 0..1 по пикселю экрана.
        static const float kBayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
        float Bayer(float2 pix)
        {
            uint2 q = uint2(pix) & 3u;
            return (kBayer[q.y * 4u + q.x] + 0.5) / 16.0;
        }

        // Растворение кроны перед героиней: карточка ближе к камере, чем героиня, пиксель выше _Fade.y по карточке и в радиусе от груди.
        void CardFade(float3 posWS, float3 rootWS, float height, float4 positionCS)
        {
            #if !defined(_PAINTED_GROUND)
            if (_ZD_HeroPos.w < 0.5) return;
            float3 hero = mul(UNITY_MATRIX_V, float4(_ZD_HeroPos.xyz + float3(0, _Fade.w, 0), 1)).xyz;
            float3 root = mul(UNITY_MATRIX_V, float4(rootWS, 1)).xyz;
            float3 frag = mul(UNITY_MATRIX_V, float4(posWS, 1)).xyz;
            float inFront = step(0.25, root.z - hero.z); // вид смотрит в −z: ближе к камере — больше z
            float d = length(frag.xy - hero.xy);
            float f = inFront * smoothstep(_Fade.x, _Fade.x * 0.55, d) * smoothstep(_Fade.y, _Fade.y + 0.35, height) * _Fade.z;
            clip(Bayer(positionCS.xy) - f - 1e-4);
            #endif
        }
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
            #pragma shader_feature_local _PAINTED_GROUND
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 card : TEXCOORD2;   // x — высота, yzw — корень в мире
                half fogFactor : TEXCOORD3;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            half Ramp(half x)
            {
                half t = saturate(x) * _Steps;
                half w = _Softness * _Steps * 0.5h;
                half s = smoothstep(1.0h - w, 1.0h + w, t) + smoothstep(2.0h - w, 2.0h + w, t);
                s += _Steps > 3.5h ? smoothstep(3.0h - w, 3.0h + w, t) : 0.0h;
                return s / (_Steps - 1.0h);
            }

            float PaintedHash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float PaintedNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(PaintedHash(i), PaintedHash(i + float2(1, 0)), f.x), lerp(PaintedHash(i + float2(0, 1)), PaintedHash(i + float2(1, 1)), f.x), f.y);
            }

            // Заливка без видимого повтора: две выборки (вторая — повёрнута и в другом масштабе), смесь по низкочастотному шуму.
            half3 Wash(TEXTURE2D_PARAM(tex, smp), float2 xz, float tile)
            {
                float2 a = xz / tile;
                float2 b = float2(a.x * 0.80 - a.y * 0.60, a.x * 0.60 + a.y * 0.80) * 0.77 + 0.37;
                half3 ca = SAMPLE_TEXTURE2D(tex, smp, a).rgb;
                half3 cb = SAMPLE_TEXTURE2D(tex, smp, b).rgb;
                half k = smoothstep(0.3, 0.7, PaintedNoise(xz / (tile * 1.7)));
                return lerp(ca, cb, k);
            }

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                float3 rootWS;
                float3 p = CardWorld(v, rootWS);
                o.positionWS = p;
                o.positionCS = TransformWorldToHClip(p);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.card = float4(v.card.x, rootWS);
                o.fogFactor = ComputeFogFactorZ0ToFar(-TransformWorldToView(p).z);
                o.color = half4(v.color);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half3 n = half3(0, 1, 0);
                half3 albedo;
                half receive = 1.0h;
                #if defined(_PAINTED_GROUND)
                {
                    float2 xz = i.positionWS.xz;
                    half3 meadow = Wash(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), xz, _Tile.x);
                    half3 road = Wash(TEXTURE2D_ARGS(_RoadMap, sampler_RoadMap), xz, _Tile.y);
                    float2 muv = (xz - _MaskRect.xy) / _MaskRect.zw;
                    half m = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, muv).r;
                    half rag = (PaintedNoise(xz * 2.3) - 0.5) * 0.22h + (PaintedNoise(xz * 0.6) - 0.5) * 0.18h;
                    half r = smoothstep(0.46h, 0.54h, m + rag);
                    half rim = smoothstep(0.3h, 0.5h, m + rag) * (1.0h - smoothstep(0.5h, 0.62h, m + rag));
                    albedo = lerp(meadow, road, r) * (1.0h - rim * 0.22h);
                    albedo *= _BaseColor.rgb;
                }
                #else
                {
                    half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                    clip(tex.a - _Cutoff);
                    CardFade(i.positionWS, i.card.yzw, i.card.x, i.positionCS);
                    albedo = tex.rgb * _BaseColor.rgb * i.color.rgb;
                    receive = 0.0h;
                }
                #endif

                half3 ambient = SampleSH(n);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(i.positionWS));
                #else
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                #endif
                Light main = GetMainLight(shadowCoord);
                half shadow = lerp(1.0h, smoothstep(0.15h, 0.85h, main.shadowAttenuation), receive);
                half band = Ramp(dot(n, main.direction) * 0.5h + 0.5h) * shadow;
                half3 light = ambient * lerp(_ShadowTint.rgb, half3(1, 1, 1), band) + main.color * (band * main.distanceAttenuation);

                #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                for (uint li = 0u; li < count; li++)
                {
                    Light l = GetAdditionalLight(li, i.positionWS);
                    half atten = l.distanceAttenuation * l.shadowAttenuation;
                    half ring = lerp(atten, Ramp(atten), 0.12h);
                    #if defined(_PAINTED_GROUND)
                    half nd = dot(n, l.direction) * 0.5h + 0.5h;
                    #else
                    half nd = 1.0h; // карточку огонь освещает с любой стороны
                    #endif
                    light += l.color * (Ramp(nd) * ring);
                }
                #endif

                half3 c = MixFog(albedo * light, i.fogFactor);
                return half4(c, 1.0h);
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
            #pragma shader_feature_local _PAINTED_GROUND
            #pragma multi_compile_instancing

            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; float4 card : TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                float3 rootWS;
                float3 p = CardWorld(v, rootWS);
                o.positionWS = p;
                o.positionCS = TransformWorldToHClip(p);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.card = float4(v.card.x, rootWS);
                return o;
            }

            half frag(Varyings i) : SV_Target
            {
                #if !defined(_PAINTED_GROUND)
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff);
                CardFade(i.positionWS, i.card.yzw, i.card.x, i.positionCS);
                #endif
                return i.positionCS.z;
            }
            ENDHLSL
        }

        // Перо постпрохода читает глубину и нормали: нормаль — вверх (как у земли), линия остаётся только по силуэту карточки.
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
            #pragma shader_feature_local _PAINTED_GROUND
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; float4 card : TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                float3 rootWS;
                float3 p = CardWorld(v, rootWS);
                o.positionWS = p;
                o.positionCS = TransformWorldToHClip(p);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.card = float4(v.card.x, rootWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                #if !defined(_PAINTED_GROUND)
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff);
                CardFade(i.positionWS, i.card.yzw, i.card.x, i.positionCS);
                #endif
                float3 n = float3(0, 1, 0);
                #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct = PackNormalOctQuadEncode(n);
                return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0.0);
                #else
                return half4(n, 0.0);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack Off
}
