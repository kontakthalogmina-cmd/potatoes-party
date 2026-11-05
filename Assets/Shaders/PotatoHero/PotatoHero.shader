// Potatoes Party – Shader Bohatera Ziemniaka ("Aksamitna Bulwa")
// URP 17 (Unity 6), Forward / Forward+. Logika i strojenie: docs/ArtDirection.md, sekcja "Krok 3".
Shader "PotatoParty/PotatoHero"
{
    Properties
    {
        [MainColor] _BaseColor ("Kolor gracza (skórka)", Color) = (0.95, 0.75, 0.45, 1)
        [MainTexture] _BaseMap ("Tekstura (opcjonalna)", 2D) = "white" {}
        _FleshColor ("Kolor miąższu (SSS)", Color) = (1.0, 0.72, 0.38, 1)
        _Smoothness ("Gładkość", Range(0, 1)) = 0.55
        _SpecularStrength ("Siła odblasku", Range(0, 2)) = 0.8
        _SpecularBand ("Stylizacja odblasku (0 GGX, 1 twardy)", Range(0, 1)) = 0.35

        [Header(Skorka proceduralna)]
        _SpeckleAmount ("Oczka i piegi", Range(0, 1)) = 0.6
        _SpeckleScale ("Skala oczek", Float) = 3.2
        _BumpStrength ("Relief", Range(0, 0.1)) = 0.025
        _GroundOcclusion ("Przyciemnienie dołu", Range(0, 1)) = 0.35

        [Header(Miekka tkanka)]
        _WrapDiffuse ("Owinięcie światła (wrap)", Range(0, 1)) = 0.5
        _ScatterStrength ("Rozpraszanie (fake SSS)", Range(0, 1)) = 0.7
        _TransDistortion ("Translucencja: zniekształcenie", Range(0, 1)) = 0.35
        _TransPower ("Translucencja: wykładnik", Range(1, 16)) = 4
        _TransScale ("Translucencja: siła", Range(0, 4)) = 1.2
        _Thickness ("Grubość (1 = tułów)", Range(0, 1)) = 0.6

        [Header(Aksamit sheen)]
        _SheenColor ("Kolor meszku", Color) = (0.35, 0.30, 0.25, 1)
        _SheenRoughness ("Szorstkość meszku", Range(0.07, 1)) = 0.45

        [Header(Rim gameplayowy)]
        [HDR] _RimColor ("Kolor konturu", Color) = (1.0, 0.96, 0.88, 1)
        _RimPower ("Ostrość konturu", Range(0.5, 8)) = 3.5
        _RimStrength ("Siła konturu", Range(0, 3)) = 0.7
        _RimPlayerTint ("Kolor gracza w konturze", Range(0, 1)) = 0.45
        _RimLightAlign ("Kontur tylko pod słońce", Range(0, 1)) = 0.6

        [Header(Maski interaktywne MaterialPropertyBlock)]
        _MaskScale ("Skala plam", Float) = 1.0
        _MaskReceive ("Przyjmowanie masek", Range(0, 1)) = 1.0
        _MudAmount ("Błoto", Range(0, 1)) = 0
        _MudDir ("Błoto: kierunek (xyz) i siła (w)", Vector) = (0, -1, 0, 0)
        _MudColor ("Kolor błota", Color) = (0.27, 0.18, 0.11, 1)
        _FrostAmount ("Szron", Range(0, 1)) = 0
        _FrostColor ("Kolor szronu", Color) = (0.82, 0.93, 1.0, 1)
        _BurnAmount ("Zwęglenie", Range(0, 1)) = 0
        _BurnHeat ("Żar", Range(0, 1)) = 0
        _BurnDir ("Wybuch: kierunek (xyz) i siła (w)", Vector) = (0, 0, 1, 0)
        [HDR] _EmberColor ("Kolor żaru", Color) = (6.0, 1.6, 0.3, 1)
        _FlashAmount ("Błysk trafienia", Range(0, 1)) = 0

        [HideInInspector] _PP_Root0 ("", Vector) = (1, 0, 0, 0)
        [HideInInspector] _PP_Root1 ("", Vector) = (0, 1, 0, 0)
        [HideInInspector] _PP_Root2 ("", Vector) = (0, 0, 1, 0)
        [HideInInspector] _PP_UseRoot ("", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "UniversalMaterialType" = "Lit" }
        LOD 300

        HLSLINCLUDE
        #include "PotatoHeroInput.hlsl"
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            // ForwardOnly: renderuje się identycznie w Forward, Forward+ i Deferred (bez przebiegu GBuffer)
            Tags { "LightMode" = "UniversalForwardOnly" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex PotatoVert
            #pragma fragment PotatoFrag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "PotatoHeroCommon.hlsl"
            #include "PotatoHeroForwardPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct ShadowVaryings { float4 positionCS : SV_POSITION; };

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                ShadowVaryings o;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDir = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDir = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = positionCS;
                return o;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct DepthAttributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct DepthVaryings { float4 positionCS : SV_POSITION; };

            DepthVaryings DepthVert(DepthAttributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                DepthVaryings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }

            half DepthFrag(DepthVaryings input) : SV_Target { return input.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            // Potrzebny dla SSAO i ewentualnego konturu z normalnych (Renderer Feature)
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

            struct DNAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct DNVaryings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            DNVaryings DepthNormalsVert(DNAttributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                DNVaryings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            half4 DepthNormalsFrag(DNVaryings input) : SV_Target
            {
                float3 n = NormalizeNormalPerPixel(input.normalWS);
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

    FallBack "Universal Render Pipeline/Lit"
}
