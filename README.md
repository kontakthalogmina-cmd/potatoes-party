# Potatoes Party 🥔🎉

Wesoła gra imprezowa dla **2–6 graczy** na jednym ekranie (Unity 6, URP).
Ziemniaczki rywalizują w minigrach, a filmowa kamera (Cinemachine) trzyma się blisko akcji, najeżdża na trafienia i reaguje na prędkość.

> **Rework „Cinematic Realism + Ultimate Responsiveness”** – nowa kamera, światło, post-processing, shader skóry
> z SSS i reakcją na brud, wodę i uszkodzenia, sterowanie od zera i pełny game feel (hit stop, time dilation,
> impulsy, wibracje, squash & stretch, VFX Graph). Wszystkie wartości do Inspektora: **[docs/Rework.md](docs/Rework.md)**.

## Jak uruchomić

1. W Unity Hub kliknij **Add → Add project from disk** i wskaż ten folder.
   Projekt jest przygotowany pod **Unity 6000.6.7f1** (inne wersje Unity 6 też powinny działać).
2. Przy pierwszym otwarciu poczekaj, aż Unity zaimportuje pakiety (kilka minut).
3. Projekt sam się konfiguruje: tworzy scenę `Assets/Scenes/PotatoesParty.unity`, włącza URP i dodaje scenę do Build Settings.
   Gdyby coś nie zadziałało, użyj menu **Potatoes Party → Napraw ustawienia projektu**.
4. Unity doinstaluje **Cinemachine 3, VFX Graph i Input System** i poprosi o restart (Input System: tryb „Both”) – zrestartuj.
5. Wciśnij **Play**. Cała gra (grafika, interfejs, muzyka) buduje się z kodu, więc działa w każdej scenie.
6. Opcjonalnie: zbuduj 5 grafów VFX według [docs/Rework.md](docs/Rework.md#9-vfx-graph--schematy-grafów). Do tego czasu efekty robią cząsteczki zapasowe.

## Sterowanie

| Kto | Ruch | Skok | Akcja (zryw / pchnięcie) |
|---|---|---|---|
| Klawiatura 1 | WASD | Spacja | Lewy Shift lub E |
| Klawiatura 2 | Strzałki | Prawy Ctrl | Prawy Shift lub Enter |
| Pad (dowolna liczba) | Lewa gałka / krzyżak | A | X, B lub prawy spust |

Przytrzymaj skok, żeby skoczyć wyżej (krótkie wciśnięcie = niski skok). Zryw działa też raz w powietrzu.
Pady wibrują przy trafieniach, lądowaniach i wybuchach.

ESC, P albo Start na padzie: pauza. W lobby dołączasz, wciskając skok lub akcję na swoim urządzeniu. Brakuje ludzi? Dodaj boty.

## Co jest w grze

- **Menu główne:** Dołącz do gry, Stwórz grę, Ustawienia, Wyjdź z gry.
- **Lobby:** nazwa, lobby **otwarte lub prywatne** (z kodem), **tryb gry**, do 6 graczy (ludzie + boty). Po scenie lobby można już biegać.
- **Dwa tryby:**
  - *Szybka impreza* — 5 losowych minigier z 12,
  - *Wielka impreza* — 10 losowych minigier z 12.
  Minigry w meczu się nie powtarzają.
- **Ekran ładowania** przed każdą minigrą (nazwa, mapa, pasek postępu, porada).
- **12 minigier, każda z własną mapą:**
  - *Gorący Ziemniak* (Ziemniaczana Farma) — przekaż bombę dotykiem, zanim wybuchnie.
  - *Złote Ziemniaki* (Sad) — zbieraj złoto, zrywem wybijasz je rywalom.
  - *Ziemniaczane Sumo* (tratwa na morzu) — zepchnij innych z kurczącej się platformy.
  - *Pękający Lód* (Lodowa Kraina) — płyty pękają pod stopami.
  - *Tor Przeszkód* (Kuchnia) — 6 odcinków: fałszywe drzwi, kruszące się kamienie, wałki, kładka z wahadłami, taśmociąg i toczące się kule.
  - *Obrotowa Belka* (Wulkan) — przeskakuj kręcącą się belkę, coraz szybszą.
  - *Deszcz Garnków* (Stołówka) — uciekaj z czerwonych kół, gdzie spadają garnki.
  - *Ziemniaczana Piłka* (Stadion) — drużyny Czerwoni i Niebiescy, wbij gola.
  - *Malarze* (Plac zabaw) — maluj pola na swój kolor, najwięcej pól wygrywa.
  - *Król Polany* (Las) — stój sam w złotym kręgu, który co chwilę się przenosi.
  - *Ucieczka przed Kombajnem* (Pole kukurydzy) — kombajn goni najbliższego.
  - *Labirynt w Ogrodzie* — za każdym razem inny labirynt, pierwszy przy wyjściu wygrywa.
- **Wygląd:** jasny, słoneczny i schludny: niebo z gradientem, miękkie cienie, lekki bloom, woda pod areną, chmurki i kilka balonów w tle. Czcionki: Titan One (tytuły, przyciski) i Varela Round (zwykły tekst), licencja OFL.
- **Styl „Aksamitna Bulwa”:** ziemniaki mają własny shader (`Assets/Shaders/PotatoHero`): aksamitny meszek na krawędziach, ciepły miąższ prześwitujący pod światło (fake SSS), kontur, który zawsze odcina postać od tła, oraz błoto, szron i przypalenia na skórce sterowane z minigier. Opis stylu i logiki shadera: [`docs/ArtDirection.md`](docs/ArtDirection.md).
- **Ziemniaczki z charakterem:** gładkie, błyszczące ciało, wielkie oczy, mruganie, miny (przerażone „O” przy upadku), rączki w rękawiczkach, tupiące nóżki, kiełek na czubku i pierścień w kolorze gracza pod stopami.
- **9 skórek do wyboru:** Klasyk, Rumiana Bulwa, Vitelotte (paski), Krówka (łaty), Złota Bulwa (metal z brokatem), Magmowa (świecące pęknięcia), Kosmiczna (gwiazdy i mgławice), Ninja i Szronik, każda z własnym dodatkiem (kapelusz, czapka, okulary, rogi, pierścień planety, nauszniki…). Skórkę zmieniasz w lobby strzałkami `< nazwa >` przy graczu albo wbiegając na podest w **Garderobie** na placu. Wybór zapamiętuje się dla danego koloru gracza, a boty losują wolne skórki.
- **Skórka reaguje na świat:** błoto, piasek lub mąka na stopach po lądowaniu (kolor zależy od mapy), plamy po zderzeniach, szron na lodzie, przypieczone stopy na wulkanie, zwęglenie w lawie, kąpiel i zmycie brudu po wpadnięciu do wody, farba w *Malarzach*, zupa w *Deszczu Garnków*, słoma od kombajnu.
- **Efekty:** cząsteczki z proceduralnymi teksturami: wybuchy z falą uderzeniową i błyskiem światła, iskry, plusk wody, kurz w kolorze podłoża, gwiazdki trafienia, szron, żar, konfetti. Każda mapa ma żywe tło: śnieg, popiół i żar, świetliki, spadające liście i płatki, pyłki, refleksy na wodzie.
- **Duża plansza:** menu, lobby i podium dzieją się na dużym placu imprezowym (mozaika, girlandy żarówek, latarnie, fontanna, rabaty kwiatów, Garderoba z manekinami). Mapy mają gęstą trawę, kwiaty, kamienie i zaspy, a na horyzoncie wzgórza, szczyty lub dymiący wulkan.
- **Światło i obraz:** trzy światła (słońce, wypełnienie, kontra), 4 kaskady cieni, SSAO, sonda odbić, osobny grading kolorów dla każdej mapy (bloom, white balance, split toning, winieta), rozmycie tła w menu.
- **Interfejs:** białe karty, granatowy tekst, wypukłe kolorowe przyciski i sprężyste animacje okienek. W grze: nazwa minigry u góry, karty graczy na dole i okrągły licznik czasu w rogu.
- **Więcej zabawy:** po mocnym uderzeniu ziemniak robi fikołka i widzi gwiazdki, lider nosi koronę, a w każdej minigrze jest jakaś niespodzianka (fałszywe złoto, fałszywe drzwi, gumowe kaczki, zawracająca belka, kombajn, który zmienia ofiarę…).
- **Finał:** ostatnia minigra meczu daje podwójne punkty.
- **Punktacja:** 3 / 2 / 1 punkt za miejsca 1–3 w każdej minigrze, na końcu podium i zwycięzca.
- **Muzyka i dźwięki:** radosne melodie i efekty generowane w grze (bez plików audio).
- **Ustawienia:** głośność ogólna, muzyki i efektów, pełny ekran/okno, jakość grafiki.

## Gra online

„Dołącz do gry” i lobby prywatne/otwarte działają teraz **lokalnie** (jeden komputer, pady + klawiatury, otwarte pokoje prowadzone przez boty).
Kod jest przygotowany na tryb online: wystarczy dopisać implementację `ILobbyService` (np. Unity Lobby + Relay + Netcode for GameObjects).

## Struktura

```
Assets/Scripts/Runtime/Core       – GameManager (przebieg gry), dane, sterowanie, lobby
Assets/Scripts/Runtime/Gameplay   – ziemniak (Potato: fasada, PotatoMotor: ruch, PotatoBody: animacja, PotatoSurface: skóra), skórki, efekty
Assets/Scripts/Runtime/Feel       – game feel: TimeDirector (hit stop, time dilation), Impulses, Rumble, Feel
Assets/Scripts/Runtime/CameraRig  – kamera filmowa Cinemachine 3 (CinematicCamera, CameraDynamics)
Assets/Scripts/Runtime/Rendering  – oświetlenie (Lighting) i post-processing (PostFx)
Assets/Scripts/Runtime/VFX        – efekty VFX Graph (Vfx) z zapasem na ParticleSystem
Assets/Shaders/PotatoHero         – shader skóry ziemniaka v2 (SSS, kurz, błoto, woda, uszkodzenia)
Assets/Scripts/Runtime/Minigames  – 12 minigier i ich wspólna baza
Assets/Scripts/Runtime/Maps       – 12 map (scenerii), plac imprezowy z Garderobą (Hub), roślinność i horyzont (Scatter)
Assets/Scripts/Runtime/UI         – menu, lobby, HUD, ekran ładowania
Assets/Scripts/Runtime/Audio      – syntezator muzyki i efektów
Assets/Scripts/Editor             – automatyczna konfiguracja projektu
```
