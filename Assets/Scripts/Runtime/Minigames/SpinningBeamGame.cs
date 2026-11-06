using UnityEngine;

namespace PotatoParty
{
    /// <summary>
    /// Obrotowa Belka: na skale nad lawą kręci się belka, coraz szybciej.
    /// Trzeba ją przeskakiwać. Kogo trafi, ten leci do lawy. Ostatni na skale wygrywa.
    /// </summary>
    public class SpinningBeamGame : Minigame
    {
        public override string Title { get { return "Obrotowa Belka"; } }
        public override string Description { get { return "Belka kręci się coraz szybciej i czasem nagle zawraca! Przeskakuj ją w odpowiedniej chwili, a zrywem wpychaj innych pod belkę."; } }
        public override string Tip { get { return "Skacz tuż przed tym, jak belka do ciebie dotrze. Uważaj, gdy zawraca!"; } }
        public override MapId PreferredMap { get { return MapId.Volcano; } }
        public override float TimeLimit { get { return 90f; } }
        public override float ArenaRadius { get { return 8f; } }
        public override string Controls { get { return "Ruch · SKOK nad belką · akcja = zryw"; } }

        Rotor rotor;
        float elapsed;
        bool second;
        float dir = 1f, dirSmooth = 1f, reverseTimer = 12f;
        bool warned;
        const float BeamHeight = 0.32f;

        public override void BuildArena()
        {
            MapBuilder.RoundArena(Root, ArenaRadius, Map);
            // Środkowa oś
            MapBuilder.Disc(Root, new Vector3(0, 0.5f, 0), 0.9f, 1f, Map.Wall);
            var pivot = Shapes.Empty("Belka", Root, Vector3.zero);
            rotor = pivot.AddComponent<Rotor>();
            rotor.Speed = 70f;
            MakeArm(pivot.transform, 0f, true);
        }

        void MakeArm(Transform pivot, float yaw, bool visibleHub)
        {
            var arm = Shapes.Empty("Ramie", pivot, Vector3.zero).transform;
            arm.localRotation = Quaternion.Euler(0, yaw, 0);
            float half = ArenaRadius + 1f;
            // ramię idzie przez całą średnicę (dwa końce)
            var beam = Shapes.Prim(PrimitiveType.Cylinder, arm, new Vector3(0, BeamHeight, 0), new Vector3(0.45f, half, 0.45f), Map.Accent, true);
            beam.transform.localRotation = Quaternion.Euler(90, 0, 0);
            beam.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(Map.Accent, 0.3f, 0.8f);
            Hazard.Add(beam, (p, h) =>
            {
                var radial = p.transform.position;
                radial.y = 0;
                var tangent = Vector3.Cross(Vector3.up, radial.sqrMagnitude > 0.01f ? radial.normalized : Vector3.forward) * Mathf.Sign(rotor.Speed);
                p.Knock(tangent * 9f + radial.normalized * 7f + Vector3.up * 6f, 0.9f);
                AudioManager.Play(Sfx.Bump, 0.9f);
                GM.Camera.Shake(0.25f);
            });
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (Finished || !Running) return;
            elapsed += dt;
            // nagłe zawracanie belki (z krótkim ostrzeżeniem)
            reverseTimer -= dt;
            if (!warned && reverseTimer < 1f)
            {
                warned = true;
                GM.Hud.Toast("Belka zaraz zawróci!", Map.Accent);
                AudioManager.Play(Sfx.Tick, 1f, 0f);
            }
            if (reverseTimer <= 0f)
            {
                dir = -dir;
                warned = false;
                reverseTimer = Random.Range(7f, 13f);
                AudioManager.Play(Sfx.Whoosh, 0.9f);
            }
            dirSmooth = Mathf.MoveTowards(dirSmooth, dir, dt * 4f);
            rotor.Speed = Mathf.Min(70f + elapsed * 2.2f, 210f) * dirSmooth;
            if (!second && elapsed > 30f)
            {
                second = true;
                MakeArm(rotor.transform, 90f, false);
                GM.Hud.Toast("Druga belka!", Map.Accent);
            }
        }

        public override string HudInfo()
        {
            return base.HudInfo() + "   ·   Na skale: " + AliveCount;
        }

        public override BotCommand Bot(Potato p)
        {
            var pos = p.transform.position;
            var flat = new Vector3(pos.x, 0, pos.z);
            float phi = Mathf.Atan2(pos.x, pos.z) * Mathf.Rad2Deg;
            float theta = rotor.Angle;
            float speed = Mathf.Abs(rotor.Speed);
            int arms = second ? 4 : 2;
            float spacing = 360f / arms;
            // Za ile czasu najbliższe ramię dojdzie do bota?
            float rel = rotor.Speed >= 0 ? phi - theta : theta - phi;
            rel = ((rel % spacing) + spacing) % spacing;
            float timeToHit = rel / Mathf.Max(1f, speed);
            float reaction = 0.2f + (p.Slot.Index % 3) * 0.04f;
            bool jump = p.Grounded && timeToHit < reaction && Random.value < 0.9f;
            Vector3 target = flat.magnitude > ArenaRadius * 0.55f ? flat * 0.4f : Wander(p, ArenaRadius * 0.5f, 2f);
            return BotCommand.Go(target, false, jump);
        }
    }
}
