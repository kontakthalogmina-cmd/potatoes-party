// Potatoes Party – Shader Bohatera Ziemniaka v2
// Przebieg UniversalForwardOnly: model oświetlenia "żywej bulwy".
//   diffuse   = Spherical Gaussian SSS per kanał (Neubelt & Pettineo / Hill: dopasowana irradiancja SG),
//               szerokość dyfuzji z krzywizny powierzchni + kolorowy półcień cienia
//   transmisja= Barré-Brisebois pod światło, tłumienie Beera-Lamberta per kanał (niebieski ginie pierwszy)
//   specular  = dwa płaty GGX jak w skórze (0.85 × r, 0.15 × r/2) + Charlie sheen (aksamitny meszek)
//   woda      = warstwa clear-coat (GGX r≈0.06, F0 0.02) nad przyciemnioną, "nasiąkniętą" bazą
//   rim       = gameplayowy Fresnel, niezależny od cieni – postać zawsze odcina się od tła
#ifndef POTATO_HERO_FORWARD_PASS_INCLUDED
#define POTATO_HERO_FORWARD_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float2 uv         : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float3 normalWS   : TEXCOORD2;
    float3 charPos    : TEXCOORD3;
    float3 charNormal : TEXCOORD4;
    half   fogFactor  : TEXCOORD5;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings PotatoVert(Attributes input)
{
    Varyings o = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    float3 restWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 positionWS = PP_ApplyImpact(restWS);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    o.positionCS = TransformWorldToHClip(positionWS);
    o.positionWS = positionWS;
    o.normalWS = normalWS;
    o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    // warstwy liczone z pozycji SPOCZYNKOWEJ: plamy nie "pływają" po skórze przy wgnieceniu
    o.charPos = PotatoCharacterSpace(restWS, input.positionOS.xyz);
    o.charNormal = PotatoCharacterDir(normalWS, input.normalOS);
    o.fogFactor = ComputeFogFactor(o.positionCS.z);
    return o;
}

// ---------------------------------------------------------------- powierzchnia

struct PotatoBRDF
{
    half3 albedo;
    half3 flesh;
    half  smoothness;
    half  specular;
    half  scatter;
    half3 scatterRadius;   // zasięg dyfuzji per kanał (już przemnożony przez krzywiznę)
    half  thickness;
    half3 absorption;
    half3 sheen;
    half  occlusion;
    half3 emission;
    half  metallic;
    half  coat;            // film wody (0..1)
    half  coatSmoothness;
};

// ---------------------------------------------------------------- wzory skórek (proceduralne, przestrzeń postaci)

struct PotatoPattern
{
    half  mask;
    half3 glow;
    half  height;
    half  rough;
};

PotatoPattern PP_EvaluatePattern(float3 cp, half3 N, half3 V, float cpWidth)
{
    PotatoPattern o = (PotatoPattern)0;
    float sc = max(_PatternScale, 0.05);
    int type = (int)round(_PatternType);

    UNITY_BRANCH if (type == 1)
    {
        float wob = PP_Fbm(cp * 1.7 + 3.1);
        float v = cp.y * 5.5 * sc + wob * 1.4 + cp.x * 0.6;
        float tri = abs(frac(v) - 0.5) * 2.0;
        float w = clamp(cpWidth * 5.5 * sc * 2.0, 0.02, 0.4);
        o.mask = smoothstep(0.5 - w, 0.5 + w, tri);
    }
    else if (type == 2)
    {
        float3 q = cp * 2.4 * sc + (PP_Fbm(cp * 3.0) - 0.5) * 0.9;
        float3 w = PP_Worley(q);
        o.mask = (1.0 - smoothstep(0.30, 0.36, w.x)) * step(0.42, w.z);
        o.height = o.mask * 0.15;
    }
    else if (type == 3)
    {
        float n = PP_Fbm(cp * 1.6 * sc + 7.0);
        o.mask = smoothstep(0.38, 0.78, n);
        float3 g = cp * 22.0 * sc;
        float3 id = floor(g);
        float3 f = frac(g) - 0.5;
        float h = PP_Hash31(id);
        float d = length(f - (PP_Hash33(id) - 0.5) * 0.6);
        half star = step(0.92, h) * saturate(1.0 - d * 7.0);
        half twinkle = 0.45 + 0.55 * sin(_Time.y * (2.0 + h * 4.0) + h * 60.0);
        o.glow = _PatternGlow.rgb * star * twinkle + _PatternColor.rgb * o.mask * 0.12;
    }
    else if (type == 4)
    {
        float3 q = cp * 2.6 * sc + (PP_Fbm(cp * 2.0 + 5.0) - 0.5) * 0.5;
        float3 w = PP_Worley(q);
        half crack = 1.0 - smoothstep(0.015, 0.10, w.y - w.x);
        half pulse = 0.65 + 0.35 * sin(_Time.y * 2.2 + w.z * 6.28 + cp.y * 3.0);
        o.mask = crack;
        o.glow = _PatternGlow.rgb * crack * pulse;
        o.height = -crack * 0.5;
        o.rough = 0.35;
    }
    else if (type == 5)
    {
        float3 g = cp * 46.0 * sc;
        float3 id = floor(g);
        float3 r = normalize(PP_Hash33(id) - 0.5);
        half on = step(0.45, PP_Hash31(id + 3.3));
        half spark = pow(saturate(dot(V, normalize(N + r * 0.85))), 48.0) * on;
        o.glow = _PatternColor.rgb * spark * 3.0;
    }
    return o;
}

// ---------------------------------------------------------------- BRDF

half PP_D_GGX(half NdotH, half a2)
{
    half d = (NdotH * a2 - NdotH) * NdotH + 1.0h;
    return a2 / (PI * d * d + 1e-4h);
}

half PP_V_SmithGGXCorrelatedApprox(half NdotV, half NdotL, half a)
{
    half v = NdotL * (NdotV * (1.0h - a) + a);
    half l = NdotV * (NdotL * (1.0h - a) + a);
    return 0.5h / (v + l + 1e-4h);
}

// Estevez & Kulla 2017
half PP_D_Charlie(half NdotH, half roughness)
{
    half invR = 1.0h / max(roughness, 0.07h);
    half sin2 = max(1.0h - NdotH * NdotH, 0.0078125h);
    return (2.0h + invR) * pow(sin2, invR * 0.5h) / (2.0h * PI);
}

half PP_V_Neubelt(half NdotV, half NdotL)
{
    return saturate(1.0h / (4.0h * (NdotL + NdotV - NdotL * NdotV) + 1e-4h));
}

// Irradiancja kosinusowego płata splecionego z gaussem sferycznym o ostrości lambda (dopasowanie S. Hilla,
// "SG Series", MJP 2016). Duża lambda → zwykły Lambert, mała → szerokie "owinięcie" jak w skórze.
// Normalizujemy do wartości przy N·L = 1, żeby rozpraszanie nie przyciemniało oświetlonej strony.
float PP_SGIrradiance(float NdotL, float lambda)
{
    const float c0 = 0.36;
    const float c1 = 1.0 / (4.0 * c0);
    float eml = exp(-lambda);
    float em2l = eml * eml;
    float rl = rcp(lambda);
    float scale = 1.0 + 2.0 * em2l - rl;
    float bias = (eml - em2l) * rl - em2l;
    float x = sqrt(max(1.0 - scale, 1e-6));
    float x1 = c1 * x;

    float x0 = c0 * NdotL;
    float n = x0 + x1;
    float y = (abs(x0) <= x1) ? n * n / x : saturate(NdotL);

    float n1 = c0 + x1;
    float y1 = (c0 <= x1) ? n1 * n1 / x : 1.0;
    return max(scale * y + bias, 0.0) / max(scale * y1 + bias, 1e-4);
}

float3 PP_SGDiffuse(float NdotL, float3 radius)
{
    // lambda = 1 / zasięg; ograniczenie [0.6, 48]: od "plasteliny" do zwykłego Lamberta
    float3 lambda = clamp(rcp(max(radius, 1e-3)), 0.6, 48.0);
    return float3(PP_SGIrradiance(NdotL, lambda.r), PP_SGIrradiance(NdotL, lambda.g), PP_SGIrradiance(NdotL, lambda.b));
}

half3 PP_DirectLight(Light light, PotatoBRDF s, half3 N, half3 V)
{
    half3 L = light.direction;
    half NdotLraw = dot(N, L);
    half NdotL = saturate(NdotLraw);
    half NdotV = saturate(dot(N, V)) + 1e-4h;
    half3 H = SafeNormalize(L + V);
    half NdotH = saturate(dot(N, H));
    half LdotH = saturate(dot(L, H));
    half shadow = light.shadowAttenuation;
    half atten = light.distanceAttenuation;

    // --- diffuse z SSS: SG per kanał + stylizowane owinięcie terminatora
    half wrapL = saturate((NdotLraw + _WrapDiffuse) / ((1.0h + _WrapDiffuse) * (1.0h + _WrapDiffuse)));
    half3 sss = (half3)PP_SGDiffuse(NdotLraw, s.scatterRadius);
    half3 diffTerm = lerp(wrapL.xxx, sss, s.scatter);
    // kolorowy półcień (Jimenez): cień na skórze ma czerwonawą krawędź, bo światło rozprasza się pod nią
    half3 shadowRGB = pow(max(shadow.xxx, 1e-3h), 1.0h / (1.0h + s.scatter * half3(1.4h, 0.45h, 0.2h)));
    half3 diffuse = s.albedo * diffTerm * shadowRGB * (1.0h - s.metallic);

    // --- transmisja pod światło: Beer-Lambert per kanał przez lokalną grubość
    half3 Ht = normalize(L + N * _TransDistortion);
    half trans = pow(saturate(dot(V, -Ht)), _TransPower) * _TransScale;
    half3 beer = exp(-s.absorption * (0.25h + s.thickness * 2.5h));
    half3 transmission = s.flesh * beer * trans * s.scatter * lerp(1.0h, shadow, 0.6h);

    // --- specular: dwa płaty GGX (skóra: ostry połysk sebum + szeroki "aksamitny" blask)
    half perceptualRough = 1.0h - s.smoothness;
    half a = max(perceptualRough * perceptualRough, 0.002h);
    half a2 = max(a * 0.5h, 0.002h);
    half F0 = lerp(0.028h, 0.9h, s.metallic);                 // skóra/miąższ ~ 0.028 (n ≈ 1.4)
    half F = F0 + (1.0h - F0) * pow(1.0h - LdotH, 5.0h);
    half spec = (0.85h * PP_D_GGX(NdotH, a * a) * PP_V_SmithGGXCorrelatedApprox(NdotV, NdotL, a)
               + 0.15h * PP_D_GGX(NdotH, a2 * a2) * PP_V_SmithGGXCorrelatedApprox(NdotV, NdotL, a2)) * F * NdotL;
    half shaped = smoothstep(0.30h, 0.42h, spec / (spec + 1.0h)) * (1.0h + s.smoothness * 2.0h);
    spec = lerp(spec, shaped, _SpecularBand) * s.specular * shadow;
    half3 specColor = spec * lerp(half3(1.0h, 1.0h, 1.0h), s.albedo * 1.6h, s.metallic);

    // --- sheen: aksamitny meszek (woda go "skleja")
    half3 sheen = s.sheen * PP_D_Charlie(NdotH, _SheenRoughness) * PP_V_Neubelt(NdotV, NdotL) * wrapL * shadow;

    // --- clear-coat wody: osobny, bardzo gładki płat; to co odbije, nie dociera do bazy
    half3 coatSpec = 0;
    half baseAtten = 1.0h;
    UNITY_BRANCH if (s.coat > 0.001h)
    {
        half cr = 1.0h - s.coatSmoothness;
        half ca = max(cr * cr, 0.002h);
        half Fc = 0.02h + 0.98h * pow(1.0h - LdotH, 5.0h);
        coatSpec = (PP_D_GGX(NdotH, ca * ca) * PP_V_SmithGGXCorrelatedApprox(NdotV, NdotL, ca) * Fc * NdotL * s.coat * shadow).xxx;
        baseAtten = 1.0h - Fc * s.coat;
    }

    return ((diffuse + transmission + specColor + sheen) * baseAtten + coatSpec) * light.color * atten;
}

// ---------------------------------------------------------------- fragment

half4 PotatoFrag(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    float3 positionWS = input.positionWS;
    half3 Ngeo = normalize(input.normalWS);
    half3 V = GetWorldSpaceNormalizeViewDir(positionWS);
    float3 cp = input.charPos;
    float3 cn = normalize(input.charNormal);

    PotatoSkinDetail skin = PP_SkinDetail(cp);
    PotatoLayers m = PP_EvaluateLayers(cp, cn, skin);
    half height = skin.height + m.height;

    float cpWidth = length(fwidth(cp));
    PotatoPattern pat = (PotatoPattern)0;
    UNITY_BRANCH if (_PatternType > 0.5)
    {
        pat = PP_EvaluatePattern(cp, Ngeo, V, cpWidth);
        height += pat.height;
    }

    // woda i błoto wygładzają relief (film wypełnia pory), kurz go nie zmienia
    half bump = _BumpStrength * (1.0h - m.wet * 0.5h) * (1.0h - m.mud * 0.3h) * (1.0h + m.cut * 0.8h);
    half3 N = PP_PerturbNormal(Ngeo, positionWS, height, bump);

    // krzywizna z pochodnych ekranowych: |Δn| / |Δp| ≈ 1/promień (poza gałęziami)
    half curvature = saturate(length(fwidth(Ngeo)) / max(length(fwidth(positionWS)), 1e-4) * _CurvatureScale);

    half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
    PotatoBRDF s;
    s.albedo = baseTex.rgb * _BaseColor.rgb;
    s.albedo *= 1.0h - (skin.eyes * 0.35h + skin.freckles * 0.12h);
    s.albedo = lerp(s.albedo, s.albedo * 1.25h + 0.04h, skin.lenticels);          // przetchlinki jaśniejsze
    s.albedo = lerp(s.albedo, s.albedo * 0.72h, skin.net);                        // korek ciemniejszy
    s.flesh = _FleshColor.rgb;
    s.smoothness = _Smoothness * (1.0h - skin.eyes * 0.5h) * (1.0h - skin.net * 0.6h);
    s.specular = _SpecularStrength;
    s.scatter = _ScatterStrength;
    // zasięg dyfuzji: cienkie, mocno zakrzywione bryły (rączki, stopy) rozpraszają szerzej
    s.scatterRadius = _ScatterRadius.rgb * _ScatterRadius.a * (0.35h + 1.65h * curvature);
    s.thickness = _Thickness;
    s.absorption = _Absorption.rgb;
    s.sheen = _SheenColor.rgb;
    s.emission = 0;
    s.metallic = _Metallic;
    s.coat = 0;
    s.coatSmoothness = 0.94h;

    s.albedo = lerp(s.albedo, _PatternColor.rgb * (1.0h - skin.eyes * 0.3h), pat.mask);
    s.smoothness = saturate(s.smoothness - pat.rough * pat.mask);
    s.emission += pat.glow;
    s.occlusion = lerp(1.0h - _GroundOcclusion, 1.0h, saturate((half)(cp.y + 0.75) / 0.9h));
    s.occlusion *= 1.0h - skin.eyes * 0.3h;

    // ---------- siniak (pod skórką): skórka ciemnieje, miąższ pod spodem robi się krwisty
    UNITY_BRANCH if (_BruiseAmount > 0.001)
    {
        half3 bruiseCol = lerp(_BruiseColor.rgb, half3(0.55h, 0.5h, 0.2h), saturate(1.0h - m.bruise * 1.8h) * 0.5h);
        s.albedo = lerp(s.albedo, s.albedo * bruiseCol * 1.6h, m.bruise);
        s.smoothness = lerp(s.smoothness, s.smoothness * 0.6h, m.bruise);
        s.sheen *= 1.0h - m.bruise * 0.6h;
        s.flesh = lerp(s.flesh, half3(0.85h, 0.18h, 0.22h), m.bruise * 0.7h);
    }

    // ---------- rozcięcie: odsłonięty miąższ = jasny, wilgotny, skrobiowy; mocno prześwituje
    UNITY_BRANCH if (_CutAmount > 0.001)
    {
        half3 exposed = lerp(_FleshColor.rgb, half3(1.0h, 0.93h, 0.72h), 0.45h);
        half3 oxidized = s.albedo * half3(0.55h, 0.42h, 0.34h);                  // brzeg rany brązowieje
        s.albedo = lerp(s.albedo, oxidized, m.cutRim);
        s.albedo = lerp(s.albedo, exposed, m.cut);
        s.smoothness = lerp(s.smoothness, 0.72h, m.cut);
        s.scatter = lerp(s.scatter, 1.0h, m.cut);
        s.thickness = lerp(s.thickness, 0.15h, m.cut);
        s.sheen *= 1.0h - saturate(m.cut + m.cutRim);
        s.metallic *= 1.0h - m.cut;
        s.emission *= 1.0h - m.cut;
        s.occlusion *= 1.0h - m.cut * 0.25h;
    }

    // ---------- zwęglenie → kurz → błoto → woda → szron (szron przykrywa wszystko)
    UNITY_BRANCH if (_BurnAmount > 0.001)
    {
        half3 soot = lerp(half3(0.05h, 0.035h, 0.03h), half3(0.20h, 0.13h, 0.09h), skin.micro);
        s.albedo = lerp(s.albedo, soot, m.burn);
        s.emission *= 1.0h - m.burn;
        s.metallic *= 1.0h - m.burn;
        s.smoothness = lerp(s.smoothness, 0.12h, m.burn);
        s.scatter *= 1.0h - m.burn * 0.85h;
        s.sheen *= 1.0h - m.burn;
        s.specular *= 1.0h - m.burn * 0.7h;
        half flicker = 0.75h + 0.25h * (half)PP_ValueNoise(float3(cp.xy * 4.0, _Time.y * 6.0));
        s.emission += _EmberColor.rgb * m.ember * flicker;
    }
    UNITY_BRANCH if (_DustAmount > 0.001)
    {
        // pył: jaśniejszy, matowy, gasi SSS i metal; pod kątem "świeci" (drobne ziarna rozpraszają wstecznie)
        s.albedo = lerp(s.albedo, _DustColor.rgb * lerp(0.9h, 1.05h, skin.micro), m.dust * 0.85h);
        s.smoothness = lerp(s.smoothness, 0.08h, m.dust);
        s.scatter *= 1.0h - m.dust * 0.7h;
        s.metallic *= 1.0h - m.dust;
        s.sheen = lerp(s.sheen, _DustColor.rgb * 0.5h, m.dust);
        s.emission *= 1.0h - m.dust * 0.8h;
    }
    UNITY_BRANCH if (_MudAmount > 0.001)
    {
        half3 mud = _MudColor.rgb * lerp(0.75h, 1.1h, skin.micro);
        s.albedo = lerp(s.albedo, mud, m.mud);
        s.albedo *= 1.0h - m.mudWet * 0.25h;
        s.smoothness = lerp(s.smoothness, 0.25h, m.mud);
        s.smoothness = lerp(s.smoothness, 0.88h, m.mudWet);
        s.scatter *= 1.0h - m.mud;
        s.sheen *= 1.0h - m.mud;
        s.emission *= 1.0h - m.mud;
        s.metallic *= 1.0h - m.mud;
        s.coat = max(s.coat, m.mudWet * 0.6h);
    }
    UNITY_BRANCH if (_WetAmount > 0.001)
    {
        // Lagarde 2013 ("Water drop 2b"): porowata powierzchnia nasiąka – albedo ciemnieje (≈ albedo²),
        // baza robi się gładsza, a na wierzchu leży film wody (clear-coat). Woda zmywa kurz.
        half w = saturate(m.wet + m.drop);
        half porosity = 0.7h * (1.0h - s.metallic);
        s.albedo = lerp(s.albedo, s.albedo * s.albedo, w * porosity);
        s.smoothness = lerp(s.smoothness, max(s.smoothness, 0.6h), w);
        s.sheen *= 1.0h - w * 0.85h;
        s.coat = max(s.coat, w);
        s.coatSmoothness = lerp(0.9h, 0.97h, m.drop);
    }
    UNITY_BRANCH if (_FrostAmount > 0.001)
    {
        s.albedo = lerp(s.albedo, _FrostColor.rgb, m.frost * 0.75h);
        s.albedo = lerp(s.albedo, _FrostColor.rgb * 1.15h, m.frostThick);
        s.smoothness = lerp(s.smoothness, 0.55h, m.frost);
        s.smoothness = lerp(s.smoothness, 0.93h, m.frostThick);
        s.flesh = lerp(s.flesh, half3(0.45h, 0.80h, 1.0h), m.frost);
        s.scatter = lerp(s.scatter, 0.9h, m.frostThick);
        s.thickness = lerp(s.thickness, 0.2h, m.frostThick);
        s.absorption = lerp(s.absorption, half3(3.0h, 1.2h, 0.6h), m.frost); // lód: ginie czerwień
        s.sheen *= 1.0h - m.frost;
        s.emission *= 1.0h - m.frostThick * 0.7h;
        s.metallic *= 1.0h - m.frost;
        s.specular = lerp(s.specular, 1.4h, m.frost);
        s.coat *= 1.0h - m.frost;
        half glint = step(1.0h - 0.02h * m.frost, (half)PP_Hash31(floor(cp * 80.0) + floor(_Time.y * 4.0)));
        s.emission += _FrostColor.rgb * glint * 3.0h;
    }

    // ---------- oświetlenie
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalWS = N;
    inputData.viewDirectionWS = V;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    inputData.shadowCoord = TransformWorldToShadowCoord(positionWS);
    half4 shadowMask = half4(1, 1, 1, 1);

    half directAO = 1.0h, indirectAO = s.occlusion;
    #if defined(_SCREEN_SPACE_OCCLUSION)
        AmbientOcclusionFactor ssao = GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
        directAO = ssao.directAmbientOcclusion;
        indirectAO *= ssao.indirectAmbientOcclusion;
    #endif

    Light mainLight = GetMainLight(inputData.shadowCoord, positionWS, shadowMask);
    half3 color = PP_DirectLight(mainLight, s, N, V) * directAO;

    #if defined(_ADDITIONAL_LIGHTS) || USE_CLUSTER_LIGHT_LOOP || USE_FORWARD_PLUS
        uint pixelLightCount = GetAdditionalLightsCount();
        #if USE_CLUSTER_LIGHT_LOOP || USE_FORWARD_PLUS
            [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
            {
                Light dl = GetAdditionalLight(lightIndex, positionWS, shadowMask);
                color += PP_DirectLight(dl, s, N, V);
            }
        #endif
        LIGHT_LOOP_BEGIN(pixelLightCount)
            Light al = GetAdditionalLight(lightIndex, positionWS, shadowMask);
            color += PP_DirectLight(al, s, N, V);
        LIGHT_LOOP_END
    #endif

    // ---------- światło pośrednie: SH + "światło zza postaci" (SSS z tyłu) + odbicia bazy i filmu wody
    half NdotV = saturate(dot(N, V));
    half fresnel = pow(1.0h - NdotV, 5.0h);
    half3 sh = SampleSH(N);
    half3 shBack = SampleSH(-N);
    half coatF = (0.02h + 0.98h * fresnel) * s.coat;
    half baseIndirect = 1.0h - coatF;
    color += s.albedo * sh * indirectAO * (1.0h - s.metallic * 0.8h) * baseIndirect;
    color += s.flesh * exp(-s.absorption * (0.25h + s.thickness * 2.5h)) * shBack * s.scatter * 0.35h * indirectAO;
    color += s.sheen * sh * pow(1.0h - NdotV, 3.0h) * 0.6h * indirectAO;

    half3 R = reflect(-V, N);
    half3 env = GlossyEnvironmentReflection(R, 1.0h - s.smoothness, indirectAO);
    half envF = 0.028h + (max(s.smoothness, 0.04h) - 0.028h) * fresnel;
    half3 envTint = lerp(half3(envF, envF, envF), s.albedo * (0.75h + 0.25h * fresnel), s.metallic);
    color += env * envTint * s.specular * baseIndirect;
    UNITY_BRANCH if (s.coat > 0.001h)
    {
        // film wody odbija prawie lustrzanie (geometria bez reliefu porów → normalna mniej zaburzona)
        half3 Nc = normalize(lerp(N, Ngeo, 0.6h));
        half3 envCoat = GlossyEnvironmentReflection(reflect(-V, Nc), 1.0h - s.coatSmoothness, indirectAO);
        color += envCoat * coatF;
    }

    // ---------- rim gameplayowy
    half rim = pow(1.0h - NdotV, _RimPower);
    half awayFromSun = saturate(0.5h - 0.5h * dot(N, mainLight.direction));
    rim *= lerp(1.0h, awayFromSun * 1.6h, _RimLightAlign);
    rim *= 0.55h + 0.45h * saturate(N.y + 0.5h);
    half3 rimTint = lerp(_RimColor.rgb, saturate(_PlayerColor.rgb * 1.6h + 0.15h), _RimPlayerTint);
    rimTint = lerp(rimTint, half3(0.55h, 0.85h, 1.0h), m.frost);
    rimTint *= 1.0h - m.mud * 0.6h;
    rimTint = lerp(rimTint, _DustColor.rgb * 1.2h, m.dust * 0.5h);               // kurz: miękki, ciepły kontur
    color += rimTint * rim * _RimStrength * s.occlusion;

    color += s.emission;
    color = lerp(color, half3(1.4h, 1.35h, 1.3h), _FlashAmount);
    color = MixFog(color, input.fogFactor);
    return half4(color, 1.0h);
}

#endif
