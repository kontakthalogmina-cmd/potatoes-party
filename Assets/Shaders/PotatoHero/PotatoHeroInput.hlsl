// Potatoes Party – Shader Bohatera Ziemniaka v2 ("Aksamitna Bulwa", rework 2026-11)
// Wspólne wejścia dla wszystkich przebiegów. Jeden CBUFFER UnityPerMaterial = zgodność z SRP Batcherem.
// Maski ustawia PotatoSurface.cs na WŁASNYCH kopiach materiałów każdego ziemniaka (nie MaterialPropertyBlock),
// dzięki czemu wszystkie części ciała zostają w SRP Batcherze.
#ifndef POTATO_HERO_INPUT_INCLUDED
#define POTATO_HERO_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4  _BaseColor;          // kolor skórki
    half4  _FleshColor;         // kolor miąższu: światło rozproszone pod skórką (SSS) i odsłonięte rany
    half   _Smoothness;
    half   _SpecularStrength;
    half   _SpecularBand;       // 0 = fizyczny GGX (dwa płaty), 1 = stylizowany, twardy kształt odblasku

    // --- skórka proceduralna (fotorealistyczne detale bez tekstur)
    half   _SpeckleAmount;      // "oczka" (wklęsłe pąki) i piegi
    half   _SpeckleScale;
    half   _LenticelAmount;     // przetchlinki: drobne jasne kreseczki typowe dla skórki ziemniaka
    half   _NetAmount;          // siateczka korkowa (odmiany "russet"): spękany, matowy wzór
    half   _BumpStrength;
    half   _GroundOcclusion;    // przyciemnienie dołu postaci (kontakt z ziemią)

    // --- skórka gracza: wzór proceduralny, metal i kolor gracza w konturze
    half4  _PlayerColor;
    half   _PatternType;        // 0 brak, 1 paski, 2 łaty, 3 kosmos, 4 magma, 5 brokat
    half4  _PatternColor;
    half4  _PatternGlow;
    half   _PatternScale;
    half   _Metallic;

    // --- podpowierzchniowe rozpraszanie (SSS)
    half   _ScatterStrength;    // 0..1: ile światła wchodzi pod skórkę
    half4  _ScatterRadius;      // rgb: zasięg dyfuzji per kanał (czerwień najdalej), a: skala globalna
    half   _CurvatureScale;     // krzywizna → szerokość dyfuzji
    half   _WrapDiffuse;        // dodatkowe owinięcie terminatora (stylizacja)
    half   _TransDistortion;    // translucencja: zgięcie wektora światła przez normalną
    half   _TransPower;
    half   _TransScale;
    half   _Thickness;          // 0 = cienko (rączki), 1 = grubo (tułów)
    half4  _Absorption;         // rgb: współczynnik pochłaniania Beera-Lamberta (niebieski ginie pierwszy)

    // --- aksamit (sheen, "meszek")
    half4  _SheenColor;
    half   _SheenRoughness;

    // --- czytelny kontur światła (rim gameplayowy)
    half4  _RimColor;
    half   _RimPower;
    half   _RimStrength;
    half   _RimPlayerTint;
    half   _RimLightAlign;

    // --- warstwy środowiskowe (PotatoSurface.cs)
    half   _MaskScale;
    half   _MaskReceive;        // 0..1: części, które słabiej łapią maski (twarz, rękawiczki)
    half   _MudAmount;          // błoto: mokre, ciężkie, ciemne
    half4  _MudDir;             // xyz: kierunek rozbryzgu (przestrzeń postaci), w: siła kierunku
    half4  _MudColor;
    half   _DustAmount;         // kurz: suchy, jasny, pudrowy – narasta od stóp przy bieganiu
    half4  _DustColor;
    half   _WetAmount;          // woda: film na skórce, krople, zacieki; schnie od góry
    half   _WetDrops;           // gęstość kropel
    half   _FrostAmount;
    half4  _FrostColor;
    half   _BurnAmount;
    half   _BurnHeat;
    half4  _BurnDir;
    half4  _EmberColor;
    half   _FlashAmount;

    // --- uszkodzenia
    half   _BruiseAmount;       // siniaki po ciosach
    half4  _BruiseDir;
    half4  _BruiseColor;
    half   _CutAmount;          // rozcięcia: zdarta skórka, odsłonięty, wilgotny miąższ
    half4  _CutDir;             // xyz: strona rany (przestrzeń postaci), w: siła kierunku
    half4  _CutSeed;            // xyz: losowe przesunięcie wzoru rany, w: skala

    // --- wgniecenie przy ciosie (wierzchołki, wszystkie przebiegi – cień i SSAO też się wginają)
    float4 _ImpactCenter;       // xyz: środek ciała (świat), w: promień ciała [m]
    float4 _ImpactDir;          // xyz: kierunek pchnięcia ciosu (świat), w: głębokość ze sprężyny (-0.6..1)
    half   _ImpactDentScale;

    // --- przestrzeń postaci: wiersze macierzy worldToLocal korzenia (Visual)
    float4 _PP_Root0;
    float4 _PP_Root1;
    float4 _PP_Root2;
    half   _PP_UseRoot;
CBUFFER_END

// Pozycja w "przestrzeni postaci": w niej liczymy szum skórki i wszystkie warstwy.
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

// Wgniecenie po ciosie (świat): "czapa" wierzchołków po stronie ciosu cofa się w głąb ciała, pierścień wokół
// lekko się wybrzusza (zachowanie objętości). Ujemna głębokość (odbicie sprężyny) = galaretowate "plum".
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

#endif
