using System.Collections.Generic;
using UnityEngine;

namespace PotatoParty
{
    /// <summary>
    /// Deszcz Garnków: na stołówce z nieba lecą garnki i patelnie. Czerwone koło pokazuje,
    /// gdzie spadnie garnek. Trafiony ziemniak odpada. Ostatni (lub wszyscy, którzy przetrwają) wygrywają.
    /// </summary>
    public class FallingPotsGame : Minigame
    {
        public override string Title { get { return "Deszcz Garnków"; } }
        public override string Description { get { return "Z sufitu spadają garnki i patelnie! Czerwone koło pokazuje, gdzie uderzą. Czasem spada gumowa kaczka (tylko odbija), a co jakiś czas OGROMNY gar. Wpychaj innych pod garnki!"; } }
        public override string Tip { get { return "Uciekaj z czerwonych kół. Im ciemniejsze koło, tym bliżej garnek."; } }
        public override MapId PreferredMap { get { return MapId.Canteen; } }
        public override float TimeLimit { get { return 70f; } }
        public override float ArenaRadius { get { return 9f; } }
        public override bool FallEliminates { get { return false; } }

        const float Half = 9f;
        const float FallTime = 1.3f;
        const float HitRadius = 1.25f;
        readonly List<Pot> pots = new List<Pot>();
        float spawnTimer = 1f, elapsed, megaTimer = 14f;

        class Pot
        {
            public Vector3 Target;
            public float T;
            public Transform Body;
            public Transform Marker;
            public Material MarkerMat;
            public bool Landed;
            public int Kind; // 0 garnek, 1 gumowa kaczka, 2 ogromny gar
            public float Radius;
            public float Linger;
        }

        public override void BuildArena()
        {
            Shapes.Box(Root, new Vector3(0, -0.5f, 0), new Vector3(Half * 2f, 1f, Half * 2f), Map.Floor, true);
            // kafelki
            for (int x = 0; x < 6; x++)
                for (int z = 0; z < 6; z++)
                    if ((x + z) % 2 == 0)
                        Shapes.Box(Root, new Vector3(-Half + 1.5f + x * 3f, 0.005f, -Half + 1.5f + z * 3f), new Vector3(3f, 0.01f, 3f), Map.FloorAlt, false);
            for (int s = 0; s < 4; s++)
            {
                var rot = Quaternion.Euler(0, s * 90f, 0);
                var w = Shapes.Box(Root, rot * new Vector3(0, 0.6f, Half + 0.3f), new Vector3(Half * 2f + 1.2f, 1.2f, 0.6f), Map.Wall, true);
                w.transform.localRotation = rot;
            }
        }

        public override Vector3 SpawnPoint(int i, int n)
        {
            float a = (i / (float)Mathf.Max(1, n)) * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 4f + Vector3.up * (Potato.HalfHeight + 0.1f);
        }

        void SpawnPot(Vector3 at, int kind = 0)
        {
            float radius = kind == 2 ? 2.7f : HitRadius;
            at.x = Mathf.Clamp(at.x, -Half + radius * 0.6f, Half - radius * 0.6f);
            at.z = Mathf.Clamp(at.z, -Half + radius * 0.6f, Half - radius * 0.6f);
            at.y = 0f;
            var pot = new Pot { Target = at, Kind = kind, Radius = radius };
            var marker = MapBuilder.Disc(Root, at + Vector3.up * 0.02f, radius, 0.02f, Color.red, false);
            pot.MarkerMat = Shapes.UniqueMat(new Color(1f, 0.3f, 0.3f), 0.2f);
            marker.GetComponent<Renderer>().sharedMaterial = pot.MarkerMat;
            pot.Marker = marker.transform;
            pot.Marker.localScale = new Vector3(0.2f, 0.01f, 0.2f);

            var body = Shapes.Empty("Garnek", Root, at + Vector3.up * 16f).transform;
            bool pan = Random.value < 0.35f;
            if (kind == 1)
            {
                var yellow = Shapes.Hex("#FFD23F");
                Shapes.Prim(PrimitiveType.Sphere, body, new Vector3(0, 0.5f, 0), new Vector3(1.4f, 1f, 1.8f), yellow, false, 0.7f);
                Shapes.Prim(PrimitiveType.Sphere, body, new Vector3(0, 1.25f, 0.55f), Vector3.one * 0.9f, yellow, false, 0.7f);
                Shapes.Prim(PrimitiveType.Sphere, body, new Vector3(0, 1.15f, 1.05f), new Vector3(0.5f, 0.2f, 0.45f), Shapes.Hex("#FF8A3D"));
                for (int s = -1; s <= 1; s += 2)
                    Shapes.Prim(PrimitiveType.Sphere, body, new Vector3(0.25f * s, 1.4f, 0.9f), Vector3.one * 0.14f, Color.black);
            }
            else if (kind == 2)
            {
                Shapes.Prim(PrimitiveType.Cylinder, body, new Vector3(0, 1.1f, 0), new Vector3(4.6f, 1.1f, 4.6f), Shapes.Hex("#9AA7B2"), false, 0.85f);
                for (int s = -1; s <= 1; s += 2)
                    Shapes.Prim(PrimitiveType.Cube, body, new Vector3(2.5f * s, 1.7f, 0), new Vector3(0.7f, 0.3f, 0.5f), Shapes.Hex("#555555"));
            }
            else if (pan)
            {
                Shapes.Prim(PrimitiveType.Cylinder, body, Vector3.zero, new Vector3(1.8f, 0.12f, 1.8f), Shapes.Hex("#333333"), false, 0.7f);
                var handle = Shapes.Prim(PrimitiveType.Cube, body, new Vector3(1.5f, 0, 0), new Vector3(1.4f, 0.15f, 0.3f), Shapes.Hex("#7A4A2A"));
                handle.name = "Raczka";
            }
            else
            {
                Shapes.Prim(PrimitiveType.Cylinder, body, new Vector3(0, 0.5f, 0), new Vector3(1.7f, 0.5f, 1.7f), Shapes.Hex("#9AA7B2"), false, 0.85f);
                for (int s = -1; s <= 1; s += 2)
                    Shapes.Prim(PrimitiveType.Cube, body, new Vector3(0.95f * s, 0.8f, 0), new Vector3(0.35f, 0.15f, 0.25f), Shapes.Hex("#555555"));
            }
            body.localRotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
            pot.Body = body;
            pots.Add(pot);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (Finished || !Running) return;
            elapsed += dt;

            spawnTimer -= dt;
            if (spawnTimer <= 0f)
            {
                float k = Mathf.Clamp01(elapsed / 55f);
                spawnTimer = Mathf.Lerp(0.85f, 0.28f, k);
                int count = elapsed > 35f ? 2 : 1;
                for (int c = 0; c < count; c++)
                {
                    Vector3 at;
                    var alive = new List<Potato>(Alive);
                    if (alive.Count > 0 && Random.value < 0.45f)
                    {
                        var victim = alive[Random.Range(0, alive.Count)];
                        at = victim.transform.position + Phys.GetVelocity(victim.Body) * 0.5f;
                    }
                    else at = new Vector3(Random.Range(-Half, Half), 0, Random.Range(-Half, Half));
                    SpawnPot(at, Random.value < 0.15f ? 1 : 0);
                }
            }
            megaTimer -= dt;
            if (megaTimer <= 0f)
            {
                megaTimer = Random.Range(12f, 16f);
                var alive = new List<Potato>(Alive);
                var at = alive.Count > 0 ? alive[Random.Range(0, alive.Count)].transform.position : Vector3.zero;
                SpawnPot(at, 2);
                GM.Hud.Toast("OGROMNY GAR!", Color.red);
                AudioManager.Play(Sfx.Whoosh, 1f);
            }

            for (int i = pots.Count - 1; i >= 0; i--)
            {
                var pot = pots[i];
                if (!pot.Landed)
                {
                    pot.T += dt;
                    float k = Mathf.Clamp01(pot.T / FallTime);
                    pot.Body.position = pot.Target + Vector3.up * Mathf.Lerp(16f, 0f, k * k);
                    float m = Mathf.Lerp(0.2f, HitRadius * 2f, k);
                    pot.Marker.localScale = new Vector3(m, 0.01f, m);
                    Shapes.SetColor(pot.MarkerMat, Color.Lerp(new Color(1f, 0.6f, 0.6f), new Color(0.9f, 0.05f, 0.05f), k));
                    if (k >= 1f)
                    {
                        pot.Landed = true;
                        pot.Linger = 0.8f;
                        AudioManager.Play(Sfx.Bump, 1f, 0.2f);
                        GM.Camera.Shake(pot.Kind == 2 ? 0.5f : 0.15f);
                        var soup = Shapes.Hex("#E3973A");
                        if (pot.Kind == 1) Effects.Splash(Root, pot.Target + Vector3.up * 0.3f, Shapes.Hex("#FFD23F"));
                        else
                        {
                            Fx.Splash(Root, pot.Target + Vector3.up * 0.3f, soup, 1.4f, 34);
                            Fx.Dust(Root, pot.Target + Vector3.up * 0.1f, 1.6f, 12);
                            Fx.Shockwave(Root, pot.Target + Vector3.up * 0.05f, new Color(1f, 1f, 1f, 0.8f), pot.Radius * 2.6f, 0.4f);
                        }
                        foreach (var p in new List<Potato>(Alive))
                        {
                            var d = p.transform.position - pot.Target;
                            d.y = 0;
                            // zupa chlapie na tych, którzy stali tuż obok
                            if (pot.Kind != 1 && p.Surface != null && d.magnitude < pot.Radius + 2.2f && d.magnitude >= pot.Radius)
                                p.Surface.SplashMud(pot.Target, Mathf.Lerp(0.45f, 0.15f, (d.magnitude - pot.Radius) / 2.2f), soup);
                            if (d.magnitude >= pot.Radius || p.transform.position.y >= 2.2f) continue;
                            if (pot.Kind == 1)
                            {
                                // gumowa kaczka tylko odbija (śmiesznie wysoko)
                                p.Knock((d.sqrMagnitude > 0.01f ? d.normalized : Vector3.forward) * 8f + Vector3.up * 11f, 0.7f);
                                GM.Hud.Toast(p.Slot.Name + " dostaje kaczką! Kwa!", p.Color);
                            }
                            else Eliminate(p, false, pot.Kind == 2 ? " zostaje przykryty garem!" : " dostaje garnkiem!");
                        }
                    }
                }
                else
                {
                    pot.Linger -= dt;
                    pot.Body.position += Vector3.down * dt * 1.5f;
                    if (pot.Linger <= 0f)
                    {
                        Object.Destroy(pot.Body.gameObject);
                        Object.Destroy(pot.Marker.gameObject);
                        pots.RemoveAt(i);
                    }
                }
            }
        }

        public override string HudInfo()
        {
            return base.HudInfo() + "   ·   W grze: " + AliveCount;
        }

        public override BotCommand Bot(Potato p)
        {
            var pos = p.transform.position;
            var flee = Vector3.zero;
            foreach (var pot in pots)
            {
                if (pot.Landed) continue;
                var d = pos - pot.Target;
                d.y = 0;
                float dist = d.magnitude;
                if (dist < pot.Radius + 1.6f)
                {
                    float urgency = Mathf.Clamp01(pot.T / FallTime) + 0.3f;
                    flee += (dist < 0.05f ? Random.insideUnitSphere : d / dist) * urgency * (pot.Radius + 1.6f - dist);
                }
            }
            flee.y = 0;
            if (flee.sqrMagnitude > 0.01f)
                return BotCommand.Go(Clamp(pos + flee.normalized * 3f, Half - 1.2f));
            return BotCommand.Go(Wander(p, Half - 2f, 1.2f));
        }

        public override void Cleanup() { pots.Clear(); }
    }
}
