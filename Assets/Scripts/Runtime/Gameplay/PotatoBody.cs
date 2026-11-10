using UnityEngine;

namespace PotatoParty
{
    /// <summary>
    /// Proceduralna animacja ziemniaka: squash &amp; stretch, przechył od przyspieszenia, cykl biegu,
    /// kończyny, mimika, fikołki i drżenie ofiary w hit-stopie. Nie ma tu żadnej logiki ruchu –
    /// wszystko wynika ze stanu <see cref="PotatoMotor"/> i zdarzeń (skok, lądowanie, zryw, cios).
    /// <para><b>Squash &amp; stretch</b> ma dwie niezależne warstwy, obie z zachowaniem objętości (boki = 1/√oś):</para>
    /// <list type="number">
    /// <item><b>Pion</b> (skala Visual): lądowanie spłaszcza, wybicie i lot w górę wydłużają. Sprężyna 5 Hz, ζ 0.32.</item>
    /// <item><b>Dowolna oś</b> (łańcuch obrót → skala → obrót⁻¹ pod Visual): cios, zryw, zderzenie.
    ///       Deformuje się cała postać razem z oczami i kapeluszem. Sprężyna 4.2 Hz, ζ 0.26.</item>
    /// </list>
    /// </summary>
    public sealed class PotatoBody : MonoBehaviour
    {
        // ---------------- strojenie (Inspektor) ----------------
        [Header("Pion: lądowanie / skok")]
        public float SquashFrequency = 5f;
        public float SquashDamping = 0.32f;
        public float LandSquashPerSpeed = 0.022f; // na m/s prędkości upadku
        public float LandSquashMax = 0.38f;
        public float TakeoffStretch = 0.22f;
        public float AirStretchPerSpeed = 0.012f; // na m/s prędkości pionowej

        [Header("Oś dowolna: ciosy i zryw")]
        public float JellyFrequency = 4.2f;
        public float JellyDamping = 0.26f;
        public float DashAnticipation = 0.2f;     // zgniecenie wzdłuż kierunku w fazie startup
        public float DashStretch = 0.3f;          // rozciągnięcie przy wystrzale

        [Header("Przechył")]
        public float LeanPerAccel = 0.26f;        // ° na m/s² przyspieszenia do przodu
        public float LeanMax = 20f;
        public float RollPerAccel = 0.2f;
        public float RollMax = 15f;
        public float LeanFrequency = 3.4f;
        public float LeanDamping = 0.55f;

        [Header("Drżenie ofiary (czas rzeczywisty)")]
        public float HitShakeTime = 0.14f;
        public float HitShakeAmplitude = 0.08f;
        public float HitShakeHz = 55f;

        // ---------------- elementy postaci (ustawia Potato.BuildVisual) ----------------
        [System.NonSerialized] public Potato Owner;
        [System.NonSerialized] public Transform Visual;
        [System.NonSerialized] public Transform[] Eyes, Arms, Feet;
        [System.NonSerialized] public GameObject MouthSmile, MouthO;
        [System.NonSerialized] public Transform Dizzy;
        [System.NonSerialized] public float VisualScale = 1.3f;

        Transform jellyA, jellyS, jellyB;
        Vector3 jellyAxis = Vector3.forward;
        Spring squash, jelly, leanPitch, leanRoll;
        float tumble, tumbleSpeed, waddle, blinkTimer = 2f, blink, hitShake, stepTimer;
        Vector3 hitShakeDir = Vector3.right;
        float yaw = 180f;

        public void Init()
        {
            BuildJellyChain();
            blinkTimer = Random.Range(1.5f, 4f);
        }

        /// <summary>Visual → A (obrót do osi) → S (skala wzdłuż osi) → B (obrót odwrotny) → części ciała.</summary>
        void BuildJellyChain()
        {
            jellyA = new GameObject("Galaretka").transform;
            jellyA.SetParent(Visual, false);
            jellyS = new GameObject("Galaretka skala").transform;
            jellyS.SetParent(jellyA, false);
            jellyB = new GameObject("Galaretka powrot").transform;
            jellyB.SetParent(jellyS, false);
            var kids = new System.Collections.Generic.List<Transform>(Visual.childCount);
            for (int i = 0; i < Visual.childCount; i++)
            {
                var ch = Visual.GetChild(i);
                if (ch != jellyA) kids.Add(ch);
            }
            for (int i = 0; i < kids.Count; i++) kids[i].SetParent(jellyB, false);
        }

        // ---------------- zdarzenia ----------------

        public void SnapYaw(Vector3 facing)
        {
            facing.y = 0f;
            if (facing.sqrMagnitude < 1e-4f) return;
            yaw = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
            if (Visual != null) Visual.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        public void OnJump() { squash.Set(-TakeoffStretch); }

        public void OnLand(float fallSpeed)
        {
            squash.Set(Mathf.Max(squash.Value, Mathf.Clamp(fallSpeed * LandSquashPerSpeed, 0.06f, LandSquashMax)));
            // lądowanie "dobija" przechył do przodu – ciężar ciała idzie w stopy
            leanPitch.Kick(fallSpeed * 6f);
        }

        public void OnDashStart(Vector3 dir) { Jelly(dir, DashAnticipation); }
        public void OnDashLaunch(Vector3 dir) { Jelly(dir, -DashStretch); leanPitch.Kick(160f); }

        /// <summary>Cios: kierunkowe zgniecenie, drżenie w hit-stopie, opcjonalny fikołek.</summary>
        public void OnImpact(Vector3 dir, float power01, bool tumbleOn)
        {
            Jelly(dir, 0.12f + 0.26f * power01);
            hitShake = HitShakeTime;
            var flat = new Vector3(dir.x, 0f, dir.z);
            hitShakeDir = flat.sqrMagnitude > 1e-4f ? flat.normalized : Vector3.right;
            // ciało odchyla się OD ciosu (głowa zostaje z tyłu)
            var local = Quaternion.Euler(0f, -yaw, 0f) * flat;
            leanPitch.Kick(-local.z * 260f * power01);
            leanRoll.Kick(local.x * 260f * power01);
            if (tumbleOn) tumbleSpeed = Random.value < 0.5f ? 720f : -720f;
        }

        /// <summary>Lekkie stuknięcie (bez ciosu).</summary>
        public void OnBump() { squash.Set(Mathf.Max(squash.Value, 0.12f)); }

        void Jelly(Vector3 worldDir, float value)
        {
            if (Visual == null || worldDir.sqrMagnitude < 1e-4f) return;
            jellyAxis = Visual.InverseTransformDirection(worldDir.normalized);
            jelly.Set(value); // od razu – deformacja widoczna już w klatce hit-stopu
        }

        // ---------------- klatka ----------------

        void Update()
        {
            if (Owner == null || Visual == null) return;
            float dt = Time.deltaTime;
            var motor = Owner.Motor;
            Vector3 vel = Owner.Body != null && !Owner.Body.isKinematic ? Phys.GetVelocity(Owner.Body) : Vector3.zero;
            Vector3 flat = new Vector3(vel.x, 0f, vel.z);
            float speed = flat.magnitude;
            bool grounded = Owner.Grounded;
            bool dashing = motor != null && motor.Dash != DashPhase.None;
            float run = Mathf.Clamp01(speed / 6.5f);

            // --- kierunek
            Vector3 facing = Owner.Facing;
            if (facing.sqrMagnitude > 1e-4f)
            {
                float targetYaw = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
                yaw = Mathf.MoveTowardsAngle(yaw, targetYaw, 1400f * dt);
            }

            // --- pion: squash + rozciąganie w locie
            squash.Step(0f, SquashFrequency, SquashDamping, dt);
            float airStretch = grounded ? 0f : Mathf.Clamp(vel.y * AirStretchPerSpeed, -0.14f, 0.2f);
            float sy = Mathf.Clamp(1f - squash.Value + airStretch, 0.58f, 1.4f);
            float sxz = 1f / Mathf.Sqrt(sy);

            // --- oś dowolna
            ApplyJelly(dt);

            // --- przechył: przyspieszenie w przestrzeni postaci
            Vector3 acc = motor != null ? motor.Acceleration : Vector3.zero;
            var toLocal = Quaternion.Euler(0f, -yaw, 0f);
            Vector3 accLocal = toLocal * acc;
            float pitchTarget = Mathf.Clamp(accLocal.z * LeanPerAccel, -LeanMax, LeanMax) + run * 7f;
            float rollTarget = Mathf.Clamp(-accLocal.x * RollPerAccel, -RollMax, RollMax);
            if (motor != null && motor.Skidding) pitchTarget = -16f;               // zawracanie: odchył w tył
            if (dashing) pitchTarget = 26f;
            leanPitch.Step(pitchTarget, LeanFrequency, LeanDamping, dt);
            leanRoll.Step(rollTarget, LeanFrequency, LeanDamping, dt);

            // --- cykl biegu
            waddle += dt * (grounded ? speed * 2.3f : 0f);
            float tilt = grounded ? Mathf.Sin(waddle) * run * 8f : 0f;
            float bob = grounded ? Mathf.Abs(Mathf.Sin(waddle)) * 0.09f * run : 0f;

            Vector3 lp = new Vector3(0f, bob + (VisualScale - 1f) * 0.72f - squash.Value * 0.18f, 0f);
            if (hitShake > 0f)
            {
                // drżenie w czasie rzeczywistym – gra w hit-stopie, gdy reszta świata stoi
                hitShake -= Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(hitShake / HitShakeTime);
                float w = Mathf.Sin(Time.unscaledTime * HitShakeHz * 6.2831853f) * HitShakeAmplitude * k * k;
                lp += Owner.transform.InverseTransformDirection(hitShakeDir) * w;
            }
            Visual.localPosition = lp;
            Visual.localScale = new Vector3(VisualScale * sxz, VisualScale * sy, VisualScale * sxz);

            // --- fikołek
            if (tumbleSpeed != 0f)
            {
                tumble += tumbleSpeed * dt;
                if (grounded && !Owner.IsStunned)
                {
                    float target = Mathf.Round(tumble / 360f) * 360f;
                    tumble = Mathf.MoveTowards(tumble, target, 900f * dt);
                    if (Mathf.Approximately(tumble, target)) { tumble = 0f; tumbleSpeed = 0f; }
                }
            }
            Visual.localRotation = Quaternion.Euler(leanPitch.Value + tumble, yaw, leanRoll.Value + tilt);

            AnimateLimbs(dt, run, vel.y, grounded, dashing);
            Face(dt, vel.y, grounded);
            Trail(dt, speed, grounded, dashing, motor != null && motor.Skidding);

            bool showDizzy = Owner.Motor != null && Owner.Motor.StunLeft > 0.25f && !Owner.Eliminated && Visual.gameObject.activeSelf;
            if (Dizzy != null)
            {
                if (Dizzy.gameObject.activeSelf != showDizzy) Dizzy.gameObject.SetActive(showDizzy);
                if (showDizzy) Dizzy.localRotation = Quaternion.Euler(0f, Time.time * 400f, 0f);
            }
        }

        void ApplyJelly(float dt)
        {
            if (jellyA == null) return;
            jelly.Step(0f, JellyFrequency, JellyDamping, dt);
            float j = Mathf.Clamp(jelly.Value, -0.32f, 0.42f);
            if (Mathf.Abs(j) < 0.001f && Mathf.Abs(jelly.Velocity) < 0.01f)
            {
                if (jellyS.localScale != Vector3.one)
                {
                    jelly.Reset();
                    jellyA.localRotation = Quaternion.identity;
                    jellyS.localScale = Vector3.one;
                    jellyB.localRotation = Quaternion.identity;
                }
                return;
            }
            var a = Quaternion.FromToRotation(Vector3.up, jellyAxis);
            float along = 1f - j;
            float side = 1f / Mathf.Sqrt(along);
            jellyA.localRotation = a;
            jellyS.localScale = new Vector3(side, along, side);
            jellyB.localRotation = Quaternion.Inverse(a);
        }

        void AnimateLimbs(float dt, float run, float vy, bool grounded, bool dashing)
        {
            if (Arms == null || Feet == null) return;
            float t = Time.time;
            bool stunned = Owner.IsStunned || Owner.Eliminated;
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Quaternion target;
                if (stunned)
                    target = Quaternion.Euler(Mathf.Sin(t * 22f + i * 2f) * 50f, 0f, side * (110f + Mathf.Sin(t * 28f + i) * 40f));
                else if (dashing)
                    target = Quaternion.Euler(-75f, 0f, side * 25f);              // ręce w tył: aerodynamiczny "pocisk"
                else if (!grounded)
                    target = Quaternion.Euler(-20f, 0f, side * (vy > 0f ? 145f : 100f) + Mathf.Sin(t * 18f) * 10f);
                else
                    target = Quaternion.Euler(Mathf.Sin(waddle + i * Mathf.PI) * 60f * run, 0f, side * (12f + run * 12f));
                Arms[i].localRotation = Quaternion.Slerp(Arms[i].localRotation, target, 1f - Mathf.Exp(-dt * 20f));

                var f = Feet[i];
                float phase = waddle + i * Mathf.PI;
                float step = grounded ? Mathf.Sin(phase) * 0.18f * run : (i == 0 ? 0.12f : -0.08f);
                float lift = grounded ? Mathf.Max(0f, Mathf.Cos(phase)) * 0.11f * run : 0.05f;
                f.localPosition = new Vector3(0.21f * side, -0.6f + lift, 0.06f + step);
            }
        }

        void Face(float dt, float vy, bool grounded)
        {
            if (Eyes == null) return;
            blinkTimer -= dt;
            if (blinkTimer <= 0f) { blink = 0.14f; blinkTimer = Random.Range(2f, 5f); }
            blink -= dt;
            bool scared = Owner.IsStunned || (!grounded && Mathf.Abs(vy) > 8f) || Owner.Eliminated;
            float eyeY = blink > 0f ? 0.03f : (scared ? 0.32f : 0.27f);
            for (int i = 0; i < Eyes.Length; i++)
            {
                var sc = Eyes[i].localScale;
                Eyes[i].localScale = new Vector3(sc.x, Mathf.MoveTowards(sc.y, eyeY, dt * 4f), sc.z);
            }
            if (MouthO != null && MouthO.activeSelf != scared) { MouthO.SetActive(scared); MouthSmile.SetActive(!scared); }
        }

        void Trail(float dt, float speed, bool grounded, bool dashing, bool skidding)
        {
            if (!grounded || Owner.Eliminated || !Visual.gameObject.activeSelf) return;
            stepTimer -= dt;
            if (stepTimer > 0f) return;
            var feetPos = Owner.transform.position + Vector3.down * (Potato.HalfHeight - 0.08f);
            if (dashing) { Vfx.DashTrail(Owner.transform.parent, feetPos, Owner.Facing, Owner.Color); stepTimer = 0.04f; }
            else if (skidding) { Vfx.Skid(Owner.transform.parent, feetPos, -Owner.Facing); stepTimer = 0.06f; }
            else if (speed > 6f) { Fx.Dust(Owner.transform.parent, feetPos, 0.35f, 2); stepTimer = 0.26f; }
        }
    }
}
