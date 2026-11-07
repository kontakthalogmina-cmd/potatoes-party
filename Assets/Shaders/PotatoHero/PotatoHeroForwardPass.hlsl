// Potatoes Party – Shader Bohatera Ziemniaka ("Aksamitna Bulwa")
// Przebieg UniversalForwardOnly: własny model oświetlenia "miękkiej bulwy".
//   diffuse  = wrapped Lambert + pas rozpraszania w kolorze miąższu (fake SSS) + translucencja pod światło
//   specular = GGX (stylizowany kształt odblasku) + Charlie sheen ("meszek" aksamitu)
//   rim      = gameplayowy Fresnel, niezależny od cieni – postać zawsze odcina się od tła
//   maski    = błoto / szron / zwęglenie modyfikują albedo, gładkość, SSS, sheen, relief i emisję
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
    float3 charPos    : TEXCOORD3;   // pozycja w przestrzeni postaci (maski, plamki)
    float3 charNormal : TEXCOORD4;   // normalna w przestrzeni postaci
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

    VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs nrm = GetVertexNormalInputs(input.normalOS);
    o.positionCS = pos.positionCS;
    o.positionWS = pos.positionWS;
    o.normalWS = nrm.normalWS;
    o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    o.charPos = PotatoCharacterSpace(pos.positionWS, input.positionOS.xyz);
    o.charNormal = PotatoCharacterDir(nrm.normalWS, input.normalOS);
    o.fogFactor = ComputeFogFactor(pos.positionCS.z);
    return o;
}

// ---------------------------------------------------------------- BRDF

struct PotatoSurface
{
    half3 albedo;
    half3 flesh;          // kolor światła rozproszonego pod skórką
    half  smoothness;
    half  specular;
    half  scatter;        // siła fake SSS (błoto i zwęglenie ją gaszą, szron zmienia kolor)
    half  thickness;
    half3 sheen;
    half  occlusion;
    half3 emission;
    half  metallic;       // 0 = skórka, 1 = metal (złota bulwa): kolorowy odblask, brak rozproszenia
};

// ---------------------------------------------------------------- wzory skórek
// Proceduralne, w przestrzeni postaci: wzór "przykleja się" do ziemniaka i wspólnie obejmuje
// tułów, rączki i stopy, bez UV i bez tekstur.

struct PotatoPattern
{
    half  mask;     // 0..1: gdzie kładziemy _PatternColor
    half3 glow;     // emisja (gwiazdy, magma, brokat)
    half  height;   // wkład do reliefu (pęknięcia magmy są wklęsłe)
    half  rough;    // dodatkowa szorstkość
};

PotatoPattern PP_EvaluatePattern(float3 cp, half3 N, half3 V, float cpWidth)
{
    PotatoPattern o = (PotatoPattern)0;
    float sc = max(_PatternScale, 0.05);
    int type = (int)round(_PatternType);

    UNITY_BRANCH if (type == 1)
    {
        // PASKI: miękko falujące, poziome pasy (ziemniak fioletowy "Vitelotte")
        float wob = PP_Fbm(cp * 1.7 + 3.1);
        float v = cp.y * 5.5 * sc + wob * 1.4 + cp.x * 0.6;
        float tri = abs(frac(v) - 0.5) * 2.0;
        // szerokość piksela liczona poza gałęzią (pochodne w dynamicznym if są zabronione na części API)
        float w = clamp(cpWidth * 5.5 * sc * 2.0, 0.02, 0.4);
        o.mask = smoothstep(0.5 - w, 0.5 + w, tri);
    }
    else if (type == 2)
    {
        // ŁATY: nieregularne plamy (krówka)
        float3 q = cp * 2.4 * sc + (PP_Fbm(cp * 3.0) - 0.5) * 0.9;
        float3 w = PP_Worley(q);
        o.mask = (1.0 - smoothstep(0.30, 0.36, w.x)) * step(0.42, w.z);
        o.height = o.mask * 0.15;
    }
    else if (type == 3)
    {
        // KOSMOS: mgławica + migoczące gwiazdy
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
        // MAGMA: ciemna, zastygła skorupa z pulsującymi, świecącymi pęknięciami
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
        // BROKAT: drobne płatki metalu, każdy z własną normalną – błyskają pod różnymi kątami
        float3 g = cp * 46.0 * sc;
        float3 id = floor(g);
        float3 r = normalize(PP_Hash33(id) - 0.5);
        half on = step(0.45, PP_Hash31(id + 3.3));
        half spark = pow(saturate(dot(V, normalize(N + r * 0.85))), 48.0) * on;
        o.glow = _PatternColor.rgb * spark * 3.0;
        o.mask = 0;
    }
    return o;
}

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

// Estevez & Kulla 2017 ("Production Friendly Microfacet Sheen BRDF")
half PP_D_Charlie(half NdotH, half roughness)
{
    half invR = 1.0h / max(roughness, 0.07h);
    half sin2 = max(1.0h - NdotH * NdotH, 0.0078125h);
    return (2.0h + invR) * pow(sin2, invR * 0.5h) / (2.0h * PI);
}

// Neubelt & Pettineo 2013 – tania widoczność dla sheen
half PP_V_Neubelt(half NdotV, half NdotL)
{
    return saturate(1.0h / (4.0h * (NdotL + NdotV - NdotL * NdotV) + 1e-4h));
}

half3 PP_DirectLight(Light light, PotatoSurface s, half3 N, half3 V)
{
    half3 L = light.direction;
    half NdotLraw = dot(N, L);
    half NdotL = saturate(NdotLraw);
    half NdotV = saturate(dot(N, V)) + 1e-4h;
    half3 H = SafeNormalize(L + V);
    half NdotH = saturate(dot(N, H));
    half LdotH = saturate(dot(L, H));

    half shadow = light.shadowAttenuation;
    half dist = light.distanceAttenuation;

    // --- diffuse: wrapped Lambert (energetycznie normalizowany) + pas rozpraszania przy terminatorze
    half w = _WrapDiffuse;
    half wrapped = saturate((NdotLraw + w) / ((1.0h + w) * (1.0h + w)));
    half sssBand = saturate(wrapped - NdotL * 0.85h);
    // światło "przecieka" w cień na krawędzi: rozpraszanie widzi bardziej miękki cień niż powierzchnia
    half sssShadow = lerp(shadow, 1.0h, 0.35h * s.scatter);
    half3 diffuse = s.albedo * lerp(NdotL, wrapped, s.scatter * 0.5h) * shadow * (1.0h - s.metallic)
                  + s.flesh * sssBand * s.scatter * 1.6h * sssShadow;

    // --- translucencja pod światło (Barré-Brisebois & Bouchard, GDC 2011)
    half3 Ht = normalize(L + N * _TransDistortion);
    half trans = pow(saturate(dot(V, -Ht)), _TransPower) * _TransScale;
    half3 transmission = s.flesh * trans * (1.0h - s.thickness) * s.scatter * lerp(1.0h, shadow, 0.5h);

    // --- specular: GGX, opcjonalnie "podany" w stylizowany, twardy kształt
    half perceptualRough = 1.0h - s.smoothness;
    half a = max(perceptualRough * perceptualRough, 0.002h);
    half D = PP_D_GGX(NdotH, a * a);
    half Vis = PP_V_SmithGGXCorrelatedApprox(NdotV, NdotL, a);
    half F0 = lerp(0.04h, 0.9h, s.metallic);
    half F = F0 + (1.0h - F0) * pow(1.0h - LdotH, 5.0h);
    half spec = D * Vis * F * NdotL;
    half shaped = smoothstep(0.30h, 0.42h, spec / (spec + 1.0h)) * (1.0h + s.smoothness * 2.0h);
    spec = lerp(spec, shaped, _SpecularBand) * s.specular * shadow;
    half3 specColor = spec * lerp(half3(1.0h, 1.0h, 1.0h), s.albedo * 1.6h, s.metallic);   // metal barwi odblask

    // --- sheen: aksamitny meszek na krawędziach (fizycznie umotywowany rim w świetle)
    half3 sheen = s.sheen * PP_D_Charlie(NdotH, _SheenRoughness) * PP_V_Neubelt(NdotV, NdotL) * wrapped * sssShadow;

    return (diffuse + transmission + specColor + sheen) * light.color * dist;
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

    // ---------- skórka: "oczka" ziemniaka (wklęsłe dołki) + piegi + mikro-relief
    float3 sp = cp * _SpeckleScale;
    float3 cell = PP_Worley(sp);
    half eyes = (1.0h - smoothstep(0.08h, 0.26h, cell.x)) * step(0.62h, cell.z);   // rzadkie dołki
    half freckles = smoothstep(0.62h, 0.78h, PP_ValueNoise(sp * 3.1 + 5.0));
    half micro = PP_ValueNoise(sp * 9.0);
    half height = -eyes * 0.8h + micro * 0.12h;

    PotatoMasks m = PP_EvaluateMasks(cp, cn);
    height += m.height;

    float cpWidth = length(fwidth(cp));
    PotatoPattern pat = (PotatoPattern)0;
    UNITY_BRANCH if (_PatternType > 0.5)
    {
        pat = PP_EvaluatePattern(cp, Ngeo, V, cpWidth);
        height += pat.height;
    }

    half3 N = PP_PerturbNormal(Ngeo, positionWS, height, _BumpStrength);

    // ---------- powierzchnia bazowa
    half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
    PotatoSurface s;
    s.albedo = baseTex.rgb * _BaseColor.rgb;
    s.albedo *= 1.0h - (eyes * 0.35h + freckles * 0.12h) * _SpeckleAmount;
    s.flesh = _FleshColor.rgb;
    s.smoothness = _Smoothness * (1.0h - eyes * 0.5h);
    s.specular = _SpecularStrength;
    s.scatter = _ScatterStrength;
    s.thickness = _Thickness;
    s.sheen = _SheenColor.rgb;
    s.emission = 0;
    s.metallic = _Metallic;
    // wzór skórki: kolor, szorstkość, świecenie (pod maskami – błoto i szron go przykrywają)
    s.albedo = lerp(s.albedo, _PatternColor.rgb * (1.0h - eyes * 0.3h), pat.mask);
    s.smoothness = saturate(s.smoothness - pat.rough * pat.mask);
    s.emission += pat.glow;
    // kontakt z ziemią: dół postaci ciemnieje (stylizowany AO obiektu, działa też bez SSAO)
    s.occlusion = lerp(1.0h - _GroundOcclusion, 1.0h, saturate((cp.y + 0.75h) / 0.9h));
    s.occlusion *= 1.0h - eyes * 0.3h;

    // ---------- maski (kolejność: zwęglenie -> błoto -> szron, bo szron pokrywa wszystko)
    UNITY_BRANCH if (_BurnAmount > 0.001)
    {
        half3 soot = lerp(half3(0.05h, 0.035h, 0.03h), half3(0.20h, 0.13h, 0.09h), micro);
        s.albedo = lerp(s.albedo, soot, m.burn);
        s.emission *= 1.0h - m.burn;
        s.metallic *= 1.0h - m.burn;
        s.smoothness = lerp(s.smoothness, 0.12h, m.burn);
        s.scatter *= 1.0h - m.burn * 0.85h;
        s.sheen *= 1.0h - m.burn;
        s.specular *= 1.0h - m.burn * 0.7h;
        half flicker = 0.75h + 0.25h * PP_ValueNoise(float3(cp.xy * 4.0, _Time.y * 6.0));
        s.emission += _EmberColor.rgb * m.ember * flicker;
    }
    UNITY_BRANCH if (_MudAmount > 0.001)
    {
        half3 mud = _MudColor.rgb * lerp(0.75h, 1.1h, micro);
        s.albedo = lerp(s.albedo, mud, m.mud);
        s.albedo *= 1.0h - m.mudWet * 0.25h;                       // mokre = ciemniejsze
        s.smoothness = lerp(s.smoothness, 0.25h, m.mud);
        s.smoothness = lerp(s.smoothness, 0.88h, m.mudWet);        // błyszcząca, mokra krawędź
        s.scatter *= 1.0h - m.mud;
        s.sheen *= 1.0h - m.mud;
        s.emission *= 1.0h - m.mud;
        s.metallic *= 1.0h - m.mud;
    }
    UNITY_BRANCH if (_FrostAmount > 0.001)
    {
        s.albedo = lerp(s.albedo, _FrostColor.rgb, m.frost * 0.75h);
        s.albedo = lerp(s.albedo, _FrostColor.rgb * 1.15h, m.frostThick);
        s.smoothness = lerp(s.smoothness, 0.55h, m.frost);
        s.smoothness = lerp(s.smoothness, 0.93h, m.frostThick);
        s.flesh = lerp(s.flesh, half3(0.45h, 0.80h, 1.0h), m.frost); // światło pod lodem robi się zimne
        s.scatter = lerp(s.scatter, 0.9h, m.frostThick);
        s.thickness = lerp(s.thickness, 0.2h, m.frostThick);
        s.sheen *= 1.0h - m.frost;
        s.emission *= 1.0h - m.frostThick * 0.7h;
        s.metallic *= 1.0h - m.frost;
        s.specular = lerp(s.specular, 1.4h, m.frost);
        // migoczące drobinki lodu
        half glint = step(1.0h - 0.02h * m.frost, PP_Hash31(floor(cp * 80.0) + floor(_Time.y * 4.0)));
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

    // ---------- światło pośrednie (SH z ambient Trilight / sondy) + odbicia
    half NdotV = saturate(dot(N, V));
    half fresnel = pow(1.0h - NdotV, 5.0h);
    half3 sh = SampleSH(N);
    half3 shBack = SampleSH(-N);
    color += s.albedo * sh * indirectAO * (1.0h - s.metallic * 0.8h);
    color += s.flesh * shBack * s.scatter * (1.0h - s.thickness) * 0.25h * indirectAO; // światło zza postaci
    color += s.sheen * sh * pow(1.0h - NdotV, 3.0h) * 0.6h * indirectAO;

    half3 R = reflect(-V, N);
    half perceptualRough = 1.0h - s.smoothness;
    half3 env = GlossyEnvironmentReflection(R, perceptualRough, indirectAO);
    half envF = 0.04h + (max(s.smoothness, 0.04h) - 0.04h) * fresnel;
    half3 envTint = lerp(half3(envF, envF, envF), s.albedo * (0.75h + 0.25h * fresnel), s.metallic);
    color += env * envTint * s.specular;

    // ---------- rim gameplayowy: nie zależy od cieni ani pory dnia mapy, zawsze czyta sylwetkę
    half rim = pow(1.0h - NdotV, _RimPower);
    half awayFromSun = saturate(0.5h - 0.5h * dot(N, mainLight.direction));
    rim *= lerp(1.0h, awayFromSun * 1.6h, _RimLightAlign);
    rim *= 0.55h + 0.45h * saturate(N.y + 0.5h);                     // mocniej od nieba, słabiej przy ziemi
    half3 rimTint = lerp(_RimColor.rgb, saturate(_PlayerColor.rgb * 1.6h + 0.15h), _RimPlayerTint);
    rimTint = lerp(rimTint, half3(0.55h, 0.85h, 1.0h), m.frost);     // zamrożony = zimny kontur
    rimTint *= 1.0h - m.mud * 0.6h;                                  // błoto matowi kontur
    color += rimTint * rim * _RimStrength * s.occlusion;

    color += s.emission;

    // ---------- błysk trafienia
    color = lerp(color, half3(1.4h, 1.35h, 1.3h), _FlashAmount);

    color = MixFog(color, input.fogFactor);
    return half4(color, 1.0h);
}

#endif
