using UnityEngine;

namespace PotatoParty
{
    /// <summary>
    /// Jeden punkt wejścia całego sprzężenia zwrotnego. Każde zdarzenie rozgrywki uruchamia ten sam
    /// "akord" kanałów, dzięki czemu 12 minigier czuje się jak jedna gra:
    /// <code>
    ///                 hit stop        time dilation      impuls (siła)      rumble L/H    kamera              obraz
    /// DashHit         55–110 ms       –                  Hit 0.10–0.22      0.25/0.55     najazd 12%, FOV -2° CA+beczka
    /// EnvironmentHit  30–70 ms        –                  Slam 0.08–0.25     0.45/0.25     FOV -1°            CA
    /// Land (ciężkie)  –               –                  Land 0.03–0.12     0.30/0.05     –                  –
    /// Explosion       80 ms           0.45× / 0.35 s     Explosion 0.3–0.6  0.9/0.5       najazd 8%          CA+flash
    /// Knockout        90 ms           0.25× / 0.8 s      Hit 0.25           0.6/0.8       najazd 22% 0.9 s   CA
    /// Goal / meta     –               0.35× / 1.0 s      Explosion 0.2      0.4/0.4       najazd 15%         bloom
    /// </code>
    /// Wszystkie parametry są publicznymi polami statycznymi: można je stroić w trakcie gry (np. z konsoli).
    /// </summary>
    public static class Feel
    {
        // ---------------- strojenie ----------------
        public static float DashStopMin = 0.055f, DashStopMax = 0.11f;
        public static float EnvStopMin = 0.03f, EnvStopMax = 0.07f;
        public static float HitImpulseMin = 0.10f, HitImpulseMax = 0.22f;
        public static float KnockoutSlowScale = 0.25f;

        static CinematicCamera Cam { get { return GameManager.Instance != null ? GameManager.Instance.Camera : null; } }

        /// <summary>Trafienie zrywem gracza w gracza. power01: 0 = muśnięcie, 1 = pełny zryw w biegu.</summary>
        public static void DashHit(Potato attacker, Potato victim, Vector3 dir, float power01)
        {
            power01 = Mathf.Clamp01(power01);
            Vector3 contact = Vector3.Lerp(attacker.transform.position, victim.transform.position, 0.55f) + Vector3.up * 0.35f;

            TimeDirector.HitStop(Mathf.Lerp(DashStopMin, DashStopMax, power01 * power01));
            Impulses.Fire(ImpulseKind.Hit, contact, dir, Mathf.Lerp(HitImpulseMin, HitImpulseMax, power01));
            Rumble.Pulse(attacker.Slot, 0.15f + 0.15f * power01, 0.45f + 0.3f * power01);
            Rumble.Pulse(victim.Slot, 0.35f + 0.3f * power01, 0.35f + 0.2f * power01);
            var cam = Cam;
            if (cam != null)
            {
                cam.FocusOn(contact, 0.06f + 0.06f * power01, 0.35f);
                cam.Punch(-1.2f - 1.0f * power01);
            }
            PostFx.Punch(0.3f + 0.45f * power01);
            Vfx.Hit(victim.transform.parent, contact, dir, attacker.Color, power01);
        }

        /// <summary>Uderzenie z otoczenia (belka, wahadło, garnek, kula). Bez cząsteczek – ma je minigra.</summary>
        public static void EnvironmentHit(Potato victim, Vector3 dir, float power01)
        {
            power01 = Mathf.Clamp01(power01);
            if (power01 < 0.25f) return;
            var pos = victim != null ? victim.transform.position : Vector3.zero;
            TimeDirector.HitStop(Mathf.Lerp(EnvStopMin, EnvStopMax, power01));
            Impulses.Fire(ImpulseKind.Slam, pos, dir, 0.08f + 0.17f * power01);
            if (victim != null) Rumble.Pulse(victim.Slot, 0.45f * power01 + 0.1f, 0.25f * power01);
            if (Cam != null) Cam.Punch(-1f * power01);
            PostFx.Punch(0.25f * power01);
        }

        /// <summary>Lądowanie. fallSpeed – prędkość pionowa w chwili kontaktu (m/s, dodatnia).</summary>
        public static void Land(Potato p, float fallSpeed)
        {
            float k = Mathf.InverseLerp(9f, 22f, fallSpeed);
            if (k <= 0f) return;
            Impulses.Fire(ImpulseKind.Land, p.transform.position, Vector3.down, 0.03f + 0.09f * k);
            Rumble.Pulse(p.Slot, 0.15f + 0.25f * k, 0.05f);
        }

        public static void Jump(Potato p) { Rumble.Pulse(p.Slot, 0f, 0.12f); }

        public static void Dash(Potato p) { Rumble.Pulse(p.Slot, 0.08f, 0.3f); }

        /// <summary>Wybuch w świecie (bomba Gorącego Ziemniaka, garnek, fałszywe złoto).</summary>
        public static void Explosion(Vector3 pos, float size01)
        {
            size01 = Mathf.Clamp01(size01);
            TimeDirector.HitStop(0.04f + 0.04f * size01);
            if (size01 > 0.5f) TimeDirector.Dilate(0.45f, 0.03f, 0.12f, 0.35f);
            Impulses.Fire(ImpulseKind.Explosion, pos, Vector3.down, 0.3f + 0.3f * size01);
            var gm = GameManager.Instance;
            if (gm != null)
            {
                Rumble.PulseAll(gm.Potatoes, pos, 0.9f * size01 + 0.2f, 0.5f * size01, 18f);
                if (gm.Camera != null) gm.Camera.FocusOn(pos, 0.08f * size01, 0.5f);
            }
            PostFx.Punch(0.5f + 0.4f * size01);
            PostFx.Flash(0.6f * size01);
        }

        /// <summary>Odpadnięcie gracza (spadł, wybuchł, złapany): krótki "bullet time" i najazd.</summary>
        public static void Knockout(Potato p)
        {
            if (p == null) return;
            TimeDirector.HitStop(0.09f);
            TimeDirector.Dilate(KnockoutSlowScale, 0.05f, 0.35f, 0.45f);
            Impulses.Fire(ImpulseKind.Hit, p.transform.position, Vector3.down, 0.25f);
            Rumble.Pulse(p.Slot, 0.6f, 0.8f);
            if (Cam != null) Cam.FocusOn(p.transform.position, 0.22f, 0.9f);
            PostFx.Punch(0.6f);
        }

        /// <summary>Gol, meta, zwycięstwo rundy.</summary>
        public static void Triumph(Vector3 pos)
        {
            TimeDirector.Dilate(0.35f, 0.06f, 0.45f, 0.5f);
            Impulses.Fire(ImpulseKind.Explosion, pos, Vector3.down, 0.2f);
            var gm = GameManager.Instance;
            if (gm != null)
            {
                Rumble.PulseAll(gm.Potatoes, pos, 0.4f, 0.4f, 0f);
                if (gm.Camera != null) gm.Camera.FocusOn(pos, 0.15f, 1.0f);
            }
            PostFx.Flash(0.35f);
        }

        /// <summary>Stare API minigier: "wstrząśnij" o tyle metrów (0.1–0.6) → impuls wybuchowy w środku kadru.</summary>
        public static void Shake(float amount)
        {
            var cam = Cam;
            Vector3 at = cam != null ? cam.FocusPoint : Vector3.zero;
            Impulses.Fire(amount > 0.35f ? ImpulseKind.Explosion : ImpulseKind.Slam, at, Vector3.down, Mathf.Clamp(amount, 0f, 0.8f) * 0.7f);
        }
    }
}
