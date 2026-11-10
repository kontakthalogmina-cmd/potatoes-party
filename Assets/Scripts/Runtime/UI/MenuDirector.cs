using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace PotatoParty
{
    public enum MenuShot { Title, Play, Settings }

    /// <summary>
    /// Reżyser menu: żywa scena 3D za interfejsem i płynne najazdy kamery między ekranami.
    /// <para><b>Kamery:</b> każdy ekran menu ma własną <see cref="CinemachineCamera"/> (bez Body/Aim – pozycję
    /// prowadzi <see cref="ShotDrift"/>: wolny dolly + "z ręki"). Wszystkie mają ten sam priorytet, więc żywa jest
    /// ta ostatnio włączona; przełączenie = wyłącz pozostałe, włącz docelową → CinemachineBrain miesza je krzywą
    /// EaseInOut (<see cref="BlendTime"/>). Wyjście z menu = wyłączenie wszystkich → wraca kamera rozgrywki, też blendem.
    /// Żadnego przeładowania sceny.</para>
    /// <para><b>Scena:</b> 6 ziemniaków przepycha się zrywem przed kamerą (strzępki, wstrząsy), 2 kolorowe reflektory
    /// sceniczne krążą nad nimi, ostrość (Bokeh DoF) trzyma bohatera na pierwszym planie.</para>
    /// </summary>
    public class MenuDirector : MonoBehaviour
    {
        // ---------------- strojenie ----------------
        public static float BlendTime = 1.25f;
        public static Vector3 BrawlCenter = new Vector3(0f, 0f, -6f);
        public static float BrawlRadius = 3.2f;

        struct ShotDef { public Vector3 Pos, Aim; public float Fov; public Vector3 Dolly; }

        static readonly ShotDef[] Shots =
        {
            // Tytuł: nisko, z lewej; bohaterowie w prawej tercji, menu po lewej
            new ShotDef { Pos = new Vector3(-2.6f, 1.45f, -14.2f), Aim = new Vector3(-2.2f, 0.9f, -6f), Fov = 30f, Dolly = new Vector3(0.6f, 0.1f, 0.5f) },
            // Graj / Dołącz: kamera obiega bójkę i wchodzi bliżej, z drugiej strony
            new ShotDef { Pos = new Vector3(5.2f, 1.1f, -10.5f), Aim = new Vector3(0.2f, 0.8f, -5.8f), Fov = 34f, Dolly = new Vector3(-0.5f, 0.05f, 0.4f) },
            // Ustawienia: wysoko, z góry na plac i fontannę – "spokojny" kadr pod panel
            new ShotDef { Pos = new Vector3(-6.5f, 8.5f, -15f), Aim = new Vector3(0f, 0.5f, -1f), Fov = 40f, Dolly = new Vector3(0.8f, 0f, 0f) },
        };

        public static MenuDirector Instance { get; private set; }

        readonly CinemachineCamera[] cams = new CinemachineCamera[3];
        readonly List<Potato> brawlers = new List<Potato>();
        readonly Dictionary<Potato, Potato> targets = new Dictionary<Potato, Potato>();
        readonly Dictionary<Potato, float> retarget = new Dictionary<Potato, float>();
        Light[] stageLights;
        public MenuShot Current { get; private set; }
        public bool Active { get; private set; }
        public Potato Hero { get; private set; }

        public static MenuDirector Ensure(Transform parent)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("Rezyser menu");
            go.transform.SetParent(parent, false);
            Instance = go.AddComponent<MenuDirector>();
            Instance.Build();
            return Instance;
        }

        void Build()
        {
            for (int i = 0; i < Shots.Length; i++)
            {
                var go = new GameObject("Ujecie menu: " + (MenuShot)i);
                go.transform.SetParent(transform, false);
                go.SetActive(false);
                var cam = go.AddComponent<CinemachineCamera>();
                var lens = cam.Lens;
                lens.FieldOfView = Shots[i].Fov;
                lens.NearClipPlane = 0.2f;
                lens.FarClipPlane = 400f;
                cam.Lens = lens;
                var drift = go.AddComponent<ShotDrift>();
                drift.Base = Shots[i].Pos;
                drift.Aim = Shots[i].Aim;
                drift.Dolly = Shots[i].Dolly;
                drift.Apply(0f);
                // wstrząsy z bójki ziemniaków działają też w menu (słabiej niż w grze)
                var listener = go.AddComponent<CinemachineImpulseListener>();
                listener.Gain = 0.6f;
                listener.Use2DDistance = true;
                listener.UseCameraSpace = true;
                listener.ChannelMask = 1;
                cams[i] = cam;
            }

            // dwa reflektory sceniczne nad bójką (ciepły i fioletowy), krążą i "szukają" ziemniaków
            stageLights = new Light[2];
            Color[] cols = { new Color(1f, 0.62f, 0.28f), new Color(0.55f, 0.4f, 1f) };
            for (int i = 0; i < 2; i++)
            {
                var lgo = new GameObject("Reflektor " + (i + 1));
                lgo.transform.SetParent(transform, false);
                var l = lgo.AddComponent<Light>();
                l.type = LightType.Spot;
                l.color = cols[i];
                l.intensity = 0f;
                l.range = 22f;
                l.spotAngle = 34f;
                l.innerSpotAngle = 18f;
                l.shadows = i == 0 ? LightShadows.Soft : LightShadows.None;
                stageLights[i] = l;
            }
        }

        // ---------------- API dla GameManager / MenuUI ----------------

        /// <summary>Wejście do menu: bohaterowie dostają mózg "bójki", kamera na ujęcie tytułowe.</summary>
        public void Enter(List<Potato> potatoes, bool instant)
        {
            Active = true;
            brawlers.Clear();
            targets.Clear();
            retarget.Clear();
            brawlers.AddRange(potatoes);
            Hero = brawlers.Count > 0 ? brawlers[0] : null;
            foreach (var p in brawlers) if (p != null) p.BotBrain = Brawl;
            PostFx.FocusTarget = Hero != null ? Hero.transform : null;
            Shot(MenuShot.Title, instant);
        }

        /// <summary>Najazd na ujęcie ekranu. instant = cięcie (wejście do menu z gry).</summary>
        public void Shot(MenuShot s, bool instant = false)
        {
            Current = s;
            var brain = GameManager.Instance != null && GameManager.Instance.Camera != null ? GameManager.Instance.Camera.Brain : null;
            if (brain != null)
                brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, instant ? 0f : BlendTime);
            for (int i = 0; i < cams.Length; i++) if (i != (int)s) cams[i].gameObject.SetActive(false);
            cams[(int)s].gameObject.SetActive(true);
            if (!instant) AudioManager.PlayPitched(Sfx.Whoosh, 0.45f, 0.8f);
            PostFx.FocusTarget = s == MenuShot.Settings ? null : (Hero != null ? Hero.transform : null);
        }

        /// <summary>Wyjście z menu (lobby, mecz): kamera rozgrywki przejmuje kadr płynnym blendem.</summary>
        public void Release(bool instant = false)
        {
            Active = false;
            var brain = GameManager.Instance != null && GameManager.Instance.Camera != null ? GameManager.Instance.Camera.Brain : null;
            if (brain != null)
                brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, instant ? 0f : BlendTime);
            for (int i = 0; i < cams.Length; i++) cams[i].gameObject.SetActive(false);
            PostFx.FocusTarget = null;
            brawlers.Clear();
            foreach (var l in stageLights) if (l != null) l.intensity = 0f;
        }

        // ---------------- żywa scena ----------------

        /// <summary>Mózg bota w menu: wybierz rywala, podejdź, wyceluj zrywem; po trafieniu odskocz. Zawsze blisko kamery.</summary>
        BotCommand Brawl(Potato p)
        {
            if (!Active) return BotCommand.Stay();
            float now = Time.time;
            Potato t;
            float until;
            if (!targets.TryGetValue(p, out t) || t == null || !retarget.TryGetValue(p, out until) || now > until)
            {
                t = brawlers.Count > 1 ? brawlers[(brawlers.IndexOf(p) + Random.Range(1, brawlers.Count)) % brawlers.Count] : null;
                targets[p] = t;
                retarget[p] = now + Random.Range(1.8f, 3.6f);
            }
            var home = BrawlCenter;
            var pos = p.transform.position;
            // ucieczka znad krawędzi pola bójki (nie wyjdź z kadru)
            var fromCenter = pos - home; fromCenter.y = 0f;
            if (fromCenter.magnitude > BrawlRadius) return BotCommand.Go(home + Random.insideUnitSphere * 0.8f, false, Random.value < 0.02f);
            if (t == null) return BotCommand.Go(home, false, false);
            var to = t.transform.position - pos; to.y = 0f;
            float d = to.magnitude;
            // zryw tylko z dystansu 1.6–3 m i gdy mniej więcej patrzy na cel
            bool dash = d > 1.6f && d < 3f && p.CanDash && p.DashCooldownLeft <= 0f && Vector3.Dot(p.Facing, to / Mathf.Max(d, 0.01f)) > 0.8f && Random.value < 0.08f;
            bool hop = Random.value < 0.012f;
            return BotCommand.Go(t.transform.position, dash, hop);
        }

        void Update()
        {
            if (!Active) return;
            float tm = Time.unscaledTime;
            // reflektory: wolne ósemki nad polem bójki, "łapią" bohatera co kilka sekund
            for (int i = 0; i < stageLights.Length; i++)
            {
                var l = stageLights[i];
                if (l == null) continue;
                float ph = tm * (0.33f + i * 0.07f) + i * 2.1f;
                var spot = BrawlCenter + new Vector3(Mathf.Sin(ph) * 2.6f, 0.5f, Mathf.Sin(ph * 2f) * 1.6f);
                if (Hero != null && Mathf.Repeat(tm + i * 3f, 7f) < 1.6f) spot = Vector3.Lerp(spot, Hero.transform.position, 0.8f);
                var src = BrawlCenter + new Vector3(i == 0 ? -5f : 5f, 7.5f, -3.5f);
                l.transform.position = src;
                l.transform.rotation = Quaternion.LookRotation(spot - src);
                l.intensity = Mathf.MoveTowards(l.intensity, 14f + 4f * Mathf.Sin(tm * 3.1f + i), Time.unscaledDeltaTime * 30f);
            }
        }
    }

    /// <summary>Ruch kamery ujęcia: wolny dolly tam i z powrotem (period 14 s) + szum "z ręki" (Perlin, 0.04 m / 0.35°).</summary>
    public class ShotDrift : MonoBehaviour
    {
        public Vector3 Base, Aim, Dolly;
        public float Period = 14f, Handheld = 0.04f, HandheldRot = 0.35f;
        float seed;

        void Awake() { seed = Random.value * 100f; }

        void LateUpdate() { Apply(Time.unscaledTime); }

        public void Apply(float t)
        {
            float k = Mathf.Sin(t * Mathf.PI * 2f / Period) * 0.5f;
            var pos = Base + Dolly * k;
            var n = new Vector3(Mathf.PerlinNoise(seed, t * 0.35f) - 0.5f, Mathf.PerlinNoise(seed + 7f, t * 0.31f) - 0.5f, 0f) * 2f * Handheld;
            transform.position = pos + n;
            var rot = Quaternion.LookRotation(Aim - pos);
            float roll = (Mathf.PerlinNoise(seed + 13f, t * 0.27f) - 0.5f) * 2f * HandheldRot;
            transform.rotation = rot * Quaternion.Euler(0, 0, roll);
        }
    }
}
