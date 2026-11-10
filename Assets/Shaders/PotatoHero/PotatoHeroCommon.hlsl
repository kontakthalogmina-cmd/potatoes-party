// Potatoes Party – Shader Bohatera Ziemniaka v2
// Szum proceduralny, mikro-detal skórki i warstwy środowiskowe (kurz, błoto, woda, szron, zwęglenie,
// siniaki, rozcięcia). Wszystko w przestrzeni postaci i bez tekstur: ziemniaki są budowane z prymitywów
// w kodzie, więc nie mają sensownego UV – proceduralność jest tu cechą, nie obejściem.
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
// Mikkelsen 2010, "Bump Mapping Unparametrized Surfaces on the GPU": gradient wysokości z pochodnych
// ekranowych. Działa na każdej bryle bez UV i tangentów, koszt: kilka ALU.
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

// ---------------------------------------------------------------- mikro-detal skórki

struct PotatoSkinDetail
{
    half height;     // pole wysokości skórki (oczka wklęsłe, przetchlinki wypukłe)
    half eyes;       // maska "oczek" (pąków)
    half freckles;
    half lenticels;
    half net;        // siateczka korkowa
    half cavity;     // 1 = zagłębienie (tu najpierw osiada brud, tu najdłużej trzyma się woda)
    half micro;
};

// Skórka prawdziwego ziemniaka ma 4 skale detalu: (1) oczka ~1 cm, (2) przetchlinki ~1–2 mm (jasne
// kreseczki), (3) siateczka korkowa ~5 mm, (4) mikro-chropowatość. Każda dostaje własny szum.
PotatoSkinDetail PP_SkinDetail(float3 cp)
{
    PotatoSkinDetail d = (PotatoSkinDetail)0;
    float3 sp = cp * _SpeckleScale;
    float3 cell = PP_Worley(sp);
    d.eyes = (1.0h - smoothstep(0.08h, 0.26h, (half)cell.x)) * step(0.62h, (half)cell.z) * _SpeckleAmount;
    d.freckles = smoothstep(0.62h, 0.78h, (half)PP_ValueNoise(sp * 3.1 + 5.0)) * _SpeckleAmount;
    d.micro = (half)PP_ValueNoise(sp * 9.0);

    // przetchlinki: krótkie kreski rozciągnięte poziomo (szum anizotropowy)
    float len = PP_ValueNoise(float3(sp.x * 6.0, sp.y * 22.0, sp.z * 6.0) + 11.0);
    d.lenticels = smoothstep(0.80h, 0.88h, (half)len) * _LenticelAmount;

    // siateczka korkowa: krawędzie komórek Worleya o zmiennej skali
    UNITY_BRANCH if (_NetAmount > 0.01)
    {
        float3 w = PP_Worley(cp * 7.0 + 2.3);
        d.net = (1.0h - smoothstep(0.0h, 0.07h, (half)(w.y - w.x))) * _NetAmount;
    }

    d.height = -d.eyes * 0.8h + d.lenticels * 0.25h - d.net * 0.3h + d.micro * 0.12h;
    d.cavity = saturate(d.eyes + d.net * 0.6h + (1.0h - d.micro) * 0.2h);
    return d;
}

// ---------------------------------------------------------------- warstwy środowiskowe

struct PotatoLayers
{
    half dust;       // suchy kurz
    half mud;        // pokrycie błotem
    half mudWet;     // mokra krawędź plamy
    half wet;        // film wody na skórce
    half drop;       // krople (maska)
    half frost;
    half frostThick;
    half burn;
    half ember;
    half bruise;
    half cut;        // odsłonięty miąższ
    half cutRim;     // poszarpana, ściemniała krawędź skórki wokół rany
    half height;     // wkład warstw do pola wysokości
};

// Wspólny "front" odsłaniania: amount 0 → nic, amount 1 → cała postać.
half PP_Coverage(half field, half amount, half softness)
{
    half t = lerp(1.3h, -0.1h, amount);
    return smoothstep(t - softness, t + softness, field);
}

PotatoLayers PP_EvaluateLayers(float3 charPos, float3 charN, PotatoSkinDetail skin)
{
    PotatoLayers m = (PotatoLayers)0;
    float3 p = charPos * _MaskScale;
    half receive = _MaskReceive;
    half fromBelow = saturate(1.0h - (half)(charPos.y + 0.55) / 1.25h);

    // --- KURZ: od stóp w górę, najpierw w zagłębieniach skórki (wysokość decyduje o kolejności)
    UNITY_BRANCH if (_DustAmount > 0.001)
    {
        half n = (half)PP_Fbm(p * 3.4 + 91.0);
        half field = fromBelow * 0.7h + n * 0.35h + skin.cavity * 0.25h;
        m.dust = PP_Coverage(field, _DustAmount * receive, 0.12h);
    }

    // --- BŁOTO: rozbryzg od dołu i od strony uderzenia; mokre, błyszczące krawędzie; grube (wypukłe)
    UNITY_BRANCH if (_MudAmount > 0.001)
    {
        half n = (half)PP_Fbm(p * 2.2 + 17.0);
        half fromDir = saturate(dot(charN, normalize(_MudDir.xyz + 1e-4))) * _MudDir.w;
        half field = fromBelow * 0.45h + fromDir * 0.35h + n * 0.55h + skin.cavity * 0.1h;
        half t = lerp(1.3h, -0.1h, _MudAmount * receive);
        m.mud = smoothstep(t - 0.03h, t + 0.03h, field);
        half core = smoothstep(t + 0.02h, t + 0.22h, field);
        m.mudWet = m.mud * (1.0h - core);
        m.height += m.mud * 0.35h + core * 0.25h * n;
    }

    // --- WODA: film schnie od góry i z wypukłości; w zagłębieniach zostaje najdłużej.
    //     Krople: komórki Worleya z kulistą czapą; zacieki: anizotropowy szum spływający w dół.
    UNITY_BRANCH if (_WetAmount > 0.001)
    {
        half top = saturate((half)charPos.y * 0.6h + 0.5h);
        half field = (1.0h - top) * 0.5h + skin.cavity * 0.35h + (half)PP_ValueNoise(p * 4.0 + 3.0) * 0.3h;
        m.wet = PP_Coverage(field, _WetAmount * receive, 0.2h);

        float3 dp = p * (10.0 + _WetDrops * 8.0);
        float3 w = PP_Worley(dp);
        half radius = 0.18h + 0.17h * (half)w.z;
        half inDrop = step(0.45h, (half)w.z) * saturate(1.0h - (half)w.x / radius);
        m.drop = inDrop * saturate(_WetAmount * 1.6h - 0.2h) * receive;

        half streak = smoothstep(0.70h, 0.82h, (half)PP_ValueNoise(float3(p.x * 16.0, p.y * 2.2 + _Time.y * 0.55, p.z * 16.0)));
        m.wet = saturate(m.wet + streak * _WetAmount * 0.6h * receive);
        m.height += sqrt(saturate(m.drop)) * 0.45h + streak * 0.05h * _WetAmount;
    }

    // --- SZRON
    UNITY_BRANCH if (_FrostAmount > 0.001)
    {
        float3 w = PP_Worley(p * 3.5 + 3.0);
        half facets = saturate((half)(w.y - w.x) * 4.0h);
        half n = (half)PP_Fbm(p * 1.6 - 11.0);
        half fromTop = saturate(charN.y * 0.5h + 0.5h);
        half field = fromTop * 0.4h + n * 0.45h + (1.0h - facets) * 0.25h;
        half amount = _FrostAmount * receive;
        m.frost = PP_Coverage(field, amount, 0.06h);
        m.frostThick = PP_Coverage(field, saturate(amount * 1.4h - 0.4h), 0.15h);
        m.height += m.frost * (0.15h + (1.0h - facets) * 0.25h) + m.frostThick * 0.2h;
    }

    // --- ZWĘGLENIE
    UNITY_BRANCH if (_BurnAmount > 0.001)
    {
        half n = (half)PP_Fbm(p * 2.8 + 41.0);
        half facing = saturate(dot(charN, normalize(_BurnDir.xyz + 1e-4)) * 0.5h + 0.5h);
        half field = lerp(0.55h, facing, _BurnDir.w) * 0.6h + n * 0.55h;
        half t = lerp(1.3h, -0.1h, _BurnAmount * receive);
        m.burn = smoothstep(t - 0.02h, t + 0.05h, field);
        half edge = smoothstep(t - 0.10h, t, field) * (1.0h - smoothstep(t + 0.02h, t + 0.10h, field));
        float3 cracks = PP_Worley(p * 5.0);
        half crack = 1.0h - smoothstep(0.0h, 0.08h, (half)(cracks.y - cracks.x));
        m.ember = saturate(edge + crack * m.burn * 0.6h) * _BurnHeat;
        m.height -= m.burn * crack * 0.3h;
    }

    // --- SIŃCE
    UNITY_BRANCH if (_BruiseAmount > 0.001)
    {
        half n = (half)PP_Fbm(p * 3.1 + 63.0);
        half facing = saturate(dot(charN, normalize(_BruiseDir.xyz + 1e-4)));
        half field = facing * _BruiseDir.w * 0.75h + n * 0.5h;
        half t = lerp(1.3h, 0.15h, _BruiseAmount * receive);
        m.bruise = smoothstep(t - 0.12h, t + 0.10h, field);
        m.height -= m.bruise * 0.12h;
    }

    // --- ROZCIĘCIA: 2–3 równoległe "szramy" po stronie ciosu, poszarpane szumem; środek wklęsły
    //     (odsłonięty miąższ), krawędź lekko odstająca (zdarta skórka)
    UNITY_BRANCH if (_CutAmount > 0.001)
    {
        float3 cd = normalize(_CutDir.xyz + 1e-4);
        half facing = saturate(dot(charN, cd));
        float3 slash = normalize(cross(cd, float3(0.31, 0.95, 0.0)) + 1e-4);   // kierunek przecięcia ⟂ ciosu
        float3 q = p + _CutSeed.xyz;
        float jag = PP_Fbm(q * 9.0) - 0.5;
        float lines = dot(q, normalize(cross(slash, cd) + 1e-4)) * (6.0 * _CutSeed.w) + jag * 0.35;
        half stripe = 1.0h - (half)abs(frac(lines) - 0.5) * 2.0h;              // 1 na środku szramy
        half along = (half)PP_Fbm(float3(dot(q, slash) * 3.0, q.y, q.z) + 7.0); // przerywane wzdłuż
        half patch = facing * _CutDir.w;
        half amount = _CutAmount * receive;
        half width = 0.06h + 0.16h * amount;
        half core = smoothstep(1.0h - width, 1.0h - width * 0.4h, stripe) * smoothstep(0.35h, 0.55h, along) * smoothstep(0.35h, 0.7h, patch * (0.6h + amount));
        half rim = smoothstep(1.0h - width * 2.2h, 1.0h - width, stripe) * (1.0h - core) * smoothstep(0.3h, 0.5h, along) * smoothstep(0.3h, 0.65h, patch * (0.6h + amount));
        m.cut = core;
        m.cutRim = rim;
        m.height += rim * 0.35h - core * 0.6h;
    }
    return m;
}

#endif
