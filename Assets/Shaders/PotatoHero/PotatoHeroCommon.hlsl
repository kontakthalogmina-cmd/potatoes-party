// Potatoes Party – Shader Bohatera Ziemniaka ("Aksamitna Bulwa")
// Szum proceduralny, pole wysokości skórki i maski interaktywne (błoto / szron / zwęglenie).
// Wszystko liczone w przestrzeni postaci, bez tekstur: nasze ziemniaki są budowane z prymitywów w kodzie,
// więc nie mamy sensownego UV – proceduralność jest tu cechą, nie obejściem.
#ifndef POTATO_HERO_COMMON_INCLUDED
#define POTATO_HERO_COMMON_INCLUDED

// ---------------------------------------------------------------- szum

float PP_Hash31(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float3 PP_Hash33(float3 p)
{
    p = frac(p * float3(0.1031, 0.1030, 0.0973));
    p += dot(p, p.yxz + 33.33);
    return frac((p.xxy + p.yxx) * p.zyx);
}

float PP_ValueNoise(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float3 u = f * f * (3.0 - 2.0 * f);
    float n000 = PP_Hash31(i);
    float n100 = PP_Hash31(i + float3(1, 0, 0));
    float n010 = PP_Hash31(i + float3(0, 1, 0));
    float n110 = PP_Hash31(i + float3(1, 1, 0));
    float n001 = PP_Hash31(i + float3(0, 0, 1));
    float n101 = PP_Hash31(i + float3(1, 0, 1));
    float n011 = PP_Hash31(i + float3(0, 1, 1));
    float n111 = PP_Hash31(i + float3(1, 1, 1));
    float x00 = lerp(n000, n100, u.x), x10 = lerp(n010, n110, u.x);
    float x01 = lerp(n001, n101, u.x), x11 = lerp(n011, n111, u.x);
    return lerp(lerp(x00, x10, u.y), lerp(x01, x11, u.y), u.z);
}

// 3 oktawy z rotacją między nimi (brak artefaktów osiowych na kulistych bryłach)
float PP_Fbm(float3 p)
{
    const float3x3 rot = float3x3(0.00, 0.80, 0.60, -0.80, 0.36, -0.48, -0.60, -0.48, 0.64);
    float a = 0.5, s = 0.0;
    [unroll] for (int o = 0; o < 3; o++)
    {
        s += a * PP_ValueNoise(p);
        p = mul(rot, p) * 2.03;
        a *= 0.5;
    }
    return s / 0.875;
}

// Komórkowy szum (Worley) 3x3x3. x = F1, y = F2, z = hash najbliższej komórki.
float3 PP_Worley(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float f1 = 8.0, f2 = 8.0, id = 0.0;
    [unroll] for (int z = -1; z <= 1; z++)
    [unroll] for (int y = -1; y <= 1; y++)
    [unroll] for (int x = -1; x <= 1; x++)
    {
        float3 g = float3(x, y, z);
        float3 o = PP_Hash33(i + g);
        float3 r = g + o - f;
        float d = dot(r, r);
        if (d < f1) { f2 = f1; f1 = d; id = PP_Hash31(i + g + 7.7); }
        else if (d < f2) { f2 = d; }
    }
    return float3(sqrt(f1), sqrt(f2), id);
}

// ---------------------------------------------------------------- bump bez tangentów
// Mikkelsen 2010, "Bump Mapping Unparametrized Surfaces on the GPU": gradient powierzchni z pochodnych
// ekranowych. Działa na każdej bryle (także bez UV/tangentów), koszt: kilka ALU. Granulacja 2x2 px
// jest niewidoczna przy naszym rozmiarze postaci na ekranie.
float3 PP_PerturbNormal(float3 N, float3 positionWS, float height, float strength)
{
    float3 dpdx = ddx(positionWS);
    float3 dpdy = ddy(positionWS);
    float dhdx = ddx(height);
    float dhdy = ddy(height);
    float3 r1 = cross(dpdy, N);
    float3 r2 = cross(N, dpdx);
    float det = dot(dpdx, r1);
    float3 surfGrad = sign(det) * (dhdx * r1 + dhdy * r2);
    return normalize(abs(det) * N - strength * surfGrad);
}

// ---------------------------------------------------------------- maski

struct PotatoMasks
{
    half mud;        // pokrycie błotem
    half mudWet;     // mokra krawędź plamy (połysk)
    half frost;      // szron / lód
    half frostThick; // gruba skorupa lodu
    half burn;       // zwęglenie
    half ember;      // żarząca się krawędź
    half height;     // wkład masek do pola wysokości (błoto i lód są wypukłe)
};

// Wspólny "front" odsłaniania: amount 0 -> nic, amount 1 -> cała postać.
half PP_Coverage(half field, half amount, half softness)
{
    half t = lerp(1.3h, -0.1h, amount);
    return smoothstep(t - softness, t + softness, field);
}

PotatoMasks PP_EvaluateMasks(float3 charPos, float3 charN)
{
    PotatoMasks m = (PotatoMasks)0;
    float3 p = charPos * _MaskScale;
    half receive = _MaskReceive;

    // --- BŁOTO: rozbryzg od dołu (stopy, brzuch) i od strony uderzenia; mokre, błyszczące krawędzie
    UNITY_BRANCH if (_MudAmount > 0.001)
    {
        half n = (half)PP_Fbm(p * 2.2 + 17.0);
        half fromBelow = saturate(1.0 - (charPos.y + 0.55) / 1.25);
        half fromDir = saturate(dot(charN, normalize(_MudDir.xyz + 1e-4))) * _MudDir.w;
        half field = fromBelow * 0.45 + fromDir * 0.35 + n * 0.55;
        half t = lerp(1.3h, -0.1h, _MudAmount * receive);
        m.mud = smoothstep(t - 0.03, t + 0.03, field);
        half core = smoothstep(t + 0.02, t + 0.22, field);
        m.mudWet = m.mud * (1.0 - core);
        m.height += m.mud * 0.35 + core * 0.25 * n;
    }

    // --- SZRON: krystaliczne komórki narastające od góry i z krawędzi komórek Worleya
    UNITY_BRANCH if (_FrostAmount > 0.001)
    {
        float3 w = PP_Worley(p * 3.5 + 3.0);
        half facets = saturate((w.y - w.x) * 4.0);          // 0 na krawędziach komórek = żyłki szronu
        half n = (half)PP_Fbm(p * 1.6 - 11.0);
        half fromTop = saturate(charN.y * 0.5 + 0.5);
        half field = fromTop * 0.4 + n * 0.45 + (1.0 - facets) * 0.25;
        half amount = _FrostAmount * receive;
        m.frost = PP_Coverage(field, amount, 0.06);
        m.frostThick = PP_Coverage(field, saturate(amount * 1.4 - 0.4), 0.15);
        m.height += m.frost * (0.15 + (1.0 - facets) * 0.25) + m.frostThick * 0.2;
    }

    // --- ZWĘGLENIE: od strony wybuchu, poszarpany front + żar na krawędzi i w pęknięciach
    UNITY_BRANCH if (_BurnAmount > 0.001)
    {
        half n = (half)PP_Fbm(p * 2.8 + 41.0);
        half facing = saturate(dot(charN, normalize(_BurnDir.xyz + 1e-4)) * 0.5 + 0.5);
        half field = lerp(0.55h, facing, _BurnDir.w) * 0.6 + n * 0.55;
        half t = lerp(1.3h, -0.1h, _BurnAmount * receive);
        m.burn = smoothstep(t - 0.02, t + 0.05, field);
        half edge = smoothstep(t - 0.10, t, field) * (1.0 - smoothstep(t + 0.02, t + 0.10, field));
        float3 cracks = PP_Worley(p * 5.0);
        half crack = 1.0 - smoothstep(0.0, 0.08, cracks.y - cracks.x);
        m.ember = saturate(edge + crack * m.burn * 0.6) * _BurnHeat;
        m.height -= m.burn * crack * 0.3;                    // pęknięcia są wklęsłe
    }
    return m;
}

#endif
