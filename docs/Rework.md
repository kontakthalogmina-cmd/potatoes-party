# Potatoes Party – rework „Cinematic Realism + Ultimate Responsiveness”

Dokument techniczny reworku: co zostało napisane od nowa, jak to działa i **jakie wartości wpisać w Inspektorze**.
Wszystkie liczby poniżej są też domyślnymi wartościami w kodzie, więc gra po wciśnięciu Play wygląda i działa tak,
jak opisano. Inspektor służy do strojenia na żywo.

---

## 0. Co trzeba zrobić po pobraniu

1. Otwórz projekt w Unity 6000.6.x. Package Manager sam doinstaluje z `Packages/manifest.json`:
   `com.unity.cinemachine 3.1.2`, `com.unity.visualeffectgraph 17.6.0`, `com.unity.inputsystem 1.11.2`.
   Jeśli Unity zaproponuje nowszą wersję Cinemachine 3.x albo Input System, zgódź się.
2. `ProjectSetup` przy pierwszym otwarciu przełączy **Active Input Handling → Both**. Unity poprosi o restart
   edytora – zrestartuj (bez tego pady i wibracje nie działają, klawiatura działa zawsze).
3. Menu **Potatoes Party → Napraw ustawienia projektu** – ustawia URP (HDR 64-bit, gradacja HDR, cienie, SSAO).
4. Play. Grafy VFX są opcjonalne: dopóki ich nie zbudujesz (sekcja 9), te same efekty robią cząsteczki zapasowe.

---

## 1. Architektura

```
Wejście (pad / klawiatura / bot)
   │
   ▼
Potato (fasada, API dla 12 minigier – bez zmian)
   ├── PotatoMotor   ruch: bieg, skok, zryw, grawitacja własna          (FixedUpdate, 100 Hz)
   ├── PotatoBody    animacja: squash & stretch, przechył, kończyny     (Update)
   └── PotatoSurface skóra: kurz, błoto, woda, szron, ogień, uszkodzenia (LateUpdate → shader)
   │
   ▼ zdarzenia (skok, lądowanie, zryw, cios, wybuch, nokaut)
Feel ─┬── TimeDirector   hit stop + time dilation + pauza  (jedyny właściciel Time.timeScale)
      ├── Impulses       Cinemachine Impulse Source (5 presetów)
      ├── Rumble         wibracje padów (2 silniki)
      ├── CinematicCamera.FocusOn / Punch   najazd i puls FOV
      ├── PostFx.Punch / Flash             aberracja, beczka, ekspozycja
      └── Vfx            VFX Graph (z zapasem na ParticleSystem)
```

Nowe pliki: `Feel/` (Spring, TimeDirector, Impulses, Rumble, Feel), `CameraRig/` (CinematicCamera, CameraDynamics),
`Rendering/` (Lighting, PostFx), `VFX/Vfx.cs`, `Gameplay/PotatoMotor.cs`, `Gameplay/PotatoBody.cs`.
Przepisane od zera: `Potato.cs`, `PotatoSurface.cs`, cały shader `PotatoHero` (4 pliki).
Usunięte: `SharedCamera.cs`, `GameFeel.cs`. `Look.cs` zawiera już tylko dekoracje nieba.

---

## 2. Kamera (Cinemachine 3)

Hierarchia tworzona z kodu (`CinematicCamera.Create`):

```
Kamera filmowa                 CinematicCamera
├── Main Camera                Camera, AudioListener, CinemachineBrain
├── Punkt kadru                (Transform, który śledzi kamera wirtualna)
└── CM Kamera areny            CinemachineCamera, CinemachinePositionComposer,
                               CameraDynamics, CinemachineImpulseListener
```

### 2.1 Wartości do Inspektora

| Komponent | Pole | Wartość | Po co |
|---|---|---|---|
| **CinemachineBrain** | Update Method | Late Update | ziemniaki mają interpolację Rigidbody |
| | Ignore Time Scale | ✔ | kamera płynie w hit-stopie i slow-motion |
| | Default Blend | Ease In Out, 0.8 s | |
| **CinemachineCamera** | Lens → Vertical FOV | 38 (menu 30) | dłuższy obiektyw = mniej zniekształceń z bliska |
| | Near / Far Clip | 0.2 / 400 | |
| | Tracking Target | Punkt kadru | |
| **Position Composer** | Camera Distance | sterowane z kodu (9–30 m) | |
| | Damping X / Y / Z | 0.18 / 0.18 / 0.30 | „organiczne” opóźnienie ponad SmoothDamp |
| | Dead Zone Depth | 0 | |
| | Center On Activate | ✔ | |
| **CinematicCamera** | Pitch Near / Far | 34° / 52° | nisko przy ciasnym kadrze, wyżej tylko gdy gracze się rozbiegną |
| | Min / Max Distance | 9 / 30 m (lobby 11, menu 13, podium 10) | wcześniej 12–34 |
| | Frame Margin | 2.2 m | zapas wokół skrajnych graczy |
| | Lead Time / Max Lead | 0.3 s / 2.5 m | kadr wyprzedza ruch grupy |
| | Focus Smooth Time | 0.22 s | |
| | Distance Smooth Time | 0.45 s (przybliżanie ×0.6) | kamera „niechętnie” odjeżdża od akcji |
| | Showcase Pitch / Orbit / Speed | 22° / ±10° / 0.07 Hz | menu, lobby, podium |
| **CameraDynamics** | Speed For Min / Max | 4 / 14 m/s | |
| | Speed Fov | +6° | „pęd” |
| | Dutch Per Speed / Max | 0.22°/(m/s) / 2.5° | przechył z bocznego ruchu kamery |
| | Dutch Frequency / Damping | 0.9 Hz / 0.7 | |
| | Handheld Amplitude / Boost / Hz | 0.12° / +0.18° / 0.3 | oddech „z ręki” |
| | Kick Frequency / Damping | 6.5 Hz / 0.45 | odrzut wzdłuż ciosu |
| | Punch Frequency / Damping | 5 Hz / 0.5 | puls FOV |
| **Impulse Listener** | Gain | 1 | |
| | Use 2D Distance | ✔ | wysokość nie osłabia wstrząsu |
| | Use Camera Space | ✔ | |
| | Channel Mask | 1 | |

### 2.2 Jak kamera „gra” akcję

1. **Kadr grupy** – środek ważony z prostokąta i średniej pozycji ludzi (boty tylko, gdy ludzi nie ma),
   plus wyprzedzenie o `0.3 s × prędkość grupy`. Dystans liczony z poziomego FOV, więc na ekranie 21:9 kamera
   może podejść bliżej niż na 16:9.
2. **Pochylenie zależne od dystansu** – `lerp(34°, 52°, smoothstep(t))`: z bliska widać twarze i horyzont,
   z daleka całą arenę.
3. **Najazd akcji** `FocusOn(punkt, zoom, czas)` – sprężyna 3.2 Hz wejścia i 1.1 Hz wyjścia, ζ 0.95.
   Cios: 6–12% na 0.35 s; wybuch: do 8% na 0.5 s; nokaut: 22% na 0.9 s; gol/meta: 15% na 1 s.
4. **Prędkość** – FOV +6°, dystans +8%, mocniejszy oddech kamery i motion blur (PostFx).

### 2.3 Presety Impulse Source

| Rodzaj | Impulse Shape | Duration | Impulse Type | Dissipation Rate | Typowa siła (Velocity) |
|---|---|---|---|---|---|
| Land | Bump | 0.14 s | Uniform | – | 0.03–0.12 |
| Hit | Recoil | 0.20 s | Uniform | – | 0.10–0.22 |
| Slam | Bump | 0.26 s | Dissipating | 0.35 | 0.08–0.25 |
| Explosion | Explosion | 0.55 s | Dissipating | 0.25 | 0.30–0.60 |
| Rumble | Rumble | 1.10 s | Uniform | – | 0.10–0.30 |

Kierunek impulsu = kierunek ciosu + 0.35 w dół (kamera „siada”, nie podskakuje).
`CinemachineImpulseManager.Instance.IgnoreTimeScale = true`.

---

## 3. Oświetlenie i cienie

| Światło | Typ | Obrót (X, Y) | Intensywność | Kolor | Cienie |
|---|---|---|---|---|---|
| Klucz (słońce) | Directional | 42°, −50° | 1.45 × `SunIntensity` mapy | `SunColor` mapy | Soft, Strength 0.9, Bias 0.02, Normal 0.25, Near 0.2, Soft Shadow Quality **High** |
| Kontra | Directional | 18°, 165° | 0.55 (wulkan 0.85) | #FFD9AE + 35% glow mapy | brak |
| Odbicie od ziemi | Directional | −35°, 120° | 0.16 | kolor podłoża | brak |
| Snop areny | Spot 60–150° | z góry | h² × 0.6 | biel + 40% glow | **Soft, 0.55** (cień kontaktowy pod stopami) |
| Otoczenie | Trilight | – | niebo × 0.85, horyzont × 0.6, ziemia × 0.4 | | |

Niższe słońce (42° zamiast ~52°) daje dłuższe cienie i lepszy modelunek brył. Kontra z naprzeciwka kamery
rysuje świetlisty kontur na krawędziach, a odbicie od ziemi rozjaśnia spody bez spłaszczania obrazu.

**Mgła** (perspektywa powietrzna): Linear, start 38 m, koniec 160 m, kolor = horyzont nieba.
**Sonda odbić**: Realtime / Via Scripting, 256 px, HDR, środek (0, 2.5, 0), rozmiar 400×200×400.

### URP Asset (ustawia `ProjectSetup`)

| Pole | Wartość |
|---|---|
| HDR / HDR Precision | ✔ / **64 Bits** |
| Color Grading Mode / LUT Size | **High Dynamic Range** / 32 |
| Anti-aliasing | MSAA 4× + SMAA High na kamerze |
| Main Light Shadow Resolution | 4096 |
| Shadow Max Distance | **50 m** |
| Cascade Count / Splits | 4 / 8% · 22% · 48% (0–4 m, 4–11 m, 11–24 m, 24–50 m) |
| Last Border | 15% |
| Depth Bias / Normal Bias | 0.6 / 0.5 |
| Soft Shadows Quality | High |
| Additional Lights | Per Pixel, shadows ✔, atlas 2048 |
| Rendering Path | Forward+ |
| Depth Texture | ✔ (DoF, SSAO) |
| SSAO (Renderer Feature) | Intensity 1.0, Radius 0.4, Direct Lighting Strength 0.25, pełna rozdzielczość |

---

## 4. Post-processing (`PostFx`)

| Efekt | Ustawienia |
|---|---|
| **Tonemapping** | ACES |
| **Bloom** | Threshold **1.0**, Intensity 0.32 (lód 0.45, wulkan 0.6), Scatter 0.72, Clamp 40, High Quality ✔ |
| **Color Adjustments** | Post Exposure 0.3 (+1.2 EV w błysku), Contrast 12, Saturation 16 (ACES zjada nasycenie) |
| **White Balance** | per mapa: lód −16, wulkan +16, kukurydza +14… |
| **Split Toning** | cienie #6675A3, światła #A38C70, balans 8 |
| **Shadows/Midtones/Highlights** | cienie (0.93, 0.94, 1.09, −0.02), światła (1.04, 1.01, 0.95, 0) |
| **Depth of Field** | **Bokeh**, ogniskowa 85 mm (menu 105 mm), 7 listków, krzywizna 0.85, obrót 12°, ostrość = punkt kadru, przysłona automatyczna |
| **Motion Blur** | Camera Only, Medium, Intensity = 0.018 × prędkość kamery (max 0.32), Clamp 0.05 |
| **Vignette** | 0.22 (+0.12 w ciosie), Smoothness 0.45, kolor #140F33 |
| **Chromatic Aberration** | 0.035 spoczynek, do 0.59 w ciosie |
| **Lens Distortion** | 0, w ciosie do −0.22 („zassanie”) |
| **Film Grain** | Thin1, 0.12, Response 0.8 |

### Precyzyjny DoF: automatyczna przysłona

Ostrość zawsze na punkcie kadru. Liczbę f wybieramy co klatkę tak, żeby **każdy żywy gracz** miał krążek
rozmycia ≤ 1.2 px. Wzór (sensor 24 mm, f = ogniskowa, S₁ = ostrość, S₂ = głębia gracza):

```
c = f² · |S₂ − S₁| / (N · S₂ · (S₁ − f))      →      N = f² · |S₂ − S₁| / (c_max · S₂ · (S₁ − f))
c_max = 1.2 px / wysokość_ekranu × 0.024 m
N = clamp(max po graczach, f/1.8, f/16)
```

Przysłona domyka się szybko (10/s, gdy ktoś wybiega z ostrości) i otwiera powoli (1.5/s, bez „pompowania”).
Efekt: tło rozmyte tak mocno, jak pozwala rozstawienie graczy – czytelność zostaje, głębia rośnie.
Jakość „Niska”: Gaussian tylko w menu, w grze DoF wyłączony, bez motion bluru i ziarna.

---

## 5. Shader ziemniaka v2 (`PotatoParty/PotatoHero`)

Pliki: `PotatoHero.shader` (właściwości, przebiegi), `PotatoHeroInput.hlsl` (CBUFFER, wgniecenie),
`PotatoHeroCommon.hlsl` (szum, detal skórki, warstwy), `PotatoHeroForwardPass.hlsl` (oświetlenie).
Zgodny z SRP Batcherem, Forward i Forward+.

### 5.1 Model oświetlenia

| Składnik | Model | Uwagi |
|---|---|---|
| Diffuse + SSS | **Spherical Gaussian per kanał** (dopasowanie S. Hilla do irradiancji SG, normalizowane) | zasięg RGB `_ScatterRadius` = (1.0, 0.42, 0.22) × 0.9 × (0.35 + 1.65 × krzywizna) |
| Półcień | kolorowy półcień Jimeneza: `shadow^(1/(1 + scatter·(1.4, 0.45, 0.2)))` | czerwonawa krawędź cienia na skórze |
| Transmisja | Barré-Brisebois + **Beer-Lambert** `exp(−σ·(0.25 + 2.5·grubość))`, σ = (0.35, 1.1, 2.4) | rączki prześwitują ciepło, tułów prawie wcale |
| Specular | **dwa płaty GGX** (0.85 × r, 0.15 × r/2), F0 0.028 | jak w skórze: szeroki blask + ostry połysk |
| Sheen | Charlie (Estevez & Kulla) | aksamitny meszek, woda go „skleja” |
| Woda | **clear-coat** GGX r ≈ 0.06, F0 0.02 + przyciemnienie bazy `albedo → albedo²` (Lagarde) | |
| Rim | gameplayowy Fresnel z kolorem gracza | niezależny od cieni |

### 5.2 Detal skórki (bez tekstur)

| Warstwa | Szum | Parametr | Wartość |
|---|---|---|---|
| Oczka (pąki) | Worley F1, rzadkie komórki | `_SpeckleAmount` / `_SpeckleScale` | 0.6 / 3.2 |
| Piegi | value noise ×3.1 | (razem z oczkami) | |
| Przetchlinki | anizotropowy value noise (×6, ×22, ×6) | `_LenticelAmount` | 0.35 |
| Siateczka korkowa | krawędzie Worleya ×7 | `_NetAmount` | 0 (skórki „russet”: 0.6) |
| Relief | bump bez tangentów (Mikkelsen 2010) | `_BumpStrength` | 0.03 |

### 5.3 Reakcja na środowisko (`PotatoSurface`)

| Warstwa | Skąd się bierze | Wygląd | Zanikanie |
|---|---|---|---|
| **Kurz** | bieg > 5 m/s po suchym podłożu, kolor podłoża mapy | jasny, matowy, od stóp w górę, najpierw w zagłębieniach | 0.01/s |
| **Błoto** | lądowanie, cios, kałuże, kombajn | ciemne, grube, mokra błyszcząca krawędź | po 4 s, 0.05/s |
| **Woda** | wpadnięcie do wody, `Soak()` | film (clear-coat), krople (Worley + kulista czapa), zacieki spływające w dół | 0.085/s, ×5 przy żarze; schnie od góry |
| Szron | lód, mokry zamarza 2× szybciej | | do `FrostTarget` |
| Zwęglenie | wybuch, lawa; odparowuje wodę | sadza + żar w pęknięciach | 0.03/s |
| **Wgniecenie** | każdy cios | wierzchołki (też w cieniu i SSAO), sprężyna 6.5 Hz ζ 0.3 | sprężyna |
| **Siniak** | każdy cios | ciemna plama, miąższ pod spodem krwisty | 0.03/s |
| **Rozcięcie** | cios ≥ 0.7 siły | 2–3 szramy ⟂ ciosu, odsłonięty wilgotny miąższ, brązowiejąca krawędź | 0.012/s (zostaje do końca minigry) |

Interakcje: woda zmywa kurz, mokra skórka zamiast kurzu łapie błoto, żar suszy wodę.

### 5.4 Ten sam shader jako Shader Graph (jeśli wolisz węzły)

URP 17 nie ma własnego modelu oświetlenia w Shader Graph, więc schemat to **Unlit + Custom Function**:

```
Blackboard: wszystkie właściwości z PotatoHero.shader (te same nazwy referencji, np. _ScatterRadius)

[Position (Object)] ─┐
[Normal (Object)]   ─┼─► Custom Function "PP_SkinDetail" (File: PotatoHeroCommon.hlsl)
                     │        out: Height, Eyes, Lenticels, Net, Cavity, Micro
                     └─► Custom Function "PP_EvaluateLayers" → Dust, Mud, MudWet, Wet, Drop, Cut, CutRim, Height
Height(skóra) + Height(warstwy) ─► [Normal From Height] (Tangent space, Strength 0.03) ─► N
[Base Color] ─► Lerp chain: siniak → rana → sadza → kurz → błoto → woda(albedo²) → szron ─► Albedo
Albedo, N, Smoothness, Coat ─► Custom Function "PotatoLighting" (File: PotatoHeroForwardPass.hlsl,
                               wywołuje GetMainLight + pętlę świateł, PP_DirectLight, SampleSH)
                               ─► Fragment.Base Color (Unlit)
Vertex: [Position (World)] ─► Custom Function "PP_ApplyImpact" ─► Transform World→Object ─► Vertex.Position
Graph Settings: Unlit, Opaque, Depth Write ✔, Cast Shadows ✔, Keywords: _MAIN_LIGHT_SHADOWS_CASCADE,
                _ADDITIONAL_LIGHTS, _SHADOWS_SOFT, _SCREEN_SPACE_OCCLUSION, _CLUSTER_LIGHT_LOOP (Multi Compile)
```

Ręcznie pisany HLSL zostaje wersją produkcyjną: jest szybszy (gałęzie po warstwach, które są wyłączone) i ma
własne przebiegi ShadowCaster/DepthNormals z wgnieceniem.

---

## 6. Sterowanie od zera (`PotatoMotor`)

Własna grawitacja (`Rigidbody.useGravity = false`). Skok definiujemy **wysokością i czasem do szczytu**:

```
g  = 2h / t²   = 2 · 1.45 / 0.30²  = 32.2 m/s²
v₀ = 2h / t    = 2 · 1.45 / 0.30   = 9.67 m/s
skok krótki (puszczony przycisk):  h ≈ v₀² / (2 · g · 2.4) ≈ 0.60 m
```

| Grupa | Pole | Wartość |
|---|---|---|
| Bieg | Move Speed (na Potato) | 7.5 m/s |
| | Ground Accel / Decel | 72 / 58 m/s² (0 → 7.5 m/s w ~0.1 s) |
| | Turn Accel (zawracanie, dot < −0.3) | 120 m/s² + „skid” (pył, odchył w tył) |
| | Air Accel / Air Decel | 30 / 5 m/s² (pęd w powietrzu zostaje) |
| | Stick Exponent | 1.35 (precyzja przy małym wychyleniu gałki) |
| | Stun Control | 12% przyspieszenia |
| | Turn Rate | 900°/s |
| Skok | Jump Height / Time To Apex | 1.45 m / 0.30 s |
| | Fall Gravity × | 1.65 (szybki opad = waga) |
| | Jump Cut Gravity × | 2.4 (zmienna wysokość) |
| | Apex Hang (|vy| < 1.4 m/s) | grawitacja × 0.55 |
| | Terminal Velocity | 26 m/s |
| | Coyote Time / Jump Buffer | 0.10 s / 0.13 s |
| | Ground Stick | 30 m/s² |
| | Knock Gravity (lot po ciosie) | 26 m/s² (długi, czytelny łuk) |
| Zryw | Dash Speed (na Potato) | 17.5 m/s |
| | Startup / Active / Recovery | **0.035 / 0.16 / 0.14 s** (≈ 2 / 10 / 8 klatek przy 60 FPS) |
| | Cooldown / Buffer | 0.85 s / 0.10 s |
| | Air Dash | 1 na skok, minimalne vy 1.5 m/s |

Tylko faza **Active** zrywu liczy się jako cios (`IsDashing`). W Startup postać hamuje do 35% i się zgniata
(antycypacja), w Recovery wraca do biegu ze sterowaniem 40%.

### Fizyka (Bootstrap)

| Ustawienie | Wartość | Dlaczego |
|---|---|---|
| Fixed Timestep | **0.01** (100 Hz), skalowany razem z czasem | zderzenia przy 17.5 m/s bez przelatywania, płynny slow-motion |
| Maximum Allowed Timestep | 0.1 | |
| Default Solver Iterations / Velocity | 8 / 2 | stabilne przepychanki 6 graczy |
| Bounce Threshold | 1.5 m/s | miękkie lądowania nie sprężynują |
| Default Contact Offset | 0.01 | brak lewitowania nad podłogą |
| Sleep Threshold | 0.005 | |
| Ziemniak | Capsule r 0.5, h 1.3, masa 1, Interpolate, Continuous Dynamic, tarcie 0 (Minimum) | |

---

## 7. Game feel (`Feel`, `TimeDirector`)

| Zdarzenie | Hit stop | Time dilation | Impuls | Rumble L / H | Kamera | Obraz |
|---|---|---|---|---|---|---|
| Cios zrywem | 55–110 ms (power²) | – | Hit 0.10–0.22 | napastnik 0.15–0.3 / 0.45–0.75, ofiara 0.35–0.65 / 0.35–0.55 | najazd 6–12%, FOV −1.2…−2.2° | CA + beczka 0.3–0.75 |
| Cios z otoczenia | 30–70 ms | – | Slam 0.08–0.25 | 0.1–0.55 / 0–0.25 | FOV −1° | 0.25 |
| Lądowanie ≥ 9 m/s | – | – | Land 0.03–0.12 | 0.15–0.4 / 0.05 | – | – |
| Wybuch | 40–80 ms | > 0.5: 0.45× (0.03 / 0.12 / 0.35 s) | Explosion 0.3–0.6 | wszyscy w 18 m: do 1.1 / 0.5 | najazd do 8% | błysk +0.7 EV |
| Nokaut | 90 ms | **0.25×** (0.05 / 0.35 / 0.45 s) | Hit 0.25 | 0.6 / 0.8 | najazd 22% na 0.9 s | 0.6 |
| Gol / meta | – | 0.35× (0.06 / 0.45 / 0.5 s) | Explosion 0.2 | wszyscy 0.4 / 0.4 | najazd 15% na 1 s | błysk |

**TimeDirector**: skala hit-stopu 0.02 (nie 0 – cząsteczki pełzną), łańcuch trafień w oknie 0.5 s dostaje
0.65ⁿ czasu, twardy limit 0.22 s hit-stopu na sekundę. Wynik = pauza ? 0 : min(hit stop, dilation).
Dźwięk: pitch = skala^0.35 (min 0.55), w hit-stopie bez zmian.

### Squash & stretch (`PotatoBody`)

| Warstwa | Wartości |
|---|---|
| Pion (sprężyna) | 5 Hz, ζ 0.32; lądowanie 0.022 × prędkość upadku (0.06–0.38); wybicie −0.22; lot ±0.012 × vy |
| Oś dowolna (galaretka) | 4.2 Hz, ζ 0.26; zryw: antycypacja +0.20, wystrzał −0.30; cios 0.12–0.38 |
| Objętość | boki = 1/√oś (ziemniak nie „chudnie”) |
| Przechył | 0.26°/(m/s²) do przodu (max 20°), 0.2°/(m/s²) na boki (max 15°), sprężyna 3.4 Hz ζ 0.55; zryw 26°, zawracanie −16° |
| Drżenie ofiary | 55 Hz, 0.08 m, 0.14 s, w czasie rzeczywistym (gra w hit-stopie) |

---

## 8. Zgodność z minigrami

Publiczne API `Potato` jest takie samo (`Knock`, `Teleport`, `FaceTowards`, `SetHidden`, `Grip`, `Push`,
`SpeedMultiplier`, `CanDash`, `IsDashing`, `Grounded`…). `GM.Camera.Shake(x)` działa dalej (impuls).
`Fx.Explosion` sam odpala `Feel.Explosion`, a `Minigame.Eliminate` – `Feel.Knockout`.

---

## 9. VFX Graph – schematy grafów

Wszystkie grafy: **Assets/Resources/PotatoParty/VFX/**, nazwy dokładnie jak niżej. Kod wysyła zdarzenie
**`Burst`** z atrybutami: `position`, `direction`, `color` (Vector3), `size`, `spawnCount`.

Wspólne ustawienia każdego systemu:
- Event context: nazwa `Burst`, podłączony **bezpośrednio do Initialize Particle** (wtedy `spawnCount`
  z atrybutu zdarzenia ustala liczbę cząsteczek).
- Initialize → Space **World**, Bounds Setting Mode **Manual**, Center (0, 0, 0), Size (300, 80, 300).
- W Initialize: `Set Position` ← `Get Attribute: position (Source)`, `Set Color` ← `color (Source)` itd.
- Każdy system z tego samego zdarzenia dostaje `spawnCount` cząsteczek. Systemy, które mają mieć ich mniej
  (np. jedna gwiazda), dostają w Initialize blok **Set Alive** z warunkiem na `spawnIndex` – nadmiar ginie
  w tej samej klatce i nic nie kosztuje.
- Outputy URP: Blend **Additive** dla świateł/iskier, **Alpha** dla dymu/kurzu, Soft Particle ✔ (fade 0.4).

### PP_Hit – trafienie zrywem

| System | Initialize | Update | Output |
|---|---|---|---|
| **Gwiazda** (cap 64; `Set Alive` ← spawnIndex == 0, czyli 1 szt.) | Lifetime 0.16–0.22, Size = 1.6 × size(src), Color = (2.4, 2.2, 1.9) HDR, Angle Z random 0–180° | – | Quad Additive, tekstura gwiazdy 4-ramiennej, Size over Life: 0.35 → 1 (t 0.25) → 0.15 |
| **Iskry** (cap 4096) | Lifetime 0.18–0.42, Velocity = **Direction(src) w stożku 28°** × 7–16 × size, +0.25 w górę, Size 0.05–0.12, Color = color(src) × 3 HDR | Gravity (0, −8.8, 0), Linear Drag 4, Collide with Plane y = 0, Bounce 0.3 | Quad Additive, **Orient: Along Velocity**, Scale.y = 2.2 + 0.045 × |v| |
| **Fala** (cap 64; `Set Alive` ← spawnIndex == 0) | Lifetime 0.2, Size = 1.4–2.6 × size, Color = color × 1.8, Orient: Fixed Axis = Direction(src) | – | Quad Additive, tekstura pierścienia, Size over Life 0.2 → 0.85 → 1, Alpha 1 → 0 |
| **Kurz** (cap 512) | Position = src − (0, 0.85, 0) + Sphere r 0.3, Velocity radial 1.5–3, Lifetime 0.5–0.9 | Drag 3, Turbulence 0.5 / 1.5 Hz | Quad Lit Alpha, Size over Life 1 → 1.8 |

### PP_Dust – lądowanie, skok, skid

| System | Initialize | Update | Output |
|---|---|---|---|
| **Kłęby** (cap 4096) | Position = src + Sphere r 0.3 × size (Y × 0.2), Velocity = radialnie 1.5–3.5 × size + (0, 0.5, 0) + Direction(src) × 1, Lifetime 0.6–1.1, Size 0.35–0.7 × size, Color = color(src), Alpha 0.7 | Linear Drag 2.5, Turbulence Intensity 0.6, Frequency 1.2, Size over Life 1 → 1.8, Alpha over Life 0.7 → 0 | **Quad Lit** (URP Lit Output), Normal Bending 0.6, Camera Fade 0.5–1.5 m, Sort: Camera Distance |
| **Grudki** (cap 2048; `Set Alive` ← Random < 0.4) | Velocity: półsfera 3–6, Lifetime 0.4–0.7, Size 0.04–0.08, Color = color × 0.6 | Gravity −18, Collide Plane y = src.y, Bounce 0.2, Friction 0.6 | Mesh Output (Cube), Lit |

### PP_DashTrail – smuga zrywu

| System | Initialize | Update | Output |
|---|---|---|---|
| **Linie pędu** (cap 2048) | Position = src + Box (0.5, 0.6, 0.5), Velocity = Direction(src) × 2–4, Lifetime 0.18–0.28, Color = color(src) × 1.6 HDR | Alpha over Life 1 → 0 | Quad Additive, Orient Along Velocity, Scale (0.12, 1.3) |
| **Duch** (cap 256; `Set Alive` ← spawnIndex == 0) | Position = src, Size 1.6, Color = color(src) × 0.8, Alpha 0.35, Lifetime 0.15 | Size over Life 1 → 1.25 | Quad Additive, tekstura miękkiego koła |

### PP_Explosion – wybuch

| System | Initialize | Update | Output |
|---|---|---|---|
| **Kula ognia** (cap 512; `Set Alive` ← spawnIndex < 12) | Position = src + Sphere r 0.5 × size, Velocity radial 2–5 × size, Lifetime 0.35–0.55, Size 1.2–2.2 × size, Color gradient po czasie: (8, 4, 1.2) HDR → (2, 0.6, 0.15) → (0.1, 0.08, 0.07) | Drag 4, Turbulence 1.5 / 0.8 Hz | Quad Additive → po 40% życia Alpha Blend (dwa outputy), Size over Life 0.6 → 1.4 |
| **Dym** (cap 1024; `Set Alive` ← spawnIndex < 24) | Velocity (0, 3–6, 0) + radial 1–3, Lifetime 1.6–2.6, Size 1.5–3 × size, Color (0.18, 0.16, 0.15), Alpha 0.8 | Drag 1.5, Turbulence 0.8 / 0.5 Hz, Force (0, 1.5, 0) | Quad Lit Alpha, Size over Life 1 → 2.5, Soft Particle 0.6 |
| **Iskry** (cap 4096; wszystkie z `spawnCount`) | Velocity: sfera 8–18 × size, Lifetime 0.6–1.2, Color (6, 2.4, 0.6) | Gravity −9.8, Drag 1.2, Collide Plane | Quad Additive Along Velocity |
| **Odłamki** (cap 512; `Set Alive` ← spawnIndex < 16) | Velocity: półsfera 6–11, Angular Velocity random ±720°/s, Size 0.12–0.25 | Gravity −20, Collide Plane, Bounce 0.35 | Mesh Output (Cube), Lit, kolor ziemi |
| **Fala uderzeniowa** (`Set Alive` ← spawnIndex == 0) | Position = src z y = 0.05, Orient Fixed Axis (0, 1, 0), Lifetime 0.45, Size 0.5 → 9 × size | – | Quad Additive pierścień, Alpha 1 → 0 |

Dodatkowo kod zapala krótkie światło punktowe (`Fx.FlashLight`) – VFX Graph świateł nie emituje.

### PP_Splash – woda / błoto / farba

| System | Initialize | Update | Output |
|---|---|---|---|
| **Krople** (cap 4096) | Velocity: stożek 18° w górę × 5–10 × size, Lifetime 0.7–1.2, Size 0.15–0.4 × size, Color = color(src), Alpha 0.95 | Gravity −18, Collide Plane y = src.y, **Kill on Collision** | Quad Alpha, Along Velocity, Scale.y 1.6, Smoothness w Lit 0.9 |
| **Korona** (cap 512; `Set Alive` ← spawnIndex < 20) | Position = src + Circle r 0.4, Velocity = radial 2–3 + (0, 4, 0) | Gravity −12 | Quad Lit Alpha |
| **Pierścień** (`Set Alive` ← spawnIndex == 0) | Orient Fixed Axis (0, 1, 0), Size 0.5 → 4 × size, Lifetime 0.7 | – | Quad Alpha, biel 0.8 → 0 |
| **Mgiełka** (cap 256; `Set Alive` ← spawnIndex < 8) | Velocity (0, 1–2, 0), Lifetime 1.2, Size 1–2, Color = color(src) + 75% bieli, Alpha 0.35 | Drag 2 | Quad Lit Alpha |

Po zbudowaniu grafu wystarczy Play – `Vfx` sam go znajdzie i przestanie używać zapasowych cząsteczek.

---

## 10. Czego nie dało się sprawdzić z chmury

- Projekt nie był uruchomiony w Unity. C# skompilowano na atrapach API (UnityEngine 2021.3 + własne atrapy
  URP 17 i Cinemachine 3.1), HLSL sprawdzono kompilatorem `glslangValidator` z atrapami nagłówków URP.
- Najbardziej prawdopodobne miejsca na drobne błędy przy pierwszym otwarciu: nazwy pól Cinemachine 3
  (`CinemachineImpulseDefinition.ImpulseShapes`, `CinemachineBrain.UpdateMethods`), wersje pakietów w manifeście.
  Jeśli konsola coś pokaże, wklej mi błąd – poprawka to zwykle jedna linijka.

## Strojenie v2 – "diametralnie" (2026-11-10)

Po pierwszym teście wszystkie gałki przesunięte w stronę kina. Każdą można cofnąć w kodzie (pola publiczne).

| Gdzie | Pole | Było | Jest |
|---|---|---|---|
| CinematicCamera | PitchNear / PitchFar | 34° / 52° | **20° / 38°** |
| CinematicCamera | MinDistance / MaxDistance | 9 / 30 m | **6 / 22 m** |
| CinematicCamera | FrameMargin | 2.2 m | **1.3 m** |
| CinematicCamera | BaseFov / ShowcaseFov / ShowcasePitch | 38 / 30 / 22° | **34 / 26 / 12°** |
| GameManager | MinDistance menu / lobby / gra / podium | 13 / 11 / 9 / 10 | **9 / 7.5 / 6 / 7** |
| PostFx | MoodExposure / MoodContrast / MoodSaturation | – | **−0.45 / +22 / −14** (dodawane do gradacji mapy) |
| PostFx | Vignette intensity / smoothness / kolor | 0.22 / 0.45 / #140F33 | **0.42 / 0.38 / #05040C** |
| PostFx | Split toning cienie / światła / balans | fiolet / beż / 8 | **teal (0.22,0.42,0.58) / pomarańcz (0.78,0.52,0.30) / −12** |
| PostFx | Bloom threshold / intensity | 1.0 / 0.32 | **0.9 / 0.55 (+0.2 na mapie)** |
| PostFx | DoF: ogniskowa / maks. rozmycie graczy / min. przysłona | 85 mm / 1.2 px / f1.8 | **70 mm / 2.5 px / f1.2** |
| PostFx | Film grain | 0.12 | **0.22** |
| Lighting | Słońce: wysokość / mnożnik / cień | 42° / 1.45 / 0.9 | **30° / 1.7 / 1.0** |
| Lighting | Ambient niebo / horyzont / ziemia | ×0.85 / 0.6 / 0.4 | **×0.5 / 0.34 / 0.2** |
| Lighting | Mgła start / koniec | 38 / 160 m | **18 / 95 m** |
| Impulses | Strength (globalny mnożnik wstrząsów) | 1.0 | **1.7** |
| Feel | Hit stop zrywu / otoczenia | 55–110 / 30–70 ms | **80–160 / 50–100 ms** |
| Feel | Knockout slow-mo | 0.25× | **0.15×** |
| TimeDirector | MaxStopPerSecond | 0.22 s | **0.3 s** |

## Zryw v3 – bez spowolnienia, ze strzępkami (2026-11-10, po playteście)

- `Feel.DashHit`: **usunięty hit stop** (było 80–160 ms). Waga ciosu = `PotatoShreds.Burst` + impuls kamery + FOV punch + rumble + rozcięcie skórki (`PotatoSurface.Impact`).
- `MotorTuning`: DashStartup 0.035 → **0** (wystrzał w tej samej klatce), DashActive 0.16 → **0.15**, DashRecovery 0.14 → **0.1** (sterowanie 60%), DashCooldown 0.85 → **0.7**; `Potato.DashSpeed` 17.5 → **21 m/s**.
- `VFX/PotatoShreds.cs`: 3 trwałe ParticleSystem (miąższ 900, skórka 400, okruchy 700 cząstek), emisja ręczna `Emit(EmitParams)`, siatki proceduralne, kolizja ze światem (bounce 0.32/0.2/0.4, dampen 0.28/0.12/0.35), gravity ×1.7/1.25/1.4. Liczba kawałków: miąższ 10–26, skórka 4–12, okruchy 14–40 (power²). Stożek 55°, prędkość 3.5–10 m/s, w górę 2.5–6.5 m/s. `PotatoShreds.Amount` = globalny mnożnik.

## Strojenie v3 – po playteście (2026-11-10)

| Gdzie | Pole | v2 | v3 |
|---|---|---|---|
| CinematicCamera | PitchNear / PitchFar | 20° / 38° | **38° / 54°** |
| CinematicCamera | MinDistance / MaxDistance / FrameMargin | 6 / 22 / 1.3 m | **13 / 34 / 2.6 m** |
| CinematicCamera | FrameHumansOnly | (zawsze tylko ludzie) | **false – kadr obejmuje wszystkich** |
| GameManager | MinDistance menu / lobby / gra / podium | 9 / 7.5 / 6 / 7 | **15 / 13 / 13 / 11**, MinSpread gry 8 m |
| PostFx | BaseExposure / MoodExposure | −0.15 / −0.45 | **−0.35 / −0.7** (Lód −0.2, Plaża +0.05 przed nastrojem) |
| PostFx | kontrast / nasycenie (bazowe, nastrój) | 34, 2 / +22, −14 | **28, 20 / +16, +4** (głęboko i soczyście) |
| PostFx | Bloom threshold / intensity | 0.9 / 0.55 | **1.15 / 0.3**, mapy ×0.7 |
| PostFx | Winieta / ziarno / DoF rozmycie graczy | 0.42 / 0.22 / 2.5 px | **0.32 / 0.14 / 1.0 px**, 85 mm |
| Lighting | Słońce wysokość / mnożnik | 30° / 1.7 | **38° / 1.35** |
| SSAO (ProjectSetup v8) | Intensity / Radius / Direct / Falloff | 1.0 / 0.4 / 0.25 / – | **2.2 / 0.55 / 0.45 / 60 m**, nadpisywane przy każdym starcie edytora |
| Ziemniak | bryła | sfera Unity | **PotatoMesh**: 56×36, guzy 0.075+0.022, asymetria 0.09, wygięcie 0.06, 6–9 oczek (głęb. 0.04, r 0.11) |
| Ziemniak | kolor domyślnej skórki | kolor gracza | **płowy brąz (0.78, 0.6, 0.38) + 22% koloru gracza** |
| Ziemniak | shader ciała | – | Speckle ≥ 0.85, Lenticel 0.75, Net 0.4, Bump 0.065; kurz nigdy < 0.22 |

## Menu główne v2 (2026-11-10)

- **Scena 3D** (`UI/MenuDirector.cs`): plac z garderobą budowany raz dla menu i lobby (`GameManager.menuStage`). 6 ziemniaków na ringu `BrawlCenter (0,0,−6)`, r 3.2 m, mózg `Brawl` (cel co 1.8–3.6 s, zryw z 1.6–3 m przy celności 0.8). 2 reflektory Spot (ciepły #FF9E47, fiolet #8C66FF), 14±4, kąt 34°/18°, ósemki nad ringiem, co 7 s łapią bohatera. DoF: `PostFx.FocusTarget` = bohater (105 mm, f1.4).
- **Ujęcia Cinemachine**: 3 `CinemachineCamera` (Tytuł FOV 30, Graj FOV 34, Ustawienia FOV 40) + `ShotDrift` (dolly 14 s, "z ręki" 0.04 m / 0.35°) + `CinemachineImpulseListener` (gain 0.6). Przełączanie = aktywacja ostatniej kamery; blend EaseInOut 1.25 s. Menu → lobby: `Release()` oddaje kadr kamerze gry blendem, bez przebudowy świata.
- **Juicy UI** (`UI/Juice.cs`): `JuicyButton` (sprężyny skali 7 Hz ζ 0.32, hover 1.10, press 0.90/1.06, kick przy kliknięciu, obrót ±6°, połysk 0.38 s, dźwięk Tick w gamie pentatonicznej, 6 iskier przy hover / 18 strzępków + pierścień przy kliknięciu), `UISparks` (pula 160 obrazków, grawitacja 1500 px/s²), `UIStagger` (kaskada 60 ms, ease-out-back 2.2), `BounceTitle` (litery na sprężynach 4.2 Hz, klik podrzuca), `Ease`.

## v4 – nowe mini-gry i mapa "Ogród Babci"

Usunięte w całości: Gorący Ziemniak, Złote Ziemniaki, Sumo, Pękający Lód, Tor Przeszkód, Wirująca Belka, Spadające Garnki, Ziemniaczana Piłka, Malarze, Król Wzgórza, Kombajn, Labirynt (12 plików). `Minigame.Count = 4`.

**Mapa `Maps/GardenMap.cs`** – 56 × 56 m, 5 poziomów: grządka −1.5 m, trawnik 0, tarasy 2.5 m, dach szopy 6 m, zbiornik 9 m. Interakcje: 2 trampoliny (kompost → dach, dach → zbiornik), 4 grabie (ogłuszenie 1.2 s), 2 zraszacze (pchnięcie 6 m/s, rewers co 8 s), szyby szklarni (3 zrywy), most na dźwignię, taczki, miękka ziemia.

| Gra | Rdzeń | Liczby |
|---|---|---|
| Wielkie Smażenie | zryw odrywa masę → strzępki → wiaderko → frytkownica | masa 0.3–1.4, cios −6…14%, wrzut 0.6 s / r 3.4 m, 10+ = ×1.5, prychnięcie co 30 s (r 4.2, wysypuje ½) |
| Chybotliwa Patelnia | moment sił przechyla patelnię r 6.5 m nad ogniem | gain 0.16, sztywność 6, tłumienie 2.2, max 24°, katapulta od 2.6 m/s krawędzi, olej co 20 s (grip 0.35 / 5 s), klops ×3 masy, 3 życia |
| Kretowisko | asymetria: krety pod ziemią vs ogrodnicy | wyskok r 1.4 m (+3/os.), tupnięcie ≥ 6 m/s w r 1.6 m (+2, kret −2), kopczyk > 4.5 m/s, role co 45 s ×3 |
| Wieża Ziemniaków | żywe wieże jako odwrócone wahadło | niestabilność 1.4 + 0.45/piętro, gałka jeźdźca 4 rad/s², upadek > 35°, podstawa −15%/piętro, 1/2/4 pkt/s, stół ×2 |

Nowe API: `Potato.KnockMultiplier`, `Potato.ActionPressed`, `Potato.Landed` (statyczne – odpinane w `Cleanup`), `PotatoShreds.Chunk(i)`, kinematyczne ciała ignorują knock.
