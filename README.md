# Potatoes Party 🥔🎉

Wesoła gra imprezowa dla **2–6 graczy** na jednym ekranie (Unity 6, URP).
Ziemniaczki rywalizują w minigrach, a wspólna kamera sama się przesuwa i oddala tak, żeby było widać każdego gracza i otoczenie.

## Jak uruchomić

1. W Unity Hub kliknij **Add → Add project from disk** i wskaż ten folder.
   Projekt jest przygotowany pod **Unity 6000.6.7f1** (inne wersje Unity 6 też powinny działać).
2. Przy pierwszym otwarciu poczekaj, aż Unity zaimportuje pakiety (kilka minut).
3. Projekt sam się konfiguruje: tworzy scenę `Assets/Scenes/PotatoesParty.unity`, włącza URP i dodaje scenę do Build Settings.
   Gdyby coś nie zadziałało, użyj menu **Potatoes Party → Napraw ustawienia projektu**.
4. Wciśnij **Play**. Cała gra (grafika, interfejs, muzyka) buduje się z kodu, więc działa w każdej scenie.

## Sterowanie

| Kto | Ruch | Skok | Akcja (zryw / pchnięcie) |
|---|---|---|---|
| Klawiatura 1 | WASD | Spacja | Lewy Shift lub E |
| Klawiatura 2 | Strzałki | Prawy Ctrl | Prawy Shift lub Enter |
| Pad (dowolna liczba) | Lewa gałka / krzyżak | A | X, B lub prawy spust |

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
- **Ziemniaczki z charakterem:** gładkie, błyszczące ciało, wielkie oczy, mruganie, miny (przerażone „O” przy upadku), rączki w rękawiczkach, tupiące nóżki, kiełek na czubku i pierścień w kolorze gracza pod stopami.
- **Efekty:** konfetti przy wygranej, golu i na mecie, deszcz konfetti na podium, obłoczki kurzu przy skoku i lądowaniu.
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
Assets/Scripts/Runtime/Gameplay   – ziemniaczek gracza, wspólna kamera, efekty
Assets/Scripts/Runtime/Minigames  – 12 minigier i ich wspólna baza
Assets/Scripts/Runtime/Maps       – 12 map (scenerii) i ich dekoracje
Assets/Scripts/Runtime/UI         – menu, lobby, HUD, ekran ładowania
Assets/Scripts/Runtime/Audio      – syntezator muzyki i efektów
Assets/Scripts/Editor             – automatyczna konfiguracja projektu
```
