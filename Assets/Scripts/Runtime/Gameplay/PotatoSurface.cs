using System.Collections.Generic;
using UnityEngine;

namespace PotatoParty
{
    /// <summary>
    /// Stan powierzchni ziemniaka (błoto, szron, zwęglenie, błysk trafienia) przekazywany do shadera
    /// PotatoParty/PotatoHero przez MaterialPropertyBlock. Jeden blok dla wszystkich części ciała,
    /// więc współdzielone materiały się nie mnożą, a maski są spójne na całej postaci
    /// (liczone w przestrzeni korzenia <see cref="Root"/>, a nie każdej bryły osobno).
    /// </summary>
    public class PotatoSurface : MonoBehaviour
    {
        static readonly int MudAmountId = Shader.PropertyToID("_MudAmount");
        static readonly int MudDirId = Shader.PropertyToID("_MudDir");
        static readonly int FrostAmountId = Shader.PropertyToID("_FrostAmount");
        static readonly int BurnAmountId = Shader.PropertyToID("_BurnAmount");
        static readonly int BurnHeatId = Shader.PropertyToID("_BurnHeat");
        static readonly int BurnDirId = Shader.PropertyToID("_BurnDir");
        static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        static readonly int Root0Id = Shader.PropertyToID("_PP_Root0");
        static readonly int Root1Id = Shader.PropertyToID("_PP_Root1");
        static readonly int Root2Id = Shader.PropertyToID("_PP_Root2");
        static readonly int UseRootId = Shader.PropertyToID("_PP_UseRoot");

        /// <summary>Przestrzeń postaci: zwykle Potato.Visual (obraca się z fikołkami i zgniataniem).</summary>
        public Transform Root;

        [Range(0, 1)] public float Mud;
        [Range(0, 1)] public float Frost;
        [Range(0, 1)] public float Burn;
        [Range(0, 1)] public float Heat;
        [Range(0, 1)] public float Flash;

        /// <summary>Poziom szronu, do którego postać dąży sama (np. 0.3 na lodowej mapie).</summary>
        [Range(0, 1)] public float FrostTarget;

        [Header("Zanikanie (na sekundę)")]
        public float MudDryDelay = 4f;
        public float MudFade = 0.05f;
        public float FrostRate = 0.25f;
        public float BurnFade = 0.03f;
        public float HeatFade = 1.2f;
        public float FlashFade = 7f;

        readonly List<Renderer> renderers = new List<Renderer>();
        MaterialPropertyBlock block;
        Vector4 mudDir = new Vector4(0, -1, 0, 0);
        Vector4 burnDir = new Vector4(0, 0, 1, 0);
        float mudAge;

        public void Init(Transform root, IEnumerable<Renderer> parts)
        {
            Root = root;
            renderers.Clear();
            renderers.AddRange(parts);
            block = new MaterialPropertyBlock();
            Push();
        }

        // ---------------- API dla minigier ----------------

        /// <summary>Rozbryzg błota z punktu w świecie (np. wybuch fałszywego złota, kałuża).</summary>
        public void SplashMud(Vector3 fromWorld, float amount)
        {
            Mud = Mathf.Clamp01(Mud + amount);
            mudDir = ToRootDir(fromWorld, 0.8f);
            mudAge = 0f;
        }

        /// <summary>Zwęglenie od wybuchu w punkcie świata. Żar na krawędzi gaśnie szybko, sadza zostaje dłużej.</summary>
        public void Scorch(Vector3 explosionWorld, float amount)
        {
            Burn = Mathf.Clamp01(Burn + amount);
            Heat = 1f;
            burnDir = ToRootDir(explosionWorld, 0.85f);
        }

        /// <summary>Natychmiastowe zamrożenie (np. trafienie śnieżką). Później topnieje do <see cref="FrostTarget"/>.</summary>
        public void Freeze(float amount) { Frost = Mathf.Clamp01(Frost + amount); }

        /// <summary>Krótki biały błysk po trafieniu.</summary>
        public void HitFlash(float amount = 1f) { Flash = Mathf.Max(Flash, Mathf.Clamp01(amount)); }

        public void Clear() { Mud = Frost = Burn = Heat = Flash = 0f; FrostTarget = 0f; }

        // Kierunek w przestrzeni postaci zapamiętany w chwili trafienia: plama "przykleja się" do ciała
        // i obraca razem z ziemniakiem, zamiast zawsze patrzeć na miejsce wybuchu.
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
            Frost = Mathf.MoveTowards(Frost, FrostTarget, FrostRate * dt);
            Burn = Mathf.MoveTowards(Burn, 0f, BurnFade * dt);
            Heat = Mathf.MoveTowards(Heat, 0f, HeatFade * dt);
            Flash = Mathf.MoveTowards(Flash, 0f, FlashFade * dt);
            Push();
        }

        void Push()
        {
            if (block == null || renderers.Count == 0) return;
            var t = Root != null ? Root : transform;
            var m = t.worldToLocalMatrix;
            block.SetVector(Root0Id, m.GetRow(0));
            block.SetVector(Root1Id, m.GetRow(1));
            block.SetVector(Root2Id, m.GetRow(2));
            block.SetFloat(UseRootId, 1f);
            block.SetFloat(MudAmountId, Mud);
            block.SetVector(MudDirId, mudDir);
            block.SetFloat(FrostAmountId, Frost);
            block.SetFloat(BurnAmountId, Burn);
            block.SetFloat(BurnHeatId, Heat * Mathf.Clamp01(Burn * 2f));
            block.SetVector(BurnDirId, burnDir);
            block.SetFloat(FlashAmountId, Flash);
            for (int i = 0; i < renderers.Count; i++)
                if (renderers[i] != null) renderers[i].SetPropertyBlock(block);
        }
    }
}
