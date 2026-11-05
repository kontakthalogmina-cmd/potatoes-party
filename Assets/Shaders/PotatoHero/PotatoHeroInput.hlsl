// Potatoes Party – Shader Bohatera Ziemniaka ("Aksamitna Bulwa")
// Wspólne wejścia dla wszystkich przebiegów. Jeden CBUFFER UnityPerMaterial = zgodność z SRP Batcherem.
// Uwaga: renderery z MaterialPropertyBlock wypadają z SRP Batchera (rysowane osobno). Przy 2–6 graczach
// i ~10 częściach ciała to ok. 60 draw calli – świadomy koszt za maski sterowane z gameplayu.
#ifndef POTATO_HERO_INPUT_INCLUDED
#define POTATO_HERO_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4  _BaseColor;          // kolor gracza ("barwiona skórka")
    half4  _FleshColor;         // kolor miąższu widoczny w rozpraszaniu (SSS)
    half   _Smoothness;
    half   _SpecularStrength;
    half   _SpecularBand;       // 0 = czysty GGX, 1 = stylizowany, twardy kształt odblasku

    // --- skórka proceduralna (oczka ziemniaka, plamki, mikro-relief)
    half   _SpeckleAmount;
    half   _SpeckleScale;
    half   _BumpStrength;
    half   _GroundOcclusion;    // przyciemnienie dołu postaci (kontakt z ziemią)

    // --- miękka tkanka
    half   _WrapDiffuse;        // owinięcie terminatora (fake SSS)
    half   _ScatterStrength;
    half   _TransDistortion;    // translucencja pod światło
    half   _TransPower;
    half   _TransScale;
    half   _Thickness;          // 0 = cienko (rączki), 1 = grubo (tułów)

    // --- aksamit (sheen, "meszek")
    half4  _SheenColor;
    half   _SheenRoughness;

    // --- czytelny kontur światła (rim gameplayowy)
    half4  _RimColor;
    half   _RimPower;
    half   _RimStrength;
    half   _RimPlayerTint;      // ile koloru gracza w rimie
    half   _RimLightAlign;      // 0 = rim dookoła, 1 = tylko po stronie przeciwnej do słońca

    // --- maski interaktywne (zwykle ustawiane przez MaterialPropertyBlock z PotatoSurface.cs)
    half   _MudAmount;
    half4  _MudDir;             // xyz: kierunek, z którego przyleciało błoto (przestrzeń postaci), w: siła kierunku
    half4  _MudColor;
    half   _FrostAmount;
    half4  _FrostColor;
    half   _BurnAmount;         // zwęglenie (zostaje dłużej)
    half   _BurnHeat;           // żar na krawędzi (gaśnie szybko)
    half4  _BurnDir;            // xyz: kierunek do wybuchu (przestrzeń postaci), w: siła kierunku
    half4  _EmberColor;
    half   _FlashAmount;        // biały błysk po trafieniu
    half   _MaskScale;          // skala szumu masek (gęstość plam)
    half   _MaskReceive;        // 0..1: części, które nie łapią masek (np. rękawiczki = 0.5)

    // --- przestrzeń postaci: wiersze macierzy worldToLocal korzenia (Visual) – maski i plamki
    //     "przyklejają się" do całego ziemniaka zamiast pływać po każdej bryle osobno
    float4 _PP_Root0;
    float4 _PP_Root1;
    float4 _PP_Root2;
    half   _PP_UseRoot;         // 0 = przestrzeń obiektu (materiał bez skryptu), 1 = przestrzeń korzenia
CBUFFER_END

// Pozycja w "przestrzeni postaci": w niej liczymy szum skórki i wszystkie maski.
float3 PotatoCharacterSpace(float3 positionWS, float3 positionOS)
{
    float4 p = float4(positionWS, 1.0);
    float3 rootPos = float3(dot(_PP_Root0, p), dot(_PP_Root1, p), dot(_PP_Root2, p));
    return lerp(positionOS, rootPos, _PP_UseRoot);
}

float3 PotatoCharacterDir(float3 dirWS, float3 dirOS)
{
    float3 rootDir = float3(dot(_PP_Root0.xyz, dirWS), dot(_PP_Root1.xyz, dirWS), dot(_PP_Root2.xyz, dirWS));
    return normalize(lerp(dirOS, rootDir, _PP_UseRoot) + 1e-5);
}

#endif
