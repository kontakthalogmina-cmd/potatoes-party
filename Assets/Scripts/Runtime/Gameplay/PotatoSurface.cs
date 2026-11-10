using System.Collections.Generic;
using UnityEngine;

namespace PotatoParty
{
    /// <summary>
    /// Stan powierzchni ziemniaka → shader PotatoParty/PotatoHero v2. Trzy rodziny warstw:
    /// <list type="bullet">
    /// <item><b>Brud</b>: kurz (suchy, narasta od stóp przy bieganiu), błoto (mokre, z kierunkiem rozbryzgu).</item>
    /// <item><b>Woda</b>: film + krople + zacieki; schnie od góry (ok. 12 s z pełnego zmoczenia), zmywa kurz.</item>
    /// <item><b>Uszkodzenia</b>: wgniecenie (sprężyna wierzchołków), siniak, rozcięcie z odsłoniętym miąższem.
    ///       Kumulują się w trakcie minigry i goją się powoli – po trzecim mocnym ciosie widać, kto obrywa.</item>
    /// </list>
    /// Plus szron, zwęglenie i błysk trafienia. Każdy ziemniak ma WŁASNE kopie materiałów (nie MPB), więc
    /// zostaje w SRP Batcherze; parametry wysyłamy tylko przy zmianie wartości.
    /// </summary>
    public class PotatoSurface : MonoBehaviour
    {
        static readonly int MudAmountId = Shader.PropertyToID("_MudAmount");
        static readonly int MudDirId = Shader.PropertyToID("_MudDir");
        static readonly int MudColorId = Shader.PropertyToID("_MudColor");
        static readonly int DustAmountId = Shader.PropertyToID("_DustAmount");
        static readonly int DustColorId = Shader.PropertyToID("_DustColor");
        static readonly int WetAmountId = Shader.PropertyToID("_WetAmount");
        static readonly int FrostAmountId = Shader.PropertyToID("_FrostAmount");
        static readonly int BurnAmountId = Shader.PropertyToID("_BurnAmount");
        static readonly int BurnHeatId = Shader.PropertyToID("_BurnHeat");
        static readonly int BurnDirId = Shader.PropertyToID("_BurnDir");
        static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        static readonly int Root0Id = Shader.PropertyToID("_PP_Root0");
        static readonly int Root1Id = Shader.PropertyToID("_PP_Root1");
        static readonly int Root2Id = Shader.PropertyToID("_PP_Root2");
        static readonly int UseRootId = Shader.PropertyToID("_PP_UseRoot");
        static readonly int ImpactCenterId = Shader.PropertyToID("_ImpactCenter");
        static readonly int ImpactDirId = Shader.PropertyToID("_ImpactDir");
        static readonly int BruiseAmountId = Shader.PropertyToID("_BruiseAmount");
        static readonly int BruiseDirId = Shader.PropertyToID("_BruiseDir");
        static readonly int CutAmountId = Shader.PropertyToID("_CutAmount");
        static readonly int CutDirId = Shader.PropertyToID("_CutDir");
        static readonly int CutSeedId = Shader.PropertyToID("_CutSeed");

        /// <summary>Przestrzeń postaci: Potato.Visual (obraca się z fikołkami i zgniataniem).</summary>
        public Transform Root;

        [Range(0, 1)] public float Dust;
        [Range(0, 1)] public float Mud;
        [Range(0, 1)] public float Wet;
        [Range(0, 1)] public float Frost;
        [Range(0, 1)] public float Burn;
        [Range(0, 1)] public float Heat;
        [Range(0, 1)] public float Flash;
        [Range(0, 1)] public float Bruise;
        [Range(0, 1)] public float Cut;
        /// <summary>Poziom szronu, do którego postać dąży sama (np. 0.18 na lodowej mapie).</summary>
        [Range(0, 1)] public float FrostTarget;

        [Header("Zanikanie (na sekundę)")]
        public float MudDryDelay = 4f;
        public float MudFade = 0.05f;
        public float DustFade = 0.01f;
        /// <summary>Ziemniak nigdy nie jest idealnie czysty: kurz nie schodzi poniżej tego poziomu.</summary>
        public static float DustFloor = 0.22f;
        public float WetDry = 0.085f;       // pełne zmoczenie schnie ~12 s
        public float FrostRate = 0.25f;
        public float BurnFade = 0.03f;
        public float HeatFade = 1.2f;
        public float FlashFade = 7f;
        public float BruiseFade = 0.03f;
        public float CutHeal = 0.012f;      // rana goi się ~80 s – zostaje do końca minigry

        [Header("Uszkodzenia")]
        public float CutThreshold = 0.7f;   // siła ciosu (0..1), od której skórka pęka
        public float DentFrequency = 6.5f;
        public float DentDamping = 0.3f;
        public float BodyRadius = 0.88f;    // tułów 0.66 × skala postaci 1.3

        readonly List<Renderer> renderers = new List<Renderer>();
        readonly List<Material> materials = new List<Material>();
        Vector4 mudDir = new Vector4(0, -1, 0, 0);
        Vector4 burnDir = new Vector4(0, 0, 1, 0);
        Vector4 bruiseDir = new Vector4(0, 0, 1, 0);
        Vector4 cutDir = new Vector4(0, 0, 1, 0);
        Vector4 cutSeed = new Vector4(0, 0, 0, 1);
        Vector3 dentDirLocal = Vector3.forward;
        Spring dent;
        float mudAge;

        float sentMud = -1f, sentDust = -1f, sentWet = -1f, sentFrost = -1f, sentBurn = -1f, sentHeat = -1f, sentFlash = -1f, sentBruise = -1f, sentCut = -1f;
        Color sentMudColor = new Color(-1, -1, -1, -1), sentDustColor = new Color(-1, -1, -1, -1);
        Vector4 sentMudDir, sentBurnDir, sentBruiseDir, sentCutDir;
        bool dentWasActive = true;

        public static readonly Color DefaultMud = new Color(0.27f, 0.18f, 0.11f, 1f);
        public static readonly Color DefaultDust = new Color(0.86f, 0.78f, 0.64f, 1f);
        /// <summary>Kolor "błota": ziemia, piasek, farba, zupa, mąka – ta sama warstwa, inny kolor.</summary>
        public Color MudColor = DefaultMud;
        public Color DustColor = DefaultDust;

        public void Init(Transform root, IEnumerable<Renderer> parts)
        {
            Root = root;
            renderers.Clear();
            renderers.AddRange(parts);
            var map = new Dictionary<Material, Material>();
            for (int i = 0; i < renderers.Count; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                var src = r.sharedMaterial;
                if (src == null) continue;
                Material inst;
                if (!map.TryGetValue(src, out inst))
                {
                    inst = new Material(src) { name = src.name + " (" + name + ")" };
                    inst.SetFloat(UseRootId, 1f);
                    map[src] = inst;
                    materials.Add(inst);
                }
                r.sharedMaterial = inst;
            }
            cutSeed = new Vector4(Random.value * 10f, Random.value * 10f, Random.value * 10f, 1f);
            Push();
        }

        void OnDestroy()
        {
            for (int i = 0; i < materials.Count; i++)
                if (materials[i] != null) Destroy(materials[i]);
            materials.Clear();
        }

        // ---------------- API: brud ----------------

        public void SplashMud(Vector3 fromWorld, float amount) { SplashMud(fromWorld, amount, DefaultMud); }

        /// <summary>Rozbryzg w dowolnym kolorze. Świeża plama przejmuje kolor proporcjonalnie do siły.</summary>
        public void SplashMud(Vector3 fromWorld, float amount, Color color)
        {
            float before = Mud;
            Mud = Mathf.Clamp01(Mud + amount);
            float k = before < 0.05f ? 1f : Mathf.Clamp01(amount / Mathf.Max(0.01f, Mud));
            MudColor = Color.Lerp(MudColor, color, k);
            mudDir = ToRootDir(fromWorld, 0.8f);
            mudAge = 0f;
        }

        /// <summary>Suchy kurz (bieg po piasku, mące, ziemi). Woda go nie przyjmuje – mokra skórka zbiera błoto.</summary>
        public void AddDust(float amount, Color color)
        {
            if (Wet > 0.4f) { SplashMud(transform.position + Vector3.down, amount * 0.5f, color * 0.75f); return; }
            float before = Dust;
            Dust = Mathf.Clamp01(Dust + amount);
            DustColor = Color.Lerp(DustColor, color, before < 0.05f ? 1f : 0.1f);
        }

        // ---------------- API: woda ----------------

        /// <summary>Zmoczenie (plusk, kałuża, deszcz). Woda rozmywa kurz i trochę błota.</summary>
        public void Soak(float amount)
        {
            Wet = Mathf.Clamp01(Wet + amount);
            Dust = Mathf.MoveTowards(Dust, 0f, amount * 1.5f);
            Mud = Mathf.MoveTowards(Mud, 0f, amount * 0.3f);
        }

        /// <summary>Kąpiel: woda zmywa błoto, kurz i trochę sadzy, skórka zostaje mokra.</summary>
        public void Wash(float amount = 1f)
        {
            Mud = Mathf.MoveTowards(Mud, 0f, amount);
            Dust = 0f;
            Burn = Mathf.MoveTowards(Burn, 0f, amount * 0.5f);
            Heat = 0f;
            Wet = Mathf.Clamp01(Wet + amount);
        }

        // ---------------- API: ogień, mróz ----------------

        public void Scorch(Vector3 explosionWorld, float amount) { Scorch(explosionWorld, amount, 1f); }

        public void Scorch(Vector3 explosionWorld, float amount, float heat)
        {
            Burn = Mathf.Clamp01(Burn + amount);
            Heat = Mathf.Max(Heat, heat);
            Wet = Mathf.MoveTowards(Wet, 0f, amount * 2f); // żar odparowuje wodę
            burnDir = ToRootDir(explosionWorld, 0.85f);
        }

        public void Freeze(float amount) { Frost = Mathf.Clamp01(Frost + amount + Wet * amount); } // mokry zamarza szybciej

        public void HitFlash(float amount = 1f) { Flash = Mathf.Max(Flash, Mathf.Clamp01(amount)); }

        // ---------------- API: uszkodzenia ----------------

        /// <summary>
        /// Cios: wgniecenie (sprężyna z odbiciem), siniak po stronie ciosu, a od <see cref="CutThreshold"/>
        /// rozcięcie skórki. pushDirWorld = kierunek, w którym cios pcha ofiarę.
        /// </summary>
        public void Impact(Vector3 pushDirWorld, float strength)
        {
            if (pushDirWorld.sqrMagnitude < 1e-4f) return;
            strength = Mathf.Clamp01(strength);
            var t = Root != null ? Root : transform;
            dentDirLocal = t.InverseTransformDirection(pushDirWorld.normalized);
            dent.Value = Mathf.Max(dent.Value, strength);
            dent.Velocity = 0f;
            Bruise = Mathf.Clamp01(Bruise + 0.22f * strength);
            var d = -dentDirLocal;
            bruiseDir = new Vector4(d.x, d.y, d.z, 0.9f);
            if (strength >= CutThreshold)
            {
                float k = (strength - CutThreshold) / (1f - CutThreshold);
                Cut = Mathf.Clamp01(Cut + 0.18f + 0.22f * k);
                cutDir = new Vector4(d.x, d.y, d.z, 1f);
                cutSeed.w = Random.Range(0.8f, 1.3f);
            }
        }

        public void Clear()
        {
            Mud = Wet = Frost = Burn = Heat = Flash = Bruise = Cut = 0f;
            Dust = DustFloor;
            FrostTarget = 0f;
            MudColor = DefaultMud;
            DustColor = DefaultDust;
            dent.Reset();
        }

        Vector4 ToRootDir(Vector3 fromWorld, float strength)
        {
            var t = Root != null ? Root : transform;
            var d = fromWorld - t.position;
            if (d.sqrMagnitude < 1e-4f) return new Vector4(0, -1, 0, 0);
            var local = t.InverseTransformDirection(d.normalized);
            return new Vector4(local.x, local.y, local.z, strength);
        }

        // ---------------- aktualizacja ----------------

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            mudAge += dt;
            if (mudAge > MudDryDelay) Mud = Mathf.MoveTowards(Mud, 0f, MudFade * dt);
            Dust = Mathf.MoveTowards(Dust, DustFloor, DustFade * dt);
            Wet = Mathf.MoveTowards(Wet, 0f, WetDry * dt * (1f + Heat * 4f));
            Frost = Mathf.MoveTowards(Frost, FrostTarget, FrostRate * dt);
            Burn = Mathf.MoveTowards(Burn, 0f, BurnFade * dt);
            Heat = Mathf.MoveTowards(Heat, 0f, HeatFade * dt);
            Flash = Mathf.MoveTowards(Flash, 0f, FlashFade * Time.unscaledDeltaTime); // błysk gaśnie też w hit-stopie
            Bruise = Mathf.MoveTowards(Bruise, 0f, BruiseFade * dt);
            Cut = Mathf.MoveTowards(Cut, 0f, CutHeal * dt);
            dent.Step(0f, DentFrequency, DentDamping, Time.unscaledDeltaTime);
            Push();
        }

        static bool Changed(ref float sent, float v)
        {
            if (Mathf.Abs(sent - v) < 0.0005f) return false;
            sent = v;
            return true;
        }

        void Push()
        {
            int n = materials.Count;
            if (n == 0) return;
            var t = Root != null ? Root : transform;

            var m = t.worldToLocalMatrix;
            Vector4 r0 = m.GetRow(0), r1 = m.GetRow(1), r2 = m.GetRow(2);
            bool dentActive = Mathf.Abs(dent.Value) > 0.002f || Mathf.Abs(dent.Velocity) > 0.02f;
            Vector4 center = Vector4.zero, dentDir = Vector4.zero;
            if (dentActive)
            {
                var p = t.position;
                var w = t.TransformDirection(dentDirLocal);
                center = new Vector4(p.x, p.y, p.z, BodyRadius);
                dentDir = new Vector4(w.x, w.y, w.z, Mathf.Clamp(dent.Value, -0.6f, 1f));
            }

            bool mud = Changed(ref sentMud, Mud) | sentMudDir != mudDir | sentMudColor != MudColor;
            bool dust = Changed(ref sentDust, Dust) | sentDustColor != DustColor;
            bool wet = Changed(ref sentWet, Wet);
            bool frost = Changed(ref sentFrost, Frost);
            float heatOut = Heat * Mathf.Clamp01(Burn * 2f);
            bool burn = Changed(ref sentBurn, Burn) | Changed(ref sentHeat, heatOut) | sentBurnDir != burnDir;
            bool flash = Changed(ref sentFlash, Flash);
            bool bruise = Changed(ref sentBruise, Bruise) | sentBruiseDir != bruiseDir;
            bool cut = Changed(ref sentCut, Cut) | sentCutDir != cutDir;
            sentMudDir = mudDir; sentMudColor = MudColor; sentDustColor = DustColor; sentBurnDir = burnDir; sentBruiseDir = bruiseDir; sentCutDir = cutDir;

            for (int i = 0; i < n; i++)
            {
                var mat = materials[i];
                if (mat == null) continue;
                mat.SetVector(Root0Id, r0);
                mat.SetVector(Root1Id, r1);
                mat.SetVector(Root2Id, r2);
                if (dentActive || dentWasActive)
                {
                    mat.SetVector(ImpactCenterId, center);
                    mat.SetVector(ImpactDirId, dentDir);
                }
                if (mud) { mat.SetFloat(MudAmountId, Mud); mat.SetVector(MudDirId, mudDir); mat.SetColor(MudColorId, MudColor); }
                if (dust) { mat.SetFloat(DustAmountId, Dust); mat.SetColor(DustColorId, DustColor); }
                if (wet) mat.SetFloat(WetAmountId, Wet);
                if (frost) mat.SetFloat(FrostAmountId, Frost);
                if (burn) { mat.SetFloat(BurnAmountId, Burn); mat.SetFloat(BurnHeatId, heatOut); mat.SetVector(BurnDirId, burnDir); }
                if (flash) mat.SetFloat(FlashAmountId, Flash);
                if (bruise) { mat.SetFloat(BruiseAmountId, Bruise); mat.SetVector(BruiseDirId, bruiseDir); }
                if (cut) { mat.SetFloat(CutAmountId, Cut); mat.SetVector(CutDirId, cutDir); mat.SetVector(CutSeedId, cutSeed); }
            }
            dentWasActive = dentActive;
        }
    }
}
