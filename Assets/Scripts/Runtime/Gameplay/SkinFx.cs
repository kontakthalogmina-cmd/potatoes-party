using UnityEngine;

namespace PotatoParty
{
    /// <summary>
    /// Reakcje skórki na świat: błoto przy lądowaniu na ziemi, szron na lodzie, przypieczone stopy
    /// na wulkanie, plamy po zderzeniach, plusk i kąpiel po wpadnięciu do wody, zwęglenie w lawie.
    /// Jedno miejsce z zasadami dla wszystkich map, żeby minigry wołały tylko proste zdarzenia.
    /// </summary>
    public static class SkinFx
    {
        public static MapTheme Map;

        static readonly Color Soil = Shapes.Hex("#4A3020");
        static readonly Color Sand = Shapes.Hex("#D9B878");
        static readonly Color Grass = Shapes.Hex("#4F7A2E");
        static readonly Color Flour = Shapes.Hex("#F3EBDD");
        static readonly Color Ash = Shapes.Hex("#2E2826");

        /// <summary>Ustawia mapę: kolor kurzu, "błota" i zachowanie skórki.</summary>
        public static void SetMap(MapTheme t)
        {
            Map = t;
            Fx.DustColor = DustFor(t);
        }

        static Color DustFor(MapTheme t)
        {
            if (t == null) return new Color(0.93f, 0.9f, 0.84f, 0.8f);
            switch (t.Id)
            {
                case MapId.Ice: return new Color(0.95f, 0.98f, 1f, 0.9f);
                case MapId.Volcano: return new Color(0.35f, 0.3f, 0.28f, 0.85f);
                case MapId.Beach:
                case MapId.Playground: return new Color(0.95f, 0.86f, 0.66f, 0.85f);
                case MapId.Kitchen: return new Color(1f, 0.97f, 0.92f, 0.9f);
                case MapId.Cornfield: return new Color(0.92f, 0.8f, 0.55f, 0.8f);
                default: return new Color(0.86f, 0.78f, 0.64f, 0.75f);
            }
        }

        /// <summary>Kolor "błota" podłoża danej mapy (null = podłoże czyste).</summary>
        public static Color? GroundStain(MapTheme t)
        {
            if (t == null) return null;
            switch (t.Id)
            {
                case MapId.Farm:
                case MapId.Orchard:
                case MapId.Garden:
                case MapId.Cornfield: return Soil;
                case MapId.Forest: return Color.Lerp(Soil, Grass, 0.25f);
                case MapId.Beach:
                case MapId.Playground: return Sand;
                case MapId.Stadium: return Grass;
                case MapId.Kitchen: return Flour;
                case MapId.Volcano: return Ash;
                default: return null;
            }
        }

        static bool WaterPit(MapTheme t)
        {
            return t != null && (t.Id == MapId.Beach || t.Id == MapId.Ice || t.Pit == Shapes.Hex("#4FC3F0"));
        }

        /// <summary>Stan początkowy skórki na mapie (np. lekki szron na mrozie).</summary>
        public static void Configure(Potato p)
        {
            if (p.Surface == null || Map == null) return;
            p.Surface.Clear();
            if (Map.Id == MapId.Ice) p.Surface.FrostTarget = 0.18f;
        }

        /// <summary>Mocne lądowanie: obłoczek kurzu i trochę podłoża na stopach.</summary>
        public static void OnLand(Potato p, Vector3 point, float impact)
        {
            var parent = p.transform.parent;
            Fx.Dust(parent, point, Mathf.Clamp(impact / 8f, 0.6f, 1.4f), 7);
            if (p.Surface == null || Map == null) return;
            if (Map.Id == MapId.Ice) { p.Surface.Freeze(0.04f); return; }
            var stain = GroundStain(Map);
            if (stain.HasValue) p.Surface.SplashMud(point + Vector3.down, Mathf.Clamp(impact * 0.008f, 0.02f, 0.08f), stain.Value);
        }

        /// <summary>Uderzenie zrywem: gwiazdka trafienia i plama od strony uderzenia.</summary>
        public static void OnHit(Potato victim, Potato hitter)
        {
            var mid = Vector3.Lerp(victim.transform.position, hitter.transform.position, 0.5f) + Vector3.up * 0.3f;
            Fx.Impact(victim.transform.parent, mid, hitter.Color);
            if (victim.Surface == null || Map == null) return;
            var stain = GroundStain(Map);
            if (stain.HasValue && Map.Id != MapId.Kitchen && Map.Id != MapId.Volcano)
                victim.Surface.SplashMud(hitter.transform.position, 0.12f, stain.Value);
        }

        /// <summary>Wpadnięcie w dno mapy: plusk wody, lawa albo kafelki. Skórka reaguje (jeśli gracz wraca na arenę).</summary>
        public static void OnFall(Potato p, Transform fxParent)
        {
            if (Map == null) return;
            var pos = p.transform.position;
            var surface = new Vector3(pos.x, Map.PitHeight + 0.05f, pos.z);
            var s = p.Surface;
            if (Map.Id == MapId.Volcano)
            {
                Fx.Embers(fxParent, surface + Vector3.up * 0.3f, 2f, 40);
                Fx.Sparks(fxParent, surface, new Color(1f, 0.5f, 0.15f), 26, 9f, 1f);
                Fx.FlashLight(fxParent, surface + Vector3.up, new Color(1f, 0.45f, 0.1f), 8f, 25f, 0.6f);
                if (s != null) s.Scorch(surface + Vector3.down, 1f);
            }
            else if (WaterPit(Map))
            {
                Fx.WaterSplash(fxParent, surface, Map.Pit);
                if (s != null)
                {
                    s.Wash();
                    if (Map.Id == MapId.Ice) { s.Freeze(0.9f); Fx.FrostBurst(fxParent, surface + Vector3.up * 0.5f, 1.4f); }
                }
            }
            else
            {
                Fx.Dust(fxParent, surface, 2f, 14);
            }
        }

        /// <summary>Ciągłe działanie mapy (np. gorące kamienie wulkanu przypiekają stopy).</summary>
        public static void Tick(Potato p, float dt)
        {
            if (p.Surface == null || Map == null || !p.Grounded) return;
            if (Map.Id == MapId.Volcano && p.Surface.Burn < 0.22f)
                p.Surface.Scorch(p.transform.position + Vector3.down * 2f, dt * 0.012f, 0.35f);
        }
    }
}
