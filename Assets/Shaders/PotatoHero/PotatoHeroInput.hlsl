// Potatoes Party – Shader Bohatera Ziemniaka ("Aksamitna Bulwa")
// Wspólne wejścia dla wszystkich przebiegów. Jeden CBUFFER UnityPerMaterial = zgodność z SRP Batcherem.
// Maski ustawia PotatoSurface.cs na WŁASNYCH kopiach materiałów każdego ziemniaka (nie MaterialPropertyBlock),
// dzięki czemu wszystkie części ciała zostają w SRP Batcherze.
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

    // --- skórka gracza: wzór proceduralny, metal i kolor gracza w konturze
    half4  _PlayerColor;        // kolor gracza (rim), niezależny od koloru skórki
    half   _PatternType;        // 0 brak, 1 paski, 2 łaty, 3 kosmos, 4 magma, 5 brokat
    half4  _PatternColor;
    half4  _PatternGlow;        // HDR: świecenie wzoru (gwiazdy, pęknięcia magmy)
    half   _PatternScale;
    half   _Metallic;

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

    // --- siniaki: interaktywna maska zniszczeń po ciosach (PotatoSurface.Impact)
    half   _BruiseAmount;
    half4  _BruiseDir;          // xyz: strona, z której przyszedł cios (przestrzeń postaci), w: siła kierunku
    half4  _BruiseColor;

    // --- miękka tkanka: zależność rozpraszania od krzywizny (małe bryły = mocniej prześwitują)
    half   _CurvatureScale;

    // --- wgniecenie przy ciosie (wierzchołki, wszystkie przebiegi – cień i SSAO też się wginają)
    float4 _ImpactCenter;       // xyz: środek ciała (świat), w: promień ciała [m]
    float4 _ImpactDir;          // xyz: kierunek pchnięcia ciosu (świat), w: głębokość ze sprężyny (-0.6..1)
    half   _ImpactDentScale;

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

// Wgniecenie po ciosie, w przestrzeni świata: "czapa" wierzchołków po stronie ciosu cofa się w głąb ciała,
// pierścień wokół lekko się wybrzusza (zachowanie objętości). Ujemna głębokość (odbicie sprężyny) = wypchnięcie,
// czyli galaretowate "plum" po ciosie. Dalekie części (czubek, dłonie) prawie się nie ruszają.
float3 PP_ApplyImpact(float3 positionWS)
{
    float depth = _ImpactDir.w;
    if (abs(depth) < 0.001) return positionWS;
    float3 rel = positionWS - _ImpactCenter.xyz;
    float r = length(rel);
    float R = max(_ImpactCenter.w, 0.05);
    float3 radial = rel / max(r, 1e-4);
    float3 d = _ImpactDir.xyz;
    float facing = dot(radial, -d);
    float cap = smoothstep(0.2, 0.95, facing);
    float ring = smoothstep(-0.25, 0.3, facing) * (1.0 - cap);
    float reach = min(r, R) * saturate(1.6 - r / R);
    float k = depth * reach * _ImpactDentScale;
    positionWS += d * (k * cap);
    positionWS += radial * (k * ring * 0.35);
    return positionWS;
}

float3 PotatoCharacterDir(float3 dirWS, float3 dirOS)
{
    float3 rootDir = float3(dot(_PP_Root0.xyz, dirWS), dot(_PP_Root1.xyz, dirWS), dot(_PP_Root2.xyz, dirWS));
    return normalize(lerp(dirOS, rootDir, _PP_UseRoot) + 1e-5);
}

#endif
