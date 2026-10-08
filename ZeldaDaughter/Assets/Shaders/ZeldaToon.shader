// D-08: the toon material of every model (ModelLook) and of the ground / primitives (SceneBuilder).
// 2-4 light steps with a soft border, warm (not grey) shadows, main-light shadows received, additional (point) lights per pixel,
// fog by eye depth (works for the orthographic iso camera, unlike the stock fog macro). Forward rendering only (no Forward+).
// _BaseMap × _BaseColor (× vertex colour when _VERTEXCOLOR) — Kenney palette / Quaternius trim sheets / flat colours.
// D-21: shadows are cool umber-blue (not black, not warm brown), softer border; _Grade regrades a palette texture the way
// ModelLook.Grade regrades a flat colour (olive greens, brown wood); _SPRITELIT = the billboard sprite material: lit like the ground
// (normal up) and by the additional lights (campfire, torch) all around, so the figure and the things near the fire catch the fire.
// D-22b: _ZD_GROUND = the one material of the ground: no ribbons or discs on top (their 2 cm steps got a dotted pen line). The colour is painted
// from world position: grass in three tones by large and small noise washes, a sandy road with a ragged grassy edge, a cobbled square, a damp
// river bank and tilled field — from _GroundMask (core GroundMask: signed distances in metres, R road, G paved, B water, A field) and
// _GroundNoise (tools/art/ground_noise.py: two fbm fields, Voronoi cell tone and its seams). Same lighting, wetness and fog as everything else.
// _ZD_ROOTED = a vegetation card (SceneBuilder.Vegetation): uv2 = the world XZ of its root (or ≥ 1e5 — "the object's origin"), vertex alpha =
// the height on the card 0…1. The lower third takes the colour of the painted ground under the root (no seam), the top sways in a wind of two
// sines with a phase from the root, and cards within 0,6 m of the heroine's feet (_ZD_HeroPos, global, set by HeroView) bend away from her.
// _GroundMask2 R = signed distance to a cool soft shadow spot under trees and rocks (GroundMask.Spots).
Shader "Zelda/Toon"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Colour", Color) = (1,1,1,1)
        _ShadowTint("Shadow Tint (cool umber)", Color) = (0.56,0.55,0.66,1)
        _Steps("Light Steps", Range(2, 4)) = 3
        _Softness("Step Softness", Range(0.005, 0.5)) = 0.07
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clip (sprites)", Float) = 0
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        [Toggle(_VERTEXCOLOR)] _UseVertexColor("Vertex Colour", Float) = 0
        [Toggle(_ZD_GRADE)] _Grade("Regrade the palette texture (olive / brown)", Float) = 0
        [Toggle(_SPRITELIT)] _SpriteLit("Billboard sprite lighting", Float) = 0
        [Toggle(_ZD_GROUND)] _Ground("Painted ground (mask + noise)", Float) = 0
        [NoScaleOffset] _GroundMask("Ground Mask (R road, G paved, B water, A field)", 2D) = "white" {}
        [NoScaleOffset] _GroundMask2("Ground Mask 2 (R shadow spots)", 2D) = "white" {}
        [Toggle(_ZD_ROOTED)] _Rooted("Vegetation card rooted in the painted ground (base colour, wind, push)", Float) = 0
        _Wind("Wind sway (m at the top)", Float) = 0.035
        _Rim("Warm rim of nearby fire on the silhouette (sprite-lit figures)", Float) = 1
        _GroundNoise("Ground Noise", 2D) = "grey" {}
        _GroundRect("Mask rect (minX, minZ, 1/width m, 1/height m)", Vector) = (0,0,0.01,0.01)
        _GrassDark("Grass dark", Color) = (0.34,0.36,0.25,1)
        _GrassMid("Grass mid", Color) = (0.45,0.47,0.30,1)
        _GrassLight("Grass light", Color) = (0.58,0.56,0.36,1)
        _SandDark("Sand dark", Color) = (0.69,0.58,0.40,1)
        _SandLight("Sand light", Color) = (0.86,0.76,0.56,1)
        _CobbleDark("Cobble dark", Color) = (0.55,0.52,0.46,1)
        _CobbleLight("Cobble light", Color) = (0.74,0.70,0.62,1)
        _BankColor("River bank", Color) = (0.36,0.38,0.30,1)
        _FieldColor("Tilled field", Color) = (0.45,0.36,0.26,1)
        [Toggle(_ZD_WATER)] _Water("Painted water (same mask: shallow at the bank, deep in the middle)", Float) = 0
        _WaterShallow("Water shallow", Color) = (0.56,0.59,0.55,1)
        _WaterDeep("Water deep", Color) = (0.28,0.35,0.35,1)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        [HideInInspector] _ZWrite("ZWrite", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_GroundMask);
        SAMPLER(sampler_GroundMask);
        TEXTURE2D(_GroundMask2);
        TEXTURE2D(_GroundNoise);
        SAMPLER(sampler_GroundNoise);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseMap_TexelSize;
            half4 _BaseColor;
            half4 _ShadowTint;
            half _Steps;
            half _Softness;
            half _Cutoff;
            float4 _GroundRect;
            half4 _GrassDark, _GrassMid, _GrassLight, _SandDark, _SandLight, _CobbleDark, _CobbleLight, _BankColor, _FieldColor, _WaterShallow, _WaterDeep;
            float _Wind;
            half _Rim;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local _VERTEXCOLOR
            #pragma shader_feature_local_fragment _ZD_GRADE
            #pragma shader_feature_local_fragment _SPRITELIT
            #pragma shader_feature_local_fragment _ZD_GROUND
            #pragma shader_feature_local_fragment _ZD_WATER
            #pragma shader_feature_local _ZD_ROOTED
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
                float2 uv2 : TEXCOORD1;
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
                float3 root : TEXCOORD5; // _ZD_ROOTED: xy = world XZ of the root, z = height on the card 0…1
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

            // Same idea as ModelLook.Grade (C#): 60 % of the chroma, greens pulled from teal to olive, orange wood to brown, a little warm.
            half3 GradePalette(half3 c)
            {
                half grey = dot(c, half3(0.3h, 0.59h, 0.11h));
                c = lerp(grey.xxx, c, 0.6h);
                half3 hsv = RgbToHsv(c);
                half h = hsv.x, s = hsv.y, v = hsv.z;
                half green = smoothstep(0.16h, 0.22h, h) * (1.0h - smoothstep(0.45h, 0.55h, h));
                h = lerp(h, 0.21h, green * 0.7h);
                s *= lerp(1.0h, 0.5h, green);
                v *= lerp(1.0h, 0.72h, green);
                half wood = smoothstep(0.04h, 0.06h, h) * (1.0h - smoothstep(0.10h, 0.13h, h)) * smoothstep(0.30h, 0.50h, s);
                h = lerp(h, 0.075h, wood * 0.5h);
                s *= lerp(1.0h, 0.42h, wood);
                v *= lerp(1.0h, 0.5h, wood);
                c = HsvToRgb(half3(h, s, v));
                return c * half3(1.05h, 1.0h, 0.86h);
            }

            float4 _ZD_HeroPos; // xyz = the heroine's feet (HeroView, global, D-25); w = 1 when set

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                VertexPositionInputs vp = GetVertexPositionInputs(input.positionOS.xyz);
                #if defined(_ZD_ROOTED)
                {
                    float2 root = input.uv2.x > 1e5 ? TransformObjectToWorld(float3(0, 0, 0)).xz : input.uv2;
                    float hf = input.color.a;
                    float phase = dot(root, float2(0.71, 1.13));
                    float sway = (sin(_Time.y * 1.6 + phase) * 0.65 + sin(_Time.y * 2.7 + phase * 1.7) * 0.35) * _Wind * hf * hf;
                    float3 offset = float3(0.7071, 0, -0.7071) * sway; // across the iso view (the camera's right at yaw 45°)
                    float2 away = root - _ZD_HeroPos.xz;
                    float dist = length(away);
                    float push = (1.0 - saturate(dist / 0.6)) * _ZD_HeroPos.w * hf;
                    offset.xz += away / max(dist, 1e-3) * push * 0.22;
                    offset.y -= push * 0.12;
                    float3 ws = vp.positionWS + offset;
                    vp.positionWS = ws;
                    vp.positionCS = TransformWorldToHClip(ws);
                    vp.positionVS = TransformWorldToView(ws);
                    o.root = float3(root, hf);
                }
                #endif
                o.positionCS = vp.positionCS;
                o.positionWS = vp.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.shadowCoord = GetShadowCoord(vp);
                #if defined(_SPRITELIT) && !defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                {
                    // D-22b: a standing figure takes the sun's shadow where its feet are (she darkens in the shade of a tree), not up the card:
                    // slide the point down the card (the camera's up) to the ground plane y = 0
                    float3 camUp = UNITY_MATRIX_I_V._m01_m11_m21;
                    float3 feet = vp.positionWS - camUp * (vp.positionWS.y / max(camUp.y, 0.2));
                    o.shadowCoord = TransformWorldToShadowCoord(feet + float3(0, 0.05, 0));
                }
                #endif
                // Eye depth along the view axis: right for both the orthographic iso camera and a perspective one.
                o.fogFactor = ComputeFogFactorZ0ToFar(-vp.positionVS.z);
                o.color = half4(input.color);
                return o;
            }

            float _ZD_Wetness; // 0 dry … 1 soaked (NatureFx, global)

            #if defined(_ZD_GROUND) || defined(_ZD_WATER) || defined(_ZD_ROOTED)
            // Noise at a world scale (metres per tile); offsets decorrelate the scales.
            float4 Noise(float2 p, float tile, float2 offset) { return SAMPLE_TEXTURE2D(_GroundNoise, sampler_GroundNoise, p / tile + offset); }

            half3 GroundAlbedo(float2 p)
            {
                float4 big = Noise(p, 31.0, float2(0.13, 0.71));   // washes of 5–15 m
                float4 mid = Noise(p, 9.0, float2(0.57, 0.29));    // brush patches of 1–3 m; B/A — leaf mosaic cells ≈ 0,4 m
                float4 fine = Noise(p, 2.7, float2(0.91, 0.43));   // grain, small cells ≈ 0,12 m
                float4 cob = Noise(p, 8.0, float2(0.0, 0.0));      // cobbles ≈ 0,36 m
                float4 grit = Noise(p, 5.0, float2(0.33, 0.77));   // sand mosaic ≈ 0,23 m (the concept road)

                // grass: three tones in soft-edged washes (the post-effect pools pigment at their borders), leafy mosaic on top
                float t = big.r * 0.62 + mid.g * 0.38;
                half3 grass = lerp(_GrassDark.rgb, _GrassMid.rgb, smoothstep(0.32, 0.48, t));
                grass = lerp(grass, _GrassLight.rgb, smoothstep(0.60, 0.76, t));
                // pigment pools where a wash ends (≈ 0,2–0,3 m band on the edge of each tone patch), as in the concept washes
                grass *= 1.0 - 0.16 * (1.0 - smoothstep(0.0, 0.03, abs(t - 0.40))) - 0.13 * (1.0 - smoothstep(0.0, 0.03, abs(t - 0.68)));
                grass *= 0.95 + 0.1 * mid.b;                                   // soft patches, no visible cells
                grass *= 0.92 + 0.16 * fine.b;                                 // fine leafy mosaic ≈ 0,12 m
                grass *= lerp(0.9, 1.0, smoothstep(0.0, 0.2, fine.a));

                float4 m = (SAMPLE_TEXTURE2D(_GroundMask, sampler_GroundMask, (p - _GroundRect.xy) * _GroundRect.zw) - 0.5) * 8.0; // metres, ±4 (GroundMask.Range)

                // tilled field
                float df = m.a + (mid.r - 0.5) * 0.6;
                half3 field = _FieldColor.rgb * (0.85 + 0.3 * fine.g);
                grass = lerp(grass, field, 1.0 - smoothstep(-0.1, 0.1, df));

                // damp river bank: darker, cooler grass in a ragged band
                float dw = m.b + (mid.r - 0.5) * 0.8;
                grass = lerp(grass, _BankColor.rgb * (0.9 + 0.2 * mid.b), (1.0 - smoothstep(0.0, 1.4, dw)) * 0.7);

                // road: the edge broken by two noise scales; a darker rim inside, a shadowed grass band outside
                float dr = m.r + (mid.r - 0.5) * 1.0 + (fine.g - 0.5) * 0.35;
                grass *= lerp(0.8, 1.0, smoothstep(0.0, 0.7, dr));
                half3 sand = lerp(_SandDark.rgb, _SandLight.rgb, smoothstep(0.25, 0.75, big.g * 0.5 + mid.g * 0.5));
                sand *= 0.9 + 0.16 * grit.b;                                   // flagstone-like mosaic of the concept road
                sand *= lerp(0.88, 1.0, smoothstep(0.0, 0.15, grit.a));
                sand *= 1.0 - 0.22 * (1.0 - smoothstep(0.0, 0.28, -dr));      // pigment pooled at the rim: ≈ 0,25 m, −22 %
                sand *= lerp(0.92, 1.0, smoothstep(0.25, 0.9, -dr));
                half3 c = lerp(grass, sand, 1.0 - smoothstep(-0.04, 0.04, dr));

                // paved square: cobbles with dark seams, sand between at the ragged rim
                float dp = m.g + (mid.r - 0.5) * 0.9;
                half3 stone = lerp(_CobbleDark.rgb, _CobbleLight.rgb, cob.b) * (0.94 + 0.12 * fine.r);
                stone *= lerp(0.72, 1.0, smoothstep(0.02, 0.16, cob.a));
                stone *= 1.0 - 0.2 * (1.0 - smoothstep(0.0, 0.3, -dp));
                c = lerp(c, stone, 1.0 - smoothstep(-0.05, 0.05, dp));

                // cool soft shadow spots under trees, rocks, bushes (stylised AO of the concept): darker and bluer, ragged
                float ds = (SAMPLE_TEXTURE2D(_GroundMask2, sampler_GroundMask, (p - _GroundRect.xy) * _GroundRect.zw).r - 0.5) * 8.0 + (mid.r - 0.5) * 0.7;
                float spot = 1.0 - smoothstep(-0.25, 0.6, ds);
                c = lerp(c, c * half3(0.66, 0.70, 0.82), spot * 0.75);
                return c;
            }

            // D-22b water (concept env-bridge): light grey-green at the bank, darker in the middle, a mosaic of washes with light glints on the seams.
            half3 WaterAlbedo(float2 p)
            {
                float4 big = Noise(p, 23.0, float2(0.41, 0.17));
                float4 mid = Noise(float2(p.x * 0.7, p.y * 1.6), 6.0, float2(0.21, 0.63)); // cells stretched along the flow (the river runs along z)
                float depth = -(SAMPLE_TEXTURE2D(_GroundMask, sampler_GroundMask, (p - _GroundRect.xy) * _GroundRect.zw).b - 0.5) * 8.0; // m from the bank
                half3 c = lerp(_WaterShallow.rgb, _WaterDeep.rgb, smoothstep(0.2, 2.6, depth + (big.r - 0.5) * 1.2));
                c *= 0.9 + 0.18 * mid.b;
                c = lerp(c, c * 1.35 + 0.05, (1.0 - smoothstep(0.0, 0.08, mid.a)) * 0.6);
                return c;
            }
            #endif

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                #if defined(_ZD_GROUND)
                tex = half4(GroundAlbedo(i.positionWS.xz), 1.0h);
                #elif defined(_ZD_WATER)
                tex = half4(WaterAlbedo(i.positionWS.xz), 1.0h);
                #endif
                #if defined(_ZD_GRADE)
                tex.rgb = GradePalette(tex.rgb);
                #endif
                half4 albedo = tex * _BaseColor;
                #if defined(_VERTEXCOLOR)
                albedo.rgb *= i.color.rgb;
                #endif
                #if defined(_ALPHATEST_ON)
                clip(albedo.a - _Cutoff);
                #endif
                #if defined(_ZD_ROOTED)
                // the lower third of the card in the colour of the ground under its root, a little darker: the tuft grows out of the wash
                albedo.rgb = lerp(GroundAlbedo(i.root.xy) * 0.86, albedo.rgb, smoothstep(0.0, 0.32, i.root.z));
                #endif

                half3 n = normalize(i.normalWS);
                #if defined(_SPRITELIT)
                n = half3(0, 1, 0); // a standing card is lit like the ground it stands on
                #endif
                half3 ambient = SampleSH(n);
                half3 shadowTint = _ShadowTint.rgb;

                Light main = GetMainLight(i.shadowCoord);
                half shadow = main.shadowAttenuation; // D-22b: keep the PCF softness (a smoothstep here made the soft shadows hard again)
                half band = Ramp(dot(n, main.direction) * 0.5h + 0.5h) * shadow;
                // Shadows keep the warm tint of the ambient light instead of going grey; the night ambient is blue, so are night shadows.
                half3 light = ambient * lerp(shadowTint, half3(1, 1, 1), band) + main.color * (band * main.distanceAttenuation);

                half3 fireLight = 0;
                #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                for (uint li = 0u; li < count; li++)
                {
                    Light l = GetAdditionalLight(li, i.positionWS);
                    half atten = l.distanceAttenuation * l.shadowAttenuation;
                    half ring = lerp(atten, Ramp(atten), 0.12h); // D-21: a soft glow, not a hard disc
                    half nd = dot(n, l.direction) * 0.5h + 0.5h;
                    #if defined(_SPRITELIT)
                    nd = 1.0h; // a card faces the fire from any side
                    #endif
                    light += l.color * (Ramp(nd) * ring);
                    fireLight += l.color * atten;
                }
                #endif

                half3 c = albedo.rgb * light;
                #if defined(_SPRITELIT) && defined(_ADDITIONAL_LIGHTS)
                // D-22b: a warm rim on the silhouette from a fire or a torch nearby — where this pixel is solid and a neighbour 3 texels away is not
                if (_Rim > 0.5h && dot(fireLight, fireLight) > 1e-4)
                {
                    float2 o3 = _BaseMap_TexelSize.xy * 3.0;
                    half aN = min(min(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv + float2(o3.x, 0)).a, SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv - float2(o3.x, 0)).a),
                                  min(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv + float2(0, o3.y)).a, SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv - float2(0, o3.y)).a));
                    half edge = saturate((albedo.a - aN) * 2.0h);
                    c += fireLight * edge * 1.4h;
                }
                #endif
                #if !defined(_SPRITELIT)
                {
                    // D-21 rain: wet things are darker and a little more saturated; a faint sheen on what faces up (a cool sky glint).
                    half wet = _ZD_Wetness;
                    half g0 = dot(c, half3(0.3h, 0.59h, 0.11h));
                    c = lerp(c, lerp(g0.xxx, c, 1.18h) * 0.62h, wet);
                    half up = saturate(n.y);
                    c += half3(0.10h, 0.13h, 0.18h) * (wet * up * up * 0.5h);
                }
                #endif
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
