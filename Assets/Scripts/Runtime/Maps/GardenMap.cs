using System.Collections.Generic;
using UnityEngine;

namespace PotatoParty
{
    /// <summary>Co stoi w zapadniętej grządce na środku ogrodu (każda gra ma swój "środek").</summary>
    public enum GardenCenter { Fryer, Pan, Table, Soil }

    /// <summary>Punkty ogrodu, z których korzystają gry (spawny, frytkownica, dach, wieża...).</summary>
    public class GardenLayout
    {
        public Transform Root;
        public Vector3 PitCenter = new Vector3(0f, -1.5f, 0f);
        public float PitHalf = 7f;
        public Vector3 ShedRoof = new Vector3(-20f, 6f, 18f);
        public Vector3 TankTop = new Vector3(-20f, 9f, 24f);
        public readonly List<Vector3> Terraces = new List<Vector3>();
        public readonly List<Vector3> SoftSoil = new List<Vector3>();
        public GateBridge Bridge;
    }

    /// <summary>
    /// <b>Ogród Babci</b> – duża mapa 56 × 56 m z pięcioma poziomami wysokości:
    /// <code>
    ///  wieża (zbiornik) 9 m  ← trampolina na dachu
    ///  dach szopy       6 m  ← trampolina z kompostu
    ///  tarasy (skrzynie) 2.5 m ← rampy z desek
    ///  trawnik          0 m
    ///  grządka (dno)   −1.5 m ← rampy N/S, most z dźwignią
    /// </code>
    /// Elementy interaktywne: grabie, zraszacz, taczka, szyby szklarni, 2 trampoliny, dźwignia mostu.
    /// Środek grządki zależy od gry (<see cref="GardenCenter"/>).
    /// </summary>
    public static class GardenMap
    {
        public const float Half = 28f;
        public const float KillY = -6f;

        static readonly Color Grass = Shapes.Hex("#6FAF4A"), GrassAlt = Shapes.Hex("#64A343");
        static readonly Color Soil = Shapes.Hex("#6B4A32"), Mud = Shapes.Hex("#4E3524");
        static readonly Color Wood = Shapes.Hex("#A8743F"), WoodDark = Shapes.Hex("#7A5230");
        static readonly Color Gravel = Shapes.Hex("#C9BBA4"), Stone = Shapes.Hex("#9A948A");
        static readonly Color ShedCol = Shapes.Hex("#B5503C"), Roof = Shapes.Hex("#5B4A62");

        public static GardenLayout Build(Transform parent, MapTheme theme, GardenCenter center)
        {
            var L = new GardenLayout();
            var root = Shapes.Empty("Ogrod Babci", parent, Vector3.zero).transform;
            L.Root = root;
            float h = Half, p = L.PitHalf;

            // ---- trawnik wokół zapadniętej grządki (4 płyty, grubość 2 m) ----
            Shapes.Box(root, new Vector3(0, -1f, (h + p) * 0.5f), new Vector3(2 * h, 2f, h - p), Grass);
            Shapes.Box(root, new Vector3(0, -1f, -(h + p) * 0.5f), new Vector3(2 * h, 2f, h - p), Grass);
            Shapes.Box(root, new Vector3(-(h + p) * 0.5f, -1f, 0), new Vector3(h - p, 2f, 2 * p), GrassAlt);
            Shapes.Box(root, new Vector3((h + p) * 0.5f, -1f, 0), new Vector3(h - p, 2f, 2 * p), GrassAlt);
            // ścieżki ze żwiru (pełna przyczepność, czytelne linie ruchu)
            Shapes.Box(root, new Vector3(0, 0.01f, -(h + p) * 0.5f), new Vector3(2.4f, 0.04f, h - p), Gravel, false);
            Shapes.Box(root, new Vector3(0, 0.01f, (h + p) * 0.5f), new Vector3(2.4f, 0.04f, h - p), Gravel, false);
            Shapes.Box(root, new Vector3(-(h + p) * 0.5f, 0.01f, 0), new Vector3(h - p, 0.04f, 2.4f), Gravel, false);
            Shapes.Box(root, new Vector3((h + p) * 0.5f, 0.01f, 0), new Vector3(h - p, 0.04f, 2.4f), Gravel, false);

            // ---- grządka: dno −1.5 m z błotem, rampy N i S ----
            Shapes.Box(root, new Vector3(0, -2f, 0), new Vector3(2 * p, 1f, 2 * p), Mud);
            Ramp(root, new Vector3(-4.5f, 0f, p), new Vector3(-4.5f, -1.5f, p - 4.5f), 3f, Wood);
            Ramp(root, new Vector3(4.5f, 0f, -p), new Vector3(4.5f, -1.5f, -p + 4.5f), 3f, Wood);
            // krawędź grządki: niska deska (wizualna, bez kolizji – da się wpaść)
            for (int s = 0; s < 4; s++)
            {
                var d = Quaternion.Euler(0, s * 90f, 0) * Vector3.forward;
                var side = Quaternion.Euler(0, s * 90f + 90f, 0) * Vector3.forward;
                var b = Shapes.Box(root, d * (p + 0.15f) + Vector3.up * 0.08f, new Vector3(2 * p + 0.6f, 0.16f, 0.3f), WoodDark, false);
                b.transform.localRotation = Quaternion.LookRotation(side);
            }

            // ---- most nad grządką (W–E) z dźwignią ----
            var bridge = Shapes.Box(root, new Vector3(0, -0.2f, -3.2f), new Vector3(2 * p + 1f, 0.4f, 2.6f), Wood);
            L.Bridge = bridge.AddComponent<GateBridge>();
            L.Bridge.Up = new Vector3(0, -0.2f, -3.2f);
            L.Bridge.Down = new Vector3(0, -2.6f, -3.2f);
            var lever = Shapes.Box(root, new Vector3(p + 2.5f, 0.06f, -5.5f), new Vector3(1.6f, 0.12f, 1.6f), Shapes.Hex("#E2463F"));
            var lc = lever.GetComponent<Collider>(); lc.isTrigger = true;
            Shapes.Prim(PrimitiveType.Cylinder, lever.transform, new Vector3(0, 4f, 0), new Vector3(0.12f, 4f, 0.12f), Stone);
            var lv = lever.AddComponent<GateLever>();
            lv.Bridge = L.Bridge;

            // ---- środek grządki zależny od gry ----
            BuildCenter(root, L, center, theme);

            // ---- tarasy: skrzynie warzywne 2.5 m z rampami ----
            Vector3[] terr = { new Vector3(-14f, 0, -14f), new Vector3(14f, 0, -14f), new Vector3(15f, 0, 6f), new Vector3(-14f, 0, 4f) };
            for (int i = 0; i < terr.Length; i++)
            {
                var c = terr[i];
                Shapes.Box(root, c + new Vector3(0, 1.25f, 0), new Vector3(7f, 2.5f, 4f), Wood);
                Shapes.Box(root, c + new Vector3(0, 2.52f, 0), new Vector3(6.4f, 0.06f, 3.4f), Soil, false);
                // warzywa na skrzyni (dekoracja)
                for (int k = -2; k <= 2; k++)
                    Shapes.Prim(PrimitiveType.Sphere, root, c + new Vector3(k * 1.2f, 2.75f, 0.6f * ((k & 1) == 0 ? 1 : -1)), new Vector3(0.5f, 0.35f, 0.5f), Shapes.Hex(k % 2 == 0 ? "#E8743B" : "#3E9A3A"), false, 0.5f);
                float side = c.x < 0 ? 1f : -1f;
                Ramp(root, c + new Vector3(side * 3.5f + side * 5f, 0f, 0f), c + new Vector3(side * 3.5f, 2.5f, 0f), 2.2f, WoodDark);
                L.Terraces.Add(c + Vector3.up * 2.5f);
            }

            // ---- szopa (dach 6 m) + trampolina z kompostu ----
            var shed = L.ShedRoof;
            Shapes.Box(root, new Vector3(shed.x, 3f, shed.z), new Vector3(8f, 6f, 6f), ShedCol);
            Shapes.Box(root, new Vector3(shed.x, 6.05f, shed.z), new Vector3(8.6f, 0.1f, 6.6f), Roof);
            Shapes.Box(root, new Vector3(shed.x + 4.02f, 2f, shed.z), new Vector3(0.05f, 3.4f, 1.8f), WoodDark, false); // drzwi
            JumpPad.Build(root, new Vector3(shed.x + 8f, 0f, shed.z), Soil, new Vector3(shed.x + 2f, 6f, shed.z), 21f, "Kompost");
            // zbiornik na deszczówkę (9 m) + trampolina na dachu
            var tank = Shapes.Prim(PrimitiveType.Cylinder, root, new Vector3(L.TankTop.x, 4.5f, L.TankTop.z), new Vector3(3.6f, 4.5f, 3.6f), Shapes.Hex("#4F7F96"), true, 0.6f);
            tank.name = "Zbiornik";
            JumpPad.Build(root, new Vector3(shed.x, 6.1f, shed.z + 1.6f), Shapes.Hex("#2FC872"), L.TankTop, 15.5f, "Trampolina");

            // ---- szklarnia z szybami do rozbicia ----
            var gh = new Vector3(18f, 0f, 19f);
            Shapes.Box(root, gh + new Vector3(0, 0.1f, 0), new Vector3(8f, 0.2f, 6f), Stone);
            for (int s = -1; s <= 1; s += 2)
            {
                GlassPanel.Build(root, gh + new Vector3(s * 4f, 1.6f, 0), new Vector3(0.12f, 3f, 6f));
                GlassPanel.Build(root, gh + new Vector3(0, 1.6f, s * 3f), new Vector3(8f, 3f, 0.12f));
            }
            Shapes.Box(root, gh + new Vector3(0, 3.15f, 0), new Vector3(8.2f, 0.1f, 6.2f), Shapes.Hex("#D9F2FF"), false);

            // ---- grabie, zraszacz, taczka ----
            Rake.Build(root, new Vector3(-7f, 0f, -12f), 20f);
            Rake.Build(root, new Vector3(9.5f, 0f, -9.5f), -60f);
            Rake.Build(root, new Vector3(-10f, 0f, 9.5f), 130f);
            Rake.Build(root, new Vector3(11f, 0f, 11f), 75f);
            Sprinkler.Build(root, new Vector3(-2f, 0f, -18f));
            Sprinkler.Build(root, new Vector3(20f, 0f, -3f));
            Wheelbarrow(root, new Vector3(6f, 0.6f, -20f));
            Wheelbarrow(root, new Vector3(-21f, 0.6f, -6f));

            // miękka ziemia (kopce Kreta, kurz)
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f + 0.3f;
                var c = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 17f;
                Shapes.Box(root, c + Vector3.up * 0.015f, new Vector3(5f, 0.03f, 3f), Soil, false, a * Mathf.Rad2Deg);
                L.SoftSoil.Add(c);
            }

            // płot dookoła (niski, wizualny – z ogrodu da się wypaść)
            for (int s = 0; s < 4; s++)
            {
                var d = Quaternion.Euler(0, s * 90f, 0) * Vector3.forward;
                for (int k = -6; k <= 6; k++)
                {
                    var side = Quaternion.Euler(0, s * 90f + 90f, 0) * Vector3.forward;
                    var pos = d * (h - 0.3f) + side * (k * 4.3f);
                    Shapes.Box(root, pos + Vector3.up * 0.5f, new Vector3(0.18f, 1f, 0.18f), Shapes.Hex("#F2E6CF"), false);
                }
            }
            return L;
        }

        static void BuildCenter(Transform root, GardenLayout L, GardenCenter center, MapTheme theme)
        {
            var c = L.PitCenter;
            switch (center)
            {
                case GardenCenter.Fryer:
                    // pomost (strefa wrzutu) + kocioł z olejem
                    MapBuilder.Disc(root, c + Vector3.up * 0.15f, 3.4f, 0.3f, Stone);
                    var pot = Shapes.Prim(PrimitiveType.Cylinder, root, c + Vector3.up * 1f, new Vector3(3.6f, 0.7f, 3.6f), Shapes.Hex("#3B3552"), true, 0.75f);
                    pot.name = "Frytkownica";
                    var oil = Shapes.Prim(PrimitiveType.Cylinder, root, c + Vector3.up * 1.62f, new Vector3(3.2f, 0.04f, 3.2f), Shapes.Hex("#F7C548"), false, 0.95f);
                    oil.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(Shapes.Hex("#F7C548"), 0.95f, 0.8f);
                    oil.name = "Olej";
                    Look.PointLight(root, c + Vector3.up * 2.4f, Shapes.Hex("#FFB347"), 9f, 1.4f, 0.12f);
                    break;
                case GardenCenter.Soil:
                    Shapes.Box(root, c + Vector3.up * 0.75f, new Vector3(2 * L.PitHalf, 1.5f, 2 * L.PitHalf), Soil); // grządka zasypana do poziomu trawnika
                    break;
                // Pan i Table budują same gry (ruchome / wysokie konstrukcje nad grządką)
            }
        }

        /// <summary>Rampa z deski od punktu a do b (szerokość w poprzek).</summary>
        public static GameObject Ramp(Transform parent, Vector3 a, Vector3 b, float width, Color c)
        {
            const float th = 0.3f;
            var dir = b - a;
            var rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            var normal = rot * Vector3.up;
            var go = Shapes.Prim(PrimitiveType.Cube, parent, (a + b) * 0.5f - normal * th * 0.5f, new Vector3(width, th, dir.magnitude + 0.4f), c, true);
            go.transform.localRotation = rot;
            go.name = "Rampa";
            return go;
        }

        static void Wheelbarrow(Transform root, Vector3 pos)
        {
            var go = Shapes.Prim(PrimitiveType.Cube, root, pos, new Vector3(1.4f, 0.7f, 2f), Shapes.Hex("#2E86AB"), true, 0.6f);
            go.name = "Taczka";
            Shapes.Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0, -0.55f, 0.6f), new Vector3(0.5f, 0.1f, 0.5f), Shapes.Hex("#222222"), false).transform.localRotation = Quaternion.Euler(0, 0, 90f);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 3f;
            go.AddComponent<BonkBall>().Power = 0.8f;
        }
    }

    // =====================================================================
    //  Elementy interaktywne ogrodu
    // =====================================================================

    /// <summary>Trampolina: wybija ziemniaka łukiem do punktu docelowego (dach, zbiornik). Zryw w powietrzu = +2 m.</summary>
    public class JumpPad : MonoBehaviour
    {
        public Vector3 Target;
        public float Up = 21f;

        public static JumpPad Build(Transform parent, Vector3 pos, Color c, Vector3 target, float up, string name)
        {
            var go = MapBuilder.Disc(parent, pos + Vector3.up * 0.2f, 1.25f, 0.4f, c);
            go.name = name;
            var trig = new GameObject("Wybicie");
            trig.transform.SetParent(go.transform, false);
            trig.transform.localPosition = Vector3.up * 0.6f;
            var bc = trig.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(1.6f, 1f, 1.6f);
            var jp = trig.AddComponent<JumpPad>();
            jp.Target = target;
            jp.Up = up;
            return jp;
        }

        readonly Dictionary<Potato, float> last = new Dictionary<Potato, float>();

        void OnTriggerEnter(Collider other)
        {
            var p = other.GetComponent<Potato>();
            if (p == null || p.Eliminated) return;
            float t;
            if (last.TryGetValue(p, out t) && Time.time - t < 0.6f) return;
            last[p] = Time.time;
            // pozioma prędkość tak, żeby szczyt łuku wypadł nad celem
            var flat = Target - p.transform.position; flat.y = 0f;
            float g = MotorTuning.Default.Gravity;
            float tApex = Up / g;
            var vFlat = flat / Mathf.Max(0.3f, tApex * 1.15f);
            p.Knock(vFlat + Vector3.up * Up, 0.08f);
            AudioManager.PlayPitched(Sfx.Jump, 0.9f, 0.7f);
            Fx.Dust(transform.parent.parent, transform.position, 0.8f, 6);
        }
    }

    /// <summary>Grabie: nadepnięte zęby podrywają trzonek – ogłuszenie 1.2 s i podrzut. 2 s na "przeładowanie".</summary>
    public class Rake : MonoBehaviour
    {
        Transform handle;
        float cooldown, swing;

        public static Rake Build(Transform parent, Vector3 pos, float yaw)
        {
            var go = Shapes.Empty("Grabie", parent, pos);
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var head = Shapes.Box(go.transform, new Vector3(0, 0.06f, 0), new Vector3(1.2f, 0.08f, 0.25f), Shapes.Hex("#8C8C8C"), false);
            for (int i = -4; i <= 4; i++)
                Shapes.Box(head.transform, new Vector3(i * 0.11f, -0.6f, 0.5f), new Vector3(0.03f, 1.5f, 0.6f), Shapes.Hex("#8C8C8C"), false);
            var pivot = Shapes.Empty("Trzonek", go.transform, new Vector3(0, 0.08f, 0)).transform;
            Shapes.Prim(PrimitiveType.Cylinder, pivot, new Vector3(0, 0, -1.4f), new Vector3(0.09f, 1.4f, 0.09f), Shapes.Hex("#A8743F"), false).transform.localRotation = Quaternion.Euler(90f, 0, 0);
            var trig = go.AddComponent<BoxCollider>();
            trig.isTrigger = true;
            trig.center = new Vector3(0, 0.4f, 0);
            trig.size = new Vector3(1.3f, 0.8f, 0.7f);
            var r = go.AddComponent<Rake>();
            r.handle = pivot;
            return r;
        }

        void OnTriggerEnter(Collider other)
        {
            if (cooldown > 0f) return;
            var p = other.GetComponent<Potato>();
            if (p == null || p.Eliminated) return;
            cooldown = 2f;
            swing = 1f;
            var back = -transform.forward;
            p.Knock(back * 4f + Vector3.up * 9f, 1.2f);
            AudioManager.Play(Sfx.Bump, 1f);
            Feel.EnvironmentHit(p, back, 0.7f);
        }

        void Update()
        {
            cooldown -= Time.deltaTime;
            swing = Mathf.MoveTowards(swing, 0f, Time.deltaTime * 1.4f);
            // trzonek podrywa się do 75° i opada z odbiciem
            float a = 75f * Mathf.Clamp01(swing * 1.4f);
            handle.localRotation = Quaternion.Euler(-a, 0, 0);
        }
    }

    /// <summary>Zraszacz: strumień wody 7 m obraca się (zmiana kierunku co 8 s), pcha 6 m/s i moczy skórkę.</summary>
    public class Sprinkler : MonoBehaviour
    {
        Transform arm;
        float dir = 1f, timer = 8f;

        public static Sprinkler Build(Transform parent, Vector3 pos)
        {
            var go = Shapes.Empty("Zraszacz", parent, pos);
            Shapes.Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0, 0.2f, 0), new Vector3(0.4f, 0.2f, 0.4f), Shapes.Hex("#E2463F"));
            var arm = Shapes.Empty("Strumien", go.transform, new Vector3(0, 0.5f, 0)).transform;
            var water = Shapes.Prim(PrimitiveType.Cube, arm, new Vector3(0, 0.3f, 3.6f), new Vector3(0.35f, 0.25f, 7f), Shapes.Hex("#8FD8FF"), false, 0.95f);
            water.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(Shapes.Hex("#8FD8FF"), 0.95f, 0.4f);
            var bc = water.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(4f, 6f, 1f);
            var s = go.AddComponent<Sprinkler>();
            s.arm = arm;
            water.AddComponent<SprinklerStream>().Owner = s;
            return s;
        }

        public Vector3 Push { get { return arm.forward; } }

        void Update()
        {
            timer -= Time.deltaTime;
            if (timer <= 0f) { timer = 8f; dir = -dir; }
            arm.Rotate(0f, dir * 40f * Time.deltaTime, 0f, Space.Self);
        }
    }

    public class SprinklerStream : MonoBehaviour
    {
        public Sprinkler Owner;

        void OnTriggerStay(Collider other)
        {
            var p = other.GetComponent<Potato>();
            if (p == null || p.Eliminated || p.Body == null || p.Body.isKinematic) return;
            var v = Phys.GetVelocity(p.Body);
            var push = Owner.Push * 6f;
            v.x = Mathf.MoveTowards(v.x, push.x, 30f * Time.fixedDeltaTime);
            v.z = Mathf.MoveTowards(v.z, push.z, 30f * Time.fixedDeltaTime);
            Phys.SetVelocity(p.Body, v);
            if (p.Surface != null) p.Surface.Soak(0.6f * Time.fixedDeltaTime);
        }
    }

    /// <summary>Szyba szklarni: 3 trafienia zrywem i pęka (odłamki), wraca po 20 s.</summary>
    public class GlassPanel : MonoBehaviour
    {
        int hits;
        float back;
        Renderer rend;
        Collider col;

        public static GlassPanel Build(Transform parent, Vector3 pos, Vector3 size)
        {
            var go = Shapes.Prim(PrimitiveType.Cube, parent, pos, size, Shapes.Hex("#CDEFFF"), true, 0.95f);
            go.name = "Szyba";
            go.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(Shapes.Hex("#CDEFFF"), 0.95f, 0.15f);
            var g = go.AddComponent<GlassPanel>();
            g.rend = go.GetComponent<Renderer>();
            g.col = go.GetComponent<Collider>();
            return g;
        }

        void OnCollisionEnter(Collision c)
        {
            var p = c.collider.GetComponent<Potato>();
            if (p == null || !p.IsDashing) return;
            hits++;
            AudioManager.PlayPitched(Sfx.Crack, 0.8f, 1f + hits * 0.15f);
            if (hits >= 3) Break(c.contacts.Length > 0 ? c.contacts[0].point : transform.position);
        }

        void Break(Vector3 at)
        {
            hits = 0;
            back = 20f;
            rend.enabled = false;
            col.enabled = false;
            Fx.Sparks(transform.parent, at, Shapes.Hex("#E6F7FF"), 30, 7f, 0.9f);
            AudioManager.Play(Sfx.Crack, 1f);
        }

        void Update()
        {
            if (back <= 0f) return;
            back -= Time.deltaTime;
            if (back <= 0f) { rend.enabled = true; col.enabled = true; }
        }
    }

    /// <summary>Most nad grządką: podnosi się i opuszcza (dźwignia).</summary>
    public class GateBridge : MonoBehaviour
    {
        public Vector3 Up, Down;
        public bool Raised = true;
        float k = 1f;
        Rigidbody rb;

        void Awake()
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        public void Toggle()
        {
            Raised = !Raised;
            AudioManager.PlayPitched(Sfx.Whoosh, 0.8f, Raised ? 1.2f : 0.8f);
        }

        void FixedUpdate()
        {
            k = Mathf.MoveTowards(k, Raised ? 1f : 0f, Time.fixedDeltaTime / 0.8f);
            var local = Vector3.Lerp(Down, Up, Ease.InOutCubic(k));
            rb.MovePosition(transform.parent != null ? transform.parent.TransformPoint(local) : local);
        }
    }

    /// <summary>Dźwignia: wejście na płytę przełącza most (co najwyżej raz na 1.5 s).</summary>
    public class GateLever : MonoBehaviour
    {
        public GateBridge Bridge;
        float cooldown;

        void OnTriggerEnter(Collider other)
        {
            if (cooldown > 0f || Bridge == null) return;
            var p = other.GetComponent<Potato>();
            if (p == null || p.Eliminated) return;
            cooldown = 1.5f;
            Bridge.Toggle();
        }

        void Update() { cooldown -= Time.deltaTime; }
    }
}
