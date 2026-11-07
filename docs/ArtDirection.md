# Potatoes Party: Art Direction i shader bohatera

Dokument opisuje, jak ustaliliśmy styl graficzny gry i jak go zbudować. Ma trzy części: analizę referencji (Krok 1), trzy koncepcje wizualne (Krok 2) oraz wybraną koncepcję zaimplementowaną jako shader HLSL dla URP (Krok 3).

**Stan projektu, od którego zaczynamy.** Unity 6 (6000.6.7f1) z URP 17.6 i rendererem Forward+. MSAA 4x, SMAA i HDR są włączone. Volume ma Neutral tonemapping, bloom (threshold 1.0, intensity 0.35), lekki color grading i winietę. Ambient działa w trybie Trilight. Cała geometria, łącznie z ziemniakami, powstaje w kodzie z prymitywów (`Shapes.Prim`). **Nie mamy UV ani tekstur do malowania.** Dlatego wszystko, co dzieje się na powierzchni postaci (oczka, piegi, błoto, szron, zwęglenie), jest proceduralne i liczone w przestrzeni postaci. Każda decyzja poniżej uwzględnia to ograniczenie.

---

## Krok 1: analiza referencji

> Analiza powstała na podstawie tego, co widać w obrazie gier (gameplay, trailery, materiały prasowe). Nie korzystaliśmy z kodu ani dokumentacji studiów. Opisujemy techniki, które dają ten wygląd, a nie konkretną implementację autorów.

### 1.1 Fall Guys

| Warstwa | Co widać | Dlaczego działa |
|---|---|---|
| **Kształt** | Sylwetka „fasolki”: jedna duża, wypukła bryła bez ostrych krawędzi. Kostiumy nie zmieniają obrysu bardziej, niż to konieczne. | Czytelność wynika z kształtu, zanim zadziała kolor. Gracz rozpozna swoją postać w tłumie 60 osób nawet w miniaturze. |
| **Materiał postaci** | Stylizowane PBR o niskiej mikrostrukturze: szeroki, miękki odblask jak na gumie lub matowym winylu. Albedo jest jasne i bardzo nasycone, bez wypalonych bieli. Terminator światła jest szeroki, bo wrap diffuse lub mocne SH sprawiają, że strona w cieniu nie spada do czerni. | Postać wygląda jak zabawka, którą da się dotknąć. Brak twardego terminatora ukrywa niską gęstość siatki i daje efekt „miękkiej” bryły. |
| **Oświetlenie** | Scena high-key: jedno dominujące, ciepłe światło kierunkowe i bardzo mocny, chłodny ambient (niebo). Cienie są wyraźne kształtem, ale jasne, czyli z wysoką wartością cienia i kolorem nieba. | Kontrast budują **barwa** i **kształt cienia kontaktowego**, a nie jasność. Dzięki temu wszystko jest czytelne także w cieniu. |
| **AO** | Wyraźny, krótki AO kontaktowy pod postaciami i przy styku przeszkód z podłożem. Szerokiego, brudzącego AO nie ma. | „Uziemienie” postaci w świecie, który poza tym jest bardzo czysty. |
| **Paleta świata** | Pastelowe, ale wysoko nasycone tła. Gameplayowo ważne obiekty dostają kolory komplementarne (różowe przeszkody na niebieskim niebie, żółte platformy). Paleta jest ograniczona do mniej więcej 6 rodzin barw na poziom. | Hierarchia: tło > plansza > przeszkody > postacie. Każda warstwa ma inny zakres nasycenia i jasności. |
| **Post-processing** | Bloom o niskim progu na emisyjnych akcentach (konfetti, mety). Lekka winieta, mocny grading w stronę ciepłych świateł. Brak aberracji chromatycznej i brak DoF w gameplayu. DoF pojawia się w menu i w prezentacjach postaci. | Gra z kamerą trzecioosobową i tłumem nie może rozmywać ani rozszczepiać obrazu, bo to utrudnia odczyt. |
| **Ruch jako shading** | Squash & stretch, wtórna animacja i „galaretowate” odbicia. | Część „soczystości” daje animacja, a nie shader. Materiał może być prosty, bo ruch go ożywia. |

### 1.2 Pummel Party

| Warstwa | Co widać | Dlaczego działa |
|---|---|---|
| **Kamera i skala** | Widok z góry pod kątem, postacie małe na ekranie, dużo graczy w jednym kadrze. | Detal na postaci jest prawie niewidoczny. Liczą się kolor gracza, sylwetka i kontrast z podłożem. |
| **Postacie** | Proste bryły o płaskim, mocno nasyconym kolorze i niskim połysku. Czytelność daje głównie jasność postaci względem ciemniejszego podłoża. | Przy dużej odległości kamery mikrodetal byłby szumem, więc lepiej działa „plama koloru” z jasną krawędzią. |
| **Oświetlenie** | Bardziej dramatyczne niż w Fall Guys: kolorowe światła punktowe, wyraźne „wyspy” światła na planszy i ciemniejsze otoczenie na wielu mapach. | Kompozycja światłem: oko trafia tam, gdzie jest gameplay. Nasz `Look.KeyLight` realizuje już ten wzorzec. |
| **Efekty** | Mocne, krótkie VFX (wybuchy, błyski), ciepła emisja i bloom. Efekty mają wyraźny kontur kształtu. | Feedback trafienia czyta się w 1–2 klatki, nawet w chaosie. |
| **Post-processing** | Bloom i umiarkowany grading. Detal tonalny jest raczej skompresowany niż filmowy. | Stabilny, czytelny obraz bez agresywnego tonemappingu, który zmieniałby kolory graczy. |

### 1.3 Wnioski techniczne dla nas

1. **Czytelność = kształt + krawędź + temperatura.** Postać musi mieć jasną krawędź, niezależną od cieni (rim gameplayowy), i inną temperaturę barwową niż tło.
2. **Cień nie może zjadać koloru gracza.** Kolor gracza jest informacją gameplayową, więc strona w cieniu musi zachować nasycenie. Służą do tego wrap diffuse, kolorowane SSS i mocne SH.
3. **Tonemapping nie może przesuwać barw graczy.** ACES desaturuje i przesuwa barwy nasycone (czerwony idzie w pomarańcz, niebieski w fiolet). Zostajemy przy Neutral, a „filmowość” budujemy gradingiem.
4. **Detal na postaci tylko w średniej i dużej skali.** Kamera jest współdzielona i potrafi się mocno oddalić, więc mikrodetal ma być subtelny i nigdy nie może dawać aliasingu odblasku.
5. **Feedback gameplayowy na materiale** (błoto, szron, oparzenia, błysk) robi ogromną część „soczystości”. To nasz wyróżnik, bo referencje robią to głównie efektami cząsteczkowymi, a my możemy robić to na skórze postaci.

---

## Krok 2: trzy koncepcje wizualne

### Koncepcja A: „Plastelinowa Niedziela” (Claymation Diorama)

**Vibe.** Stop-motion na kuchennym stole. Wszystko ulepione z plasteliny i masy solnej, widać ślady palców, a świat ma skalę miniatury. Ciepło, rękodzieło, humor w stylu filmów poklatkowych.

**Materiał ziemniaków.**
- Plastelina to materiał o wysokiej szorstkości (smoothness 0.2–0.3) z **bardzo szerokim, słabym odblaskiem** i lekkim woskowym połyskiem na wypukłościach.
- Proceduralne odciski palców: tri-planar Worley w przestrzeni postaci, z normalną z pochodnych ekranowych i amplitudą malejącą z odległością kamery.
- Fake SSS o krótkim zasięgu (wosk), z kolorem rozpraszania o oktawę cieplejszym od albedo.
- **Animacja na „dwójkach”**: shader dostaje `_StepTime` i kwantyzuje deformację wierzchołków (lekki szum pozycji co 1/12 s). Daje to drżenie powierzchni typowe dla animacji poklatkowej.

**Oświetlenie.**
- Jedno miękkie światło kluczowe o dużym kącie (symulacja softboxa: łagodne PCF i duży `shadowNormalBias`) plus wyraźny, ciepły bounce od blatu.
- GI: SH z mocnym, ciepłym kolorem od dołu (ground w Trilight `#C9925C`) i chłodnym od nieba. Cienie brązowo-czerwone, nigdy szare.

**Post-processing.**
- Tonemapping Neutral, Color Adjustments z kontrastem +10 i saturacją +5.
- **Depth of Field Bokeh** jako tilt-shift: ogniskowa sprzężona z odległością kamery od środka areny, przysłona f/2.8–4. To główny środek stylu, bo daje efekt miniatury.
- Bloom: threshold 1.1, intensity 0.2. Bez aberracji chromatycznej.
- Film grain *Thin1* o intensywności 0.15, który maskuje banding i dokłada fakturę.

**Ryzyka.** DoF jest drogi i przy oddalonej kamerze zaczyna rozmywać graczy na krawędziach areny. Plastelina słabo znosi neonowe kolory graczy. Pracochłonne mapy, bo całe otoczenie trzeba przerobić na „ulepione”.

### Koncepcja B: „Cukierkowe Szkło” (Hard Candy Funfair)

**Vibe.** Wesołe miasteczko z cukierków: twarde landrynki, lukier, żelki. Ziemniaki wyglądają jak glazurowane figurki z polewą. Maksymalna jaskrawość, „neonowy pastel”.

**Materiał ziemniaków.**
- Dwuwarstwowy model: matowa baza i **clear coat** (drugi lobe GGX o roughness 0.05, F0 0.04), który daje ostry, biały odblask jak polewa.
- Pseudo-refrakcja: albedo modulowane `SampleSH(refract(-V, N, 0.8))`, dzięki czemu postać „łapie” kolor nieba przez polewę.
- Transmisja o długim zasięgu (żelek) z koloryzacją Beer-Lambert: `exp(-thickness * sigma)`, gdzie sigma to dopełnienie koloru gracza.

**Oświetlenie.**
- Dwa światła kierunkowe: ciepłe kluczowe i chłodne, kontrujące z tyłu bez cieni, które obrysowuje postacie odbiciem w clear coacie.
- Reflection probe z ręcznie malowanym, kolorowym „studiem” (paski softboxów), bo polewa potrzebuje czegoś do odbicia.

**Post-processing.**
- Tonemapping Neutral. Bloom: threshold 0.9, intensity 0.6, scatter 0.75, z teksturą brudu (dirt) w formie cukrowych drobinek.
- Chromatic Aberration 0.04 tylko w menu i na podium, w gameplayu 0.
- Grading przez Lift/Gamma/Gain: lift w stronę magenty, gain w stronę brzoskwini.

**Ryzyka.** Wizualnie blisko Fall Guys, więc słabo wyróżnia się na rynku. Clear coat aliasuje na małych postaciach, a bloom na odblaskach w tłumie 6 postaci zaczyna świecić jak choinka.

### Koncepcja C: „Aksamitna Bulwa” (Velvet Tuber) ✅ wybrana

**Vibe.** „Designer toy” z miękkiego, aksamitnego winylu, który jest jednocześnie wiarygodnym warzywem. Ziemniak ma matowy, aksamitny „meszek” na krawędziach, wyczuwalne oczka i ciepły, miąższowy blask tam, gdzie przez skórkę przechodzi światło. Światy wyglądają jak sesja zabawek w słonecznym studiu, czyli czysto, jasno i z wyraźnym kierunkiem światła. To bezpośrednio rozwija obecny „jasny, słoneczny i schludny” wygląd gry, a nie go zastępuje.

**Dlaczego ta.**
- **Unikalność:** aksamitny sheen i miąższowe SSS to materiał, którego nie ma u konkurencji (tam dominuje guma lub plastik i płaski kolor). Dobrze widać go na miniaturach i w trailerach.
- **Czytelność za darmo:** sheen jest fizycznie umotywowanym rimem. Postać ma jasną krawędź „z natury”, a rim gameplayowy tylko to wzmacnia.
- **Gameplay na skórze:** miękki materiał przekonująco brudzi się, zamarza i przypala. Maski stają się częścią komedii (przypalony ziemniak po Gorącym Ziemniaku, ochlapany błotem po fałszywym złocie).
- **Koszt wdrożenia:** pasuje do proceduralnej geometrii, bo nie potrzebuje UV ani tekstur, i do obecnego światła oraz Volume.

**Materiał ziemniaków (warstwy, od dołu):**
1. **Skórka (albedo):** kolor gracza jako „barwiona skórka” plus proceduralne oczka (Worley, rzadkie dołki) i piegi, które przyciemniają albedo i obniżają gładkość.
2. **Miąższ (fake SSS):** wrap diffuse plus **pas rozpraszania przy terminatorze** w kolorze miąższu `_FleshColor` (ciepły, żółto-pomarańczowy). Cień na krawędzi jest miękki, bo SSS „widzi” złagodzony shadow attenuation.
3. **Translucencja:** przy świetle z tyłu cienkie części (rączki) świecą ciepło. Grubość jest ustawiana per część ciała.
4. **Odblask:** GGX z opcjonalnym, stylizowanym kształtem (`_SpecularBand`), który daje „zabawkowy”, wyraźny refleks bez aliasingu.
5. **Aksamit (sheen):** Charlie sheen (Estevez & Kulla) z widocznością Neubelta to meszek na krawędziach, zależny od światła.
6. **Rim gameplayowy:** Fresnel niezależny od cieni, z domieszką koloru gracza, mocniejszy od strony nieba i po stronie przeciwnej do słońca.
7. **Maski:** błoto, szron i zwęglenie z żarem nadpisują albedo, gładkość, SSS, sheen, relief i emisję.

**Architektura oświetlenia.**
- **Kluczowe:** jedno słońce, ciepłe (2–3 stopnie cieplejsze niż biel, `t.SunColor`), elewacja 50°, miękkie cienie z `shadowStrength 0.5` (już jest w `Look.Setup`). Cień ma być jasny i chłodny, nie czarny.
- **GI:** Trilight SH: niebo chłodne i jasne, horyzont neutralny, ziemia ciepła i ciemniejsza (już jest w `Look.Ambient`). Shader próbkuje SH dwa razy: `SampleSH(N)` dla ambientu i `SampleSH(-N)` dla światła „zza postaci”, które przechodzi przez miąższ.
- **Kolor cieni:** chłodny lawendowy przez *Shadows / Midtones / Highlights* (dodane w `Look.BuildVolume`). Kontrast temperatur zamiast kontrastu jasności.
- **Światło kompozycyjne:** spot nad areną (`Look.KeyLight`) zostaje. Dla mocnego backlightu (wulkan, pochodnie) translucencja da ciepłe krawędzie rączek.
- **AO:** obiektowy „ground occlusion” w shaderze (dół postaci ciemnieje) działa zawsze. SSAO z URP jest opcjonalne, a shader ma przebieg `DepthNormals` i obsługę `_SCREEN_SPACE_OCCLUSION`.

**Stos post-processingu (docelowy).**

| Efekt | Ustawienie | Uzasadnienie |
|---|---|---|
| Tonemapping | **Neutral** | Nie przesuwa barw graczy (ACES zmienia czerwony w pomarańcz). |
| Color Adjustments | exposure +0.1, contrast +6, saturation +12 | Bez zmian. Kontrast trzymamy nisko, bo resztę robi temperatura. |
| Shadows/Midtones/Highlights | cienie (0.94, 0.94, 1.08), światła (1.04, 1.01, 0.95), cienie do 0.3, światła od 0.55 | Chłodne cienie i ciepłe światła: główny środek „studyjnego” wyglądu. |
| Bloom | threshold 1.0, intensity 0.35, scatter 0.6 | Łapie tylko żar oparzeń, iskry szronu i emisję. Rim i sheen są poniżej progu, żeby postacie nie świeciły. |
| Vignette | 0.12, smoothness 0.6 | Delikatne skupienie na środku areny. |
| Depth of Field | **wyłączony w gameplayu**. Bokeh tylko w menu, lobby i na podium (ogniskowa na postaci, f/2) | Współdzielona kamera z tłumem nie może rozmywać graczy. |
| Chromatic Aberration | **0 w gameplayu**, 0.15 przez 0.2 s przy wybuchu (impuls) | Jako feedback uderzenia, nie jako stała stylizacja. |
| Film Grain | 0 (opcjonalnie 0.08 *Thin1* na PC High) | Maskuje banding w gradientach nieba. |
| Antyaliasing | MSAA 4x + SMAA High (jest) | Sheen i rim to efekty krawędziowe, więc muszą być wygładzone. |

---

## Krok 3: Shader Bohatera Ziemniaka

### 3.1 Pliki

| Plik | Rola |
|---|---|
| `Assets/Shaders/PotatoHero/PotatoHero.shader` | Właściwości, przebiegi `UniversalForwardOnly`, `ShadowCaster`, `DepthOnly`, `DepthNormals`, słowa kluczowe. |
| `Assets/Shaders/PotatoHero/PotatoHeroInput.hlsl` | `CBUFFER UnityPerMaterial` (zgodny z SRP Batcherem) i przejście do przestrzeni postaci. |
| `Assets/Shaders/PotatoHero/PotatoHeroCommon.hlsl` | Szum (value, fBm, Worley), bump bez tangentów i ewaluacja masek. |
| `Assets/Shaders/PotatoHero/PotatoHeroForwardPass.hlsl` | Model oświetlenia: wrap/SSS, translucencja, GGX, sheen, rim, maski, GI. |
| `Assets/Scripts/Runtime/Gameplay/PotatoSurface.cs` | Stan masek i `MaterialPropertyBlock`, API dla minigier. |
| `Shapes.HeroMat(...)` / `PotatoPart` | Materiały per część ciała, z zapasowym URP Lit. |

### 3.2 Logika fragmentu (kolejność obliczeń)

```
1. przestrzeń postaci   cp = worldToLocal(Visual) * positionWS      (spójne maski na wszystkich bryłach)
2. skórka               Worley(cp * 3.2) -> oczka (dołki), ValueNoise -> piegi, mikro-relief
3. maski                błoto / szron / zwęglenie -> pokrycie, mokra krawędź, gruba skorupa, żar, wkład w wysokość
4. normalna             N = perturb(Ngeo, ddx/ddy(height))          (Mikkelsen 2010, bez UV i tangentów)
5. powierzchnia         albedo, smoothness, scatter, thickness, sheen, emisja <- maski
6. światła bezpośrednie main + additional (Forward+ cluster loop) -> PP_DirectLight
7. pośrednie            SH(N) * albedo, SH(-N) * miąższ (przez postać), sheen * SH, odbicia z sondy
8. rim gameplayowy      Fresnel * maska "od słońca" * maska "od nieba", nie zależy od cieni
9. emisja + błysk + mgła
```

### 3.3 Zintegrowany Fresnel / Rim Lighting

Są dwa niezależne mechanizmy, każdy z inną rolą:

**a) Sheen (Charlie): krawędź „w świetle”.** Fizycznie umotywowany meszek. Świeci tylko tam, gdzie pada światło, więc wzmacnia formę, ale w cieniu znika:

```hlsl
half PP_D_Charlie(half NdotH, half roughness)
{
    half invR = 1.0h / max(roughness, 0.07h);
    half sin2 = max(1.0h - NdotH * NdotH, 0.0078125h);
    return (2.0h + invR) * pow(sin2, invR * 0.5h) / (2.0h * PI);
}
// sheen = _SheenColor * D_Charlie * V_Neubelt * wrapped * light
```

**b) Rim gameplayowy: krawędź „zawsze”.** To nie jest światło, tylko sygnał czytelności. Celowo **nie** jest mnożony przez shadow attenuation, więc ziemniak w cieniu przeszkody nadal ma kontur:

```hlsl
half rim = pow(1.0h - NdotV, _RimPower);
half awayFromSun = saturate(0.5h - 0.5h * dot(N, mainLight.direction));
rim *= lerp(1.0h, awayFromSun * 1.6h, _RimLightAlign);   // kontra do słońca = naturalny backlight
rim *= 0.55h + 0.45h * saturate(N.y + 0.5h);              // od nieba mocniej, przy ziemi słabiej
half3 rimTint = lerp(_RimColor.rgb, saturate(_BaseColor.rgb * 1.6h + 0.15h), _RimPlayerTint);
color += rimTint * rim * _RimStrength * s.occlusion;      // AO gasi rim we wklęsłościach
```

Strojenie: `_RimPlayerTint` 0.4–0.5 daje kolorowy kontur gracza, który dodatkowo wzmacnia identyfikację (obok pierścienia pod stopami). Rim ma zostać **poniżej progu bloomu**, więc `_RimStrength` nie powinien przekraczać 1.0 przy `_RimColor` LDR. Brzuszek i twarz to osobne sfery nachodzące na tułów, dlatego mają rim 0.25 i 0.15, żeby nie rysowały „pierścieni” na ciele.

### 3.4 Fake Subsurface Scattering

Trzy składowe, wszystkie tanie (bez dodatkowych przebiegów i bez blura w przestrzeni ekranu):

```hlsl
// 1) wrap diffuse, energetycznie normalizowany: terminator przesuwa się w stronę cienia
half wrapped = saturate((NdotL + w) / ((1.0h + w) * (1.0h + w)));

// 2) pas rozpraszania: dokładnie tam, gdzie wrap "dodał" światło, wstawiamy kolor miąższu
half sssBand   = saturate(wrapped - NdotL * 0.85h);
half sssShadow = lerp(shadow, 1.0h, 0.35h * scatter);     // światło przecieka w cień na krawędzi
diffuse = albedo * lerp(NdotL, wrapped, scatter * 0.5h) * shadow
        + flesh * sssBand * scatter * 1.6h * sssShadow;

// 3) translucencja pod światło (Barré-Brisebois & Bouchard, GDC 2011)
half3 Ht   = normalize(L + N * _TransDistortion);
half trans = pow(saturate(dot(V, -Ht)), _TransPower) * _TransScale;
transmission = flesh * trans * (1.0h - thickness) * scatter * lerp(1.0h, shadow, 0.5h);
```

Do tego światło pośrednie „przez postać”: `SampleSH(-N) * flesh * scatter * (1 - thickness)`. Strona w cieniu dostaje odrobinę koloru nieba przefiltrowanego przez miąższ. To ten efekt sprawia, że ziemniak nie wygląda jak kamień nawet w pełnym cieniu.

Grubość per część ciała (`Shapes.HeroMat`): tułów 0.65, brzuszek 0.7, rączki 0.15 (mocno prześwitują), plamki i stopy 0.8.

### 3.5 Maski interaktywne (błoto, szron, oparzenia)

**Przestrzeń postaci.** Ziemniak składa się z ok. 10 brył. Gdyby maski liczyć w przestrzeni obiektu każdej bryły, plama błota „pękałaby” na granicy tułowia i rączki. `PotatoSurface` co klatkę przekazuje trzy wiersze macierzy `Visual.worldToLocalMatrix` (`_PP_Root0..2`), więc wszystkie bryły liczą szum w jednym układzie. Ten układ obraca się z fikołkami i zgniataniem, dzięki czemu plama jest „przyklejona” do ciała. Bez skryptu (`_PP_UseRoot = 0`) shader wraca do przestrzeni obiektu, więc materiał działa też w edytorze na dowolnej bryle.

**Wspólny front odsłaniania.** Każda maska to pole skalarne `field` w zakresie mniej więcej 0..1, zbudowane z kierunku i szumu, oraz próg przesuwany przez `amount`:

```hlsl
half t = lerp(1.3h, -0.1h, amount);           // amount 0 -> nic, 1 -> całość
coverage = smoothstep(t - soft, t + soft, field);
```

Dzięki temu animacja `amount` daje organiczny, poszarpany front narastania i zanikania, a nie przenikanie alfą.

| Maska | Pole `field` | Wpływ na materiał |
|---|---|---|
| **Błoto** | `fromBelow` (od stóp) + `fromDir` (od strony rozbryzgu, `_MudDir`) + fBm | Albedo `_MudColor`, gładkość 0.25 w środku plamy i **0.88 na mokrej krawędzi**, SSS i sheen wyłączone, plama wypukła w reliefie. |
| **Szron** | `fromTop` + fBm + krawędzie komórek Worleya (żyłki kryształów) | Albedo w stronę `_FrostColor`, gruba skorupa o gładkości 0.93, **SSS zmienia kolor na zimny cyjan** (światło pod lodem), mniejsza grubość, migoczące drobinki (emisja), zimny rim. |
| **Zwęglenie** | `facing` do wybuchu (`_BurnDir`) + fBm | Albedo sadzy, gładkość 0.12, SSS gaszone, **żar HDR na krawędzi frontu i w pęknięciach** (Worley F2−F1) z migotaniem. `_BurnHeat` gaśnie w ok. 1 s, sadza zostaje ok. 30 s. Bloom łapie żar. |
| **Błysk** | brak (jednolity) | `lerp` do bieli HDR przy mocnym uderzeniu (`Potato.Knock` przy stun ≥ 0.5). |

Kolejność nakładania: zwęglenie, potem błoto, na końcu szron, bo szron przykrywa wszystko. Kierunki (`_MudDir`, `_BurnDir`) są zapisywane **w przestrzeni postaci w chwili trafienia**, więc przypalony bok obraca się razem z ziemniakiem.

**API dla minigier (`PotatoSurface`):**

```csharp
p.Surface.SplashMud(fromWorldPos, 0.7f);   // rozbryzg błota od strony punktu
p.Surface.Scorch(explosionPos, 0.8f);      // zwęglenie + żar od strony wybuchu
p.Surface.Freeze(0.5f);                    // natychmiastowy szron (topnieje do FrostTarget)
p.Surface.FrostTarget = 0.35f;             // stały poziom szronu na mapie (lód)
p.Surface.HitFlash(1f);                    // biały błysk
```

`p.Surface` ma wartość `null`, gdy shader jest niedostępny (Built-in), więc każde wywołanie ma sprawdzenie na null. Podpięte w tym PR:
- **Gorący Ziemniak:** wybuch przypala ocalałych w promieniu 4 m (mocniej bliżej) i ofiarę.
- **Złote Ziemniaki:** fałszywe złoto ochlapuje błotem od strony monety.
- **Pękający Lód:** postacie powoli łapią szron do 35%.
- **Każde mocne uderzenie:** krótki błysk.

Pomysły na dalsze podpięcia: Deszcz Garnków (szron od lodowego garnka), Malarze (`_MudColor` = kolor farby jako „ochlapanie farbą”), Wulkan (stały lekki żar od podłoża, `_BurnDir` = dół), Kombajn (błoto przy wejściu w uprawę).

### 3.5b Skórki: wzory i metal

Skórki (`Gameplay/PotatoSkins.cs`) to kolory części ciała, dodatek na głowę oraz wzór liczony w shaderze w tej samej przestrzeni postaci co maski, więc paski i łaty płynnie przechodzą przez tułów i rączki. Właściwości materiału: `_PatternType` (0 brak, 1 paski, 2 łaty, 3 kosmos, 4 magma, 5 brokat), `_PatternColor`, `_PatternGlow` (HDR, emisja), `_PatternScale` i `_Metallic`.

- `PP_EvaluatePattern` zwraca maskę, świecenie, wysokość (do bumpu) i zmianę szorstkości. Szerokość filtra `cpWidth = length(fwidth(cp))` liczymy przed gałęzią, bo pochodne w dynamicznym `if` są niezdefiniowane. Wzory są antyaliasowane tą szerokością.
- **Metal** (Złota Bulwa) przesuwa F0 w stronę koloru skórki, przyciemnia rozproszenie `diffuse * (1 - metallic)` i barwi odbicie otoczenia, więc złoto naprawdę odbija sondę odbić sceny.
- **Błoto, szron i zwęglenie przykrywają wzór:** gaszą jego emisję i metal, więc ubłocona Złota Bulwa przestaje błyszczeć, a Magmowa w szronie przestaje świecić.
- Kontur (rim) ma kolor gracza (`_PlayerColor`), więc gracza da się rozpoznać niezależnie od skórki.
- Wzór dostają tylko tułów, rączki i plamki (`Shapes.HeroMat(..., skin, skinIndex, player)`), a materiały są cache'owane po skórce i kolorze gracza.

**Reakcje na świat** (`Gameplay/SkinFx.cs`): jedna tabela zasad dla wszystkich map. Lądowanie zostawia na stopach podłoże mapy (ziemia, piasek, trawa, mąka, popiół), uderzenie zrywem daje plamę od strony napastnika, lód szroni, gorące kamienie wulkanu powoli przypiekają stopy, lawa zwęgla, a woda zmywa brud (`PotatoSurface.Wash`). Minigry wołają tylko te zdarzenia oraz `SplashMud`, `Scorch` i `Freeze` tam, gdzie mają własny pomysł (farba, zupa, słoma).

### 3.6 Bump bez UV

Postacie to prymitywy, więc relief liczymy z pochodnych ekranowych (Mikkelsen, *Bump Mapping Unparametrized Surfaces on the GPU*, 2010):

```hlsl
float3 dpdx = ddx(positionWS), dpdy = ddy(positionWS);
float  dhdx = ddx(height),     dhdy = ddy(height);
float3 r1 = cross(dpdy, N), r2 = cross(N, dpdx);
float  det = dot(dpdx, r1);
float3 surfGrad = sign(det) * (dhdx * r1 + dhdy * r2);
N = normalize(abs(det) * N - strength * surfGrad);
```

Wysokość to suma: oczka (wklęsłe), mikro-szum, błoto (wypukłe), szron (wypukły na kryształach) i pęknięcia zwęglenia (wklęsłe). Wszystkie maski automatycznie dostają relief bez dodatkowych tekstur. Granulacja 2x2 px przy naszym rozmiarze postaci na ekranie jest niewidoczna.

### 3.7 Wydajność

- **SRP Batcher:** wszystkie właściwości siedzą w jednym `UnityPerMaterial`. Renderery z `MaterialPropertyBlock` wypadają jednak z batchera. Przy 6 graczach i ok. 10 bryłach daje to ok. 60 osobnych draw calli, co na PC jest bez znaczenia. Gdyby kiedyś trzeba było 30+ postaci, maski przenosimy do `GraphicsBuffer` indeksowanego `unity_RendererUserValue` (`Renderer.SetShaderUserValue`) i wracamy do batchera.
- **ALU:** najdroższy jest Worley 3x3x3 (do 4 wywołań, gdy szron i zwęglenie są aktywne naraz). Maski są w `UNITY_BRANCH` zależnych od uniformów, więc gdy maska ma wartość 0, nie kosztuje nic. Na słabszy sprzęt: wariant z Worleyem 2x2x2 albo `shader_feature _PP_LOW` wyłączający szum skórki.
- **Warianty:** forward ma ok. 4 × 2 × 2 × 5 × 2 × fog. Jeśli build za bardzo urośnie, warto dodać stripping (`IPreprocessShaders`) dla nieużywanych `_SHADOWS_SOFT_*`.

### 3.8 Podpięcie: materiały i kamera

**Automatycznie (w tym PR):**
1. Shader leży w `Assets/Shaders/PotatoHero/`.
2. `ProjectSetup` (menu **Potatoes Party → Napraw ustawienia projektu**, uruchamiane też samo przy otwarciu projektu) tworzy `Assets/Resources/PotatoParty/PotatoHero.mat`. Bez tego materiału `Shader.Find` w buildzie nie znajdzie shadera.
3. `Potato.BuildVisual` nakłada `Shapes.HeroMat(kolor, PotatoPart.X)` na tułów, brzuszek, plamki, twarz, rączki i stopy. Oczy, usta, rękawiczki i kiełek zostają na URP Lit, żeby maski ich nie brudziły i żeby oczy pozostały czytelne.
4. `PotatoSurface` jest dodawany do każdego ziemniaka, gdy shader jest dostępny.
5. `Look.BuildVolume` dodaje *Shadows / Midtones / Highlights* (chłodne cienie, ciepłe światła).

**Renderer:** `ProjectSetup` (wersja v6) próbuje sam dodać **Screen Space Ambient Occlusion** do URP Renderer Data i włącza depth texture, 4 kaskady cieni oraz miękkie cienie wysokiej jakości. Gdyby SSAO się nie pojawiło, dodaj ręcznie: **Add Renderer Feature → Screen Space Ambient Occlusion**, Method *Interleaved Gradient*, Intensity 0.6, Radius 0.25, Direct Lighting Strength 0.15, Source *Depth Normals*. Shader ma przebieg `DepthNormals`, więc SSAO widzi ziemniaki poprawnie.

Sprawdzić, czy *Rendering Path* jest ustawiony na *Forward+* (ProjectSetup to wymusza). Shader obsługuje cluster light loop, więc pochodnie i spot areny oświetlają ziemniaki.

**Kamera:** HDR i post-processing są już włączone w `Look.Setup` (`allowHDR`, `renderPostProcessing`, SMAA High). Shader wymaga HDR, żeby żar oparzeń i iskry szronu przekraczały próg bloomu (1.0).

**Strojenie w edytorze:** w trakcie Play zaznacz `Ziemniak X` i w komponencie `PotatoSurface` przesuwaj suwaki Mud, Frost, Burn, Heat i Flash. Wartości materiału (rim, SSS, sheen) zmieniasz na `Resources/PotatoParty/PotatoHero.mat`. Uwaga: `Shapes.HeroMat` kopiuje ten materiał przy tworzeniu postaci, więc zmiany widać po restarcie minigry.

### 3.9 Odpowiednik w Shader Graph (dla zespołu, który woli węzły)

| Blok HLSL | Shader Graph |
|---|---|
| Przestrzeń postaci | `Position (World)` → `Vector4(..,1)` → 3× `Dot Product` z właściwościami `_PP_Root0..2` → `Lerp` z `Position (Object)` |
| Szum skórki | Custom Function `PP_Worley` / `PP_Fbm` (plik `PotatoHeroCommon.hlsl`, tryb *File*) |
| Bump bez UV | Custom Function `PP_PerturbNormal` → wyjście do `Normal (World Space)`, *Fragment Normal Space = World* |
| Wrap/SSS/translucencja/sheen | Unlit Graph + Custom Function z `PP_DirectLight` i pętlą świateł (Lit Graph nie pozwala podmienić BRDF) |
| Rim | `Fresnel Effect` (power = `_RimPower`) × `Dot(Normal, -MainLightDirection)` remapowany do 0..1 → Add do Emission |
| Maski | Custom Function `PP_EvaluateMasks` → `Lerp`-i na Base Color, Smoothness i Emission |

Zalecamy wersję HLSL: własny BRDF i tak wymaga Unlit Graph z Custom Function, a hand-written shader daje pełną kontrolę nad wariantami i przebiegami.
