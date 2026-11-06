using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PotatoParty
{
    /// <summary>Przeszkoda, która coś robi ziemniakowi przy dotknięciu (np. odrzuca go).</summary>
    public class Hazard : MonoBehaviour
    {
        public System.Action<Potato, Hazard> OnTouch;
        readonly Dictionary<Potato, float> lastHit = new Dictionary<Potato, float>();

        void OnTriggerEnter(Collider other) { Touch(other); }
        void OnTriggerStay(Collider other) { Touch(other); }

        void Touch(Collider other)
        {
            var p = other.GetComponent<Potato>();
            if (p == null || p.Eliminated || OnTouch == null) return;
            float t;
            if (lastHit.TryGetValue(p, out t) && Time.time - t < 0.4f) return;
            lastHit[p] = Time.time;
            OnTouch(p, this);
        }

        public static Hazard Add(GameObject go, System.Action<Potato, Hazard> onTouch)
        {
            var col = go.GetComponent<Collider>();
            if (col == null) col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            var h = go.AddComponent<Hazard>();
            h.OnTouch = onTouch;
            return h;
        }
    }

    /// <summary>Obraca obiekt wokół osi Y ze zmienną prędkością.</summary>
    public class Rotor : MonoBehaviour
    {
        public float Speed = 90f; // stopnie na sekundę
        public float Angle;

        void Update()
        {
            Angle += Speed * Time.deltaTime;
            transform.localRotation = Quaternion.Euler(0, Angle, 0);
        }
    }

    /// <summary>Przesuwa obiekt tam i z powrotem.</summary>
    public class Slider : MonoBehaviour
    {
        public Vector3 Axis = Vector3.right;
        public float Distance = 3f;
        public float Speed = 1.2f;
        public float Phase;
        Vector3 start;

        void Start() { start = transform.localPosition; }

        void Update()
        {
            transform.localPosition = start + Axis * Mathf.Sin(Time.time * Speed + Phase) * Distance;
        }
    }

    /// <summary>Wahadło: kołysze się wokół lokalnej osi Z (w poprzek toru).</summary>
    public class Pendulum : MonoBehaviour
    {
        public float Amplitude = 60f;
        public float Speed = 1.6f;
        public float Phase;
        public float Angle { get; private set; }
        /// <summary>Kierunek ruchu głowicy w osi X (+1 / -1).</summary>
        public float SwingDir { get { return Mathf.Sign(Mathf.Cos(Time.time * Speed + Phase)); } }

        void Update()
        {
            Angle = Mathf.Sin(Time.time * Speed + Phase) * Amplitude;
            transform.localRotation = Quaternion.Euler(0, 0, Angle);
        }
    }

    /// <summary>Drzwi w ścianie: prawdziwe przewracają się po dotknięciu, fałszywe odbijają ziemniaka.</summary>
    public class FakeDoor : MonoBehaviour
    {
        public bool Real;
        public bool Opened;
        public bool KnownFake;
        /// <summary>Zamiast przewracać się do przodu, chowa się w ziemię (np. w labiryncie).</summary>
        public bool Sink;
        float fall;
        Vector3 startPos;
        Quaternion startRot;
        float wobble;

        void Start() { startPos = transform.localPosition; startRot = transform.localRotation; }

        void OnCollisionEnter(Collision c)
        {
            var p = c.collider.GetComponent<Potato>();
            if (p == null || Opened) return;
            if (Real)
            {
                Opened = true;
                GetComponent<Collider>().enabled = false;
                AudioManager.Play(Sfx.Crack, 0.8f);
            }
            else
            {
                KnownFake = true;
                wobble = 1f;
                var away = p.transform.position - transform.position;
                away.y = 0;
                if (Sink)
                {
                    // ściana w labiryncie: odbij prostopadle do niej
                    var n = transform.right * Mathf.Sign(Vector3.Dot(away, transform.right));
                    if (transform.lossyScale.x > transform.lossyScale.z) n = transform.forward * Mathf.Sign(Vector3.Dot(away, transform.forward));
                    p.Knock(n * 9f + Vector3.up * 4f, 0.6f);
                }
                else p.Knock(new Vector3(Random.Range(-2f, 2f), 4f, -9f), 0.6f);
                AudioManager.Play(Sfx.Bump, 1f);
            }
        }

        void Update()
        {
            if (Opened && fall < 1f)
            {
                fall = Mathf.MoveTowards(fall, 1f, Time.deltaTime * 2.5f);
                float k = fall * fall;
                if (Sink) transform.localPosition = startPos + Vector3.down * 1.75f * k;
                else
                {
                    transform.localRotation = startRot * Quaternion.Euler(85f * k, 0, 0);
                    transform.localPosition = startPos + new Vector3(0, -1.2f * k, 1.2f * k);
                }
            }
            else if (wobble > 0f)
            {
                wobble = Mathf.MoveTowards(wobble, 0f, Time.deltaTime * 2f);
                transform.localRotation = startRot * Quaternion.Euler(Mathf.Sin(Time.time * 40f) * 6f * wobble, 0, 0);
            }
        }
    }

    /// <summary>Kamień, który chwilę po wejściu na niego trzęsie się i spada, a potem wraca.</summary>
    public class CrumbleStone : MonoBehaviour
    {
        public float Delay = 0.7f;
        public float Return = 3f;
        float timer = -1f, down;
        Vector3 start;
        Collider col;
        Renderer[] rends;
        public bool Solid { get { return timer < 0f || timer < Delay; } }

        void Start()
        {
            start = transform.localPosition;
            // Disc() podmienia kapsułę na MeshCollider, a stara kapsuła jest jeszcze w trakcie usuwania
            foreach (var c in GetComponents<Collider>()) if (c != null && c.enabled) col = c;
            rends = GetComponentsInChildren<Renderer>();
        }

        public void Step() { if (timer < 0f) timer = 0f; }

        void Update()
        {
            if (timer < 0f) return;
            timer += Time.deltaTime;
            if (timer < Delay)
            {
                transform.localPosition = start + new Vector3(Mathf.Sin(Time.time * 60f) * 0.06f, 0, 0);
            }
            else if (timer < Delay + Return)
            {
                if (col != null && col.enabled) { col.enabled = false; AudioManager.Play(Sfx.Crack, 0.5f); }
                down += Time.deltaTime * 8f;
                transform.localPosition = start + Vector3.down * down;
                foreach (var r in rends) r.enabled = down < 12f;
            }
            else
            {
                timer = -1f;
                down = 0f;
                transform.localPosition = start;
                if (col != null) col.enabled = true;
                foreach (var r in rends) r.enabled = true;
            }
        }
    }

    /// <summary>Toczona kula: uderzony ziemniak robi fikołka.</summary>
    public class BonkBall : MonoBehaviour
    {
        public float Power = 1f;
        Rigidbody rb;
        void Awake() { rb = GetComponent<Rigidbody>(); }

        void OnCollisionEnter(Collision c)
        {
            var p = c.collider.GetComponent<Potato>();
            if (p == null || rb == null) return;
            var v = Phys.GetVelocity(rb);
            v.y = 0;
            if (v.magnitude < 2f) return;
            p.Knock(v * 0.9f * Power + Vector3.up * 5f, 0.7f);
            AudioManager.Play(Sfx.Bump, 0.8f);
        }
    }

    /// <summary>
    /// Baza wyścigów (tor przeszkód, labirynt): kolejność na mecie, a reszta według postępu.
    /// </summary>
    public abstract class RaceGame : Minigame
    {
        public override bool FallEliminates { get { return false; } }
        public override bool EndsWhenOneLeft { get { return false; } }
        public override float CameraPull { get { return 0f; } }

        protected readonly List<Potato> FinishOrder = new List<Potato>();
        float graceLeft = -1f;
        protected virtual float Grace { get { return 15f; } }

        /// <summary>Im większy postęp, tym bliżej mety (do rankingu i karty gracza).</summary>
        protected abstract float Progress(Potato p);
        protected abstract bool CrossedFinish(Potato p);

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (Finished || !Running) return;
            foreach (var p in Players)
            {
                if (p == null || p.Eliminated || FinishOrder.Contains(p)) continue;
                Score[p] = Mathf.Max(ScoreOf(p), Mathf.RoundToInt(Progress(p)));
                if (CrossedFinish(p))
                {
                    FinishOrder.Add(p);
                    Effects.Confetti(p.transform.parent, p.transform.position, FinishOrder.Count == 1 ? 160 : 70);
                    AudioManager.Play(Sfx.Fanfare, 0.6f);
                    GM.Hud.Toast(p.Slot.Name + " na mecie! (" + FinishOrder.Count + ". miejsce)", p.Color);
                    p.Frozen = true;
                    if (graceLeft < 0f) graceLeft = Grace;
                }
            }
            if (graceLeft >= 0f)
            {
                graceLeft -= dt;
                if (graceLeft <= 0f || FinishOrder.Count >= AliveCount) Finish();
            }
        }

        public override List<List<Potato>> Ranking()
        {
            var result = FinishOrder.Select(p => new List<Potato> { p }).ToList();
            var rest = Players.Where(p => p != null && !FinishOrder.Contains(p)).OrderByDescending(Progress).ToList();
            foreach (var p in rest) result.Add(new List<Potato> { p });
            return result;
        }

        public override string StatFor(Potato p)
        {
            int i = FinishOrder.IndexOf(p);
            return i >= 0 ? "META #" + (i + 1) : "";
        }

        public override string HudInfo()
        {
            string s = base.HudInfo();
            if (graceLeft >= 0f) s += "   ·   Koniec za " + Mathf.CeilToInt(graceLeft) + " s";
            return s;
        }
    }
}
