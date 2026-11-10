using UnityEngine;

namespace PotatoParty
{
    /// <summary>
    /// Strojenie ruchu. Wartości dobrane pod 100 Hz fizyki (TimeDirector.BaseFixedDelta = 0.01).
    /// Skok jest zdefiniowany <b>wysokością i czasem do szczytu</b>, a nie siłą – grawitacja i prędkość
    /// wybicia wynikają z nich (g = 2h/t², v0 = 2h/t), więc projektant stroi to, co gracz czuje.
    /// </summary>
    [System.Serializable]
    public class MotorTuning
    {
        [Header("Bieg")]
        public float GroundAccel = 72f;        // m/s² – 0 → 7.5 m/s w ~0.1 s
        public float GroundDecel = 58f;        // m/s² bez wejścia (× grip²)
        public float TurnAccel = 120f;         // m/s² przy zawracaniu (dot < -0.3): ostry "skid"
        public float AirAccel = 30f;
        public float AirDecel = 5f;            // w powietrzu pęd zostaje
        public float StickExponent = 1.35f;    // krzywa gałki: precyzja przy małym wychyleniu
        public float StunControl = 0.12f;      // ułamek przyspieszenia w ogłuszeniu
        public float TurnRate = 900f;          // °/s obrotu postaci

        [Header("Skok")]
        public float JumpHeight = 1.45f;       // m (pełne przytrzymanie)
        public float TimeToApex = 0.30f;       // s
        public float FallGravityMul = 1.65f;   // szybki opad: skok "waży"
        public float JumpCutGravityMul = 2.4f; // puszczony przycisk w locie w górę → niższy skok (min ≈ 0.55 m)
        public float ApexHangSpeed = 1.4f;     // |vy| poniżej → zawis
        public float ApexHangGravityMul = 0.55f;
        public float TerminalVelocity = 26f;
        public float CoyoteTime = 0.10f;
        public float JumpBuffer = 0.13f;
        public float GroundStick = 30f;        // m/s² docisku do podłoża (zjazd z rampy bez "odlotu")
        public float KnockGravity = 26f;       // m/s² w locie po ciosie: dłuższy, czytelny łuk (balans minigier jak dotąd)

        [Header("Zryw (akcja)")]
        public float DashStartup = 0f;         // s antycypacji – 0 = wystrzał w tej samej klatce co przycisk
        public float DashActive = 0.15f;       // s pełnej prędkości – tylko wtedy trafienia liczą się jako cios
        public float DashRecovery = 0.1f;      // s wyhamowania, sterowanie 60%
        public float DashCooldown = 0.7f;     // s od początku zrywu
        public float DashBuffer = 0.1f;
        public float AirDashLift = 1.5f;       // m/s minimalnej prędkości pionowej przy zrywie w powietrzu

        public float Gravity { get { return 2f * JumpHeight / (TimeToApex * TimeToApex); } }
        public float JumpVelocity { get { return 2f * JumpHeight / TimeToApex; } }

        public static readonly MotorTuning Default = new MotorTuning();
    }

    /// <summary>Faza zrywu (klatki "wejście / aktywne / powrót" jak w bijatykach).</summary>
    public enum DashPhase { None, Startup, Active, Recovery }

    /// <summary>
    /// Silnik ruchu ziemniaka – napisany od zera. Własna grawitacja (Rigidbody.useGravity = false),
    /// bufor skoku i zrywu, coyote time, zmienna wysokość skoku, zawis na szczycie, szybki opad,
    /// zawracanie ze "skidem", zryw w trzech fazach i jeden zryw w powietrzu na skok.
    /// Wejście podaje <see cref="Potato"/> (gracz albo bot), zdarzenia trafiają do <see cref="PotatoBody"/>.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public sealed class PotatoMotor : MonoBehaviour
    {
        public MotorTuning Tuning = MotorTuning.Default;

        [System.NonSerialized] public Potato Owner;
        [System.NonSerialized] public Rigidbody Body;

        // ---------------- wejście (ustawia Potato co klatkę) ----------------
        [System.NonSerialized] public Vector2 Move;
        [System.NonSerialized] public bool JumpHeld;

        // ---------------- stan (czyta Potato i minigry) ----------------
        public bool Grounded { get; private set; }
        public Collider GroundCollider { get; private set; }
        public Vector3 GroundNormal { get; private set; }
        public DashPhase Dash { get; private set; }
        public Vector3 DashDir { get; private set; }
        public float DashCooldownLeft { get { return Mathf.Max(0f, cooldown); } }
        public float StunLeft { get { return Mathf.Max(0f, stun); } }
        public Vector3 Facing = Vector3.back;
        /// <summary>Przyspieszenie poziome z ostatniego kroku [m/s²] – do przechyłu postaci.</summary>
        public Vector3 Acceleration { get; private set; }
        /// <summary>Czy w tej chwili trwa ostre zawracanie (pył spod stóp, przechył w tył).</summary>
        public bool Skidding { get; private set; }

        float jumpBuffered = -1f, dashBuffered = -1f;
        float coyote, cooldown, stun, phaseTime;
        bool jumping, airDashUsed;
        Vector3 lastFlatVel;

        public void QueueJump() { jumpBuffered = Tuning.JumpBuffer; }
        public void QueueAction() { dashBuffered = Tuning.DashBuffer; }
        public void ClearInput() { jumpBuffered = dashBuffered = -1f; Move = Vector2.zero; JumpHeld = false; }

        /// <summary>Uderzenie z zewnątrz: nadpisuje prędkość poziomą, podbija pionową, przerywa zryw.</summary>
        public void ApplyKnock(Vector3 impulse, float stunTime)
        {
            if (Body == null || Body.isKinematic) return;
            var v = Phys.GetVelocity(Body);
            v.x = impulse.x;
            v.z = impulse.z;
            v.y = Mathf.Max(v.y, impulse.y);
            Phys.SetVelocity(Body, v);
            stun = Mathf.Max(stun, stunTime);
            if (Dash != DashPhase.None) { Dash = DashPhase.None; phaseTime = 0f; }
            jumping = false;
        }

        /// <summary>Manekin w garderobie: bez fizyki, ale z pozą "na ziemi".</summary>
        public void ForceGrounded(bool grounded) { Grounded = grounded; }

        public void ResetState()
        {
            Dash = DashPhase.None;
            stun = 0f;
            cooldown = 0f;
            jumping = false;
            airDashUsed = false;
            ClearInput();
        }

        void FixedUpdate()
        {
            if (Body == null || Body.isKinematic || Owner == null) return;
            float dt = Time.fixedDeltaTime;
            var T = Tuning;
            bool locked = Owner.Frozen || Owner.Eliminated;

            ProbeGround();

            jumpBuffered -= dt;
            dashBuffered -= dt;
            cooldown -= dt;
            stun -= dt;
            coyote = Grounded && !jumping ? T.CoyoteTime : coyote - dt;
            if (Grounded) airDashUsed = false;
            if (locked) { jumpBuffered = dashBuffered = -1f; }

            Vector3 v = Phys.GetVelocity(Body);
            Vector2 raw = locked ? Vector2.zero : Move;
            float mag = Mathf.Clamp01(raw.magnitude);
            Vector3 input = mag > 0.001f ? new Vector3(raw.x, 0f, raw.y) / raw.magnitude * Mathf.Pow(mag, T.StickExponent) : Vector3.zero;
            bool stunned = stun > 0f;

            // ---------------- zryw ----------------
            if (dashBuffered > 0f && Dash == DashPhase.None && Owner.CanDash && cooldown <= 0f && !stunned && (Grounded || !airDashUsed))
            {
                dashBuffered = -1f;
                DashDir = input.sqrMagnitude > 0.01f ? input.normalized : Facing;
                Facing = DashDir;
                Dash = DashPhase.Startup;
                phaseTime = 0f;
                cooldown = T.DashCooldown;
                if (!Grounded) airDashUsed = true;
                Owner.OnDashStart(DashDir);
                if (T.DashStartup <= 0f) { Dash = DashPhase.Active; Owner.OnDashLaunch(DashDir); }
            }

            if (Dash != DashPhase.None)
            {
                phaseTime += dt;
                Vector3 flat = new Vector3(v.x, 0f, v.z);
                switch (Dash)
                {
                    case DashPhase.Startup:
                        // antycypacja: przez 35 ms hamujemy wykładniczo do ~35% prędkości ("przykucnięcie" przed wystrzałem)
                        flat *= Mathf.Exp(-30f * dt);
                        if (phaseTime >= T.DashStartup) { Dash = DashPhase.Active; phaseTime = 0f; Owner.OnDashLaunch(DashDir); }
                        break;
                    case DashPhase.Active:
                        flat = DashDir * Owner.DashSpeed;
                        if (!Grounded) v.y = Mathf.Max(v.y, T.AirDashLift);
                        if (phaseTime >= T.DashActive) { Dash = DashPhase.Recovery; phaseTime = 0f; }
                        break;
                    case DashPhase.Recovery:
                        var steer = input * Owner.MoveSpeed * Owner.SpeedMultiplier;
                        float k = phaseTime / Mathf.Max(0.001f, T.DashRecovery);
                        flat = Vector3.Lerp(DashDir * Owner.DashSpeed, Vector3.Lerp(DashDir * Owner.MoveSpeed, steer, 0.6f), Mathf.SmoothStep(0f, 1f, k));
                        if (phaseTime >= T.DashRecovery) { Dash = DashPhase.None; phaseTime = 0f; }
                        break;
                }
                v.x = flat.x;
                v.z = flat.z;
                Skidding = false;
            }
            else
            {
                // ---------------- bieg ----------------
                Vector3 desired = input * Owner.MoveSpeed * Owner.SpeedMultiplier + (Grounded ? Owner.Push : Vector3.zero);
                Vector3 flat = new Vector3(v.x, 0f, v.z);
                float grip = Owner.Grip;
                float accel;
                bool hasInput = input.sqrMagnitude > 0.0004f;
                float along = hasInput && flat.sqrMagnitude > 1f ? Vector3.Dot(flat.normalized, input.normalized) : 1f;
                Skidding = Grounded && hasInput && along < -0.3f && flat.magnitude > 3f;
                if (Grounded)
                {
                    if (!hasInput && Owner.Push.sqrMagnitude < 0.01f) accel = T.GroundDecel * grip * grip + 2f;
                    else if (Skidding) accel = T.TurnAccel * grip;
                    else accel = T.GroundAccel * grip;
                }
                else accel = hasInput ? T.AirAccel : T.AirDecel;
                if (stunned) accel *= T.StunControl;
                flat = Vector3.MoveTowards(flat, desired, accel * dt);
                v.x = flat.x;
                v.z = flat.z;
            }

            // ---------------- skok ----------------
            if (jumpBuffered > 0f && coyote > 0f && Owner.CanJump && !stunned && Dash != DashPhase.Active)
            {
                jumpBuffered = -1f;
                coyote = 0f;
                jumping = true;
                v.y = T.JumpVelocity;
                Grounded = false;
                Owner.OnJump();
            }

            // ---------------- grawitacja ----------------
            float g = T.Gravity;
            if (Dash == DashPhase.Active) g = 0f;                         // zryw jest płaski
            else if (v.y < 0f) g *= T.FallGravityMul;
            else if (jumping && !JumpHeld) g *= T.JumpCutGravityMul;
            if (jumping && JumpHeld && Mathf.Abs(v.y) < T.ApexHangSpeed) g *= T.ApexHangGravityMul;
            if (stunned && !Grounded && Dash == DashPhase.None) g = T.KnockGravity;
            if (Grounded && !jumping && v.y <= 0.5f) g += T.GroundStick;
            v.y -= g * dt;
            if (v.y < -T.TerminalVelocity) v.y = -T.TerminalVelocity;
            if (jumping && v.y <= 0f && Grounded) jumping = false;

            Phys.SetVelocity(Body, v);

            // ---------------- kierunek patrzenia i przyspieszenie ----------------
            Vector3 flatNow = new Vector3(v.x, 0f, v.z);
            Acceleration = dt > 0f ? (flatNow - lastFlatVel) / dt : Vector3.zero;
            lastFlatVel = flatNow;
            Vector3 lookDir = Dash != DashPhase.None ? DashDir : (input.sqrMagnitude > 0.01f ? input : (flatNow.sqrMagnitude > 0.5f ? flatNow : Facing));
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 1e-4f && !stunned)
                Facing = Vector3.RotateTowards(Facing, lookDir.normalized, T.TurnRate * Mathf.Deg2Rad * dt, 0f);
        }

        void ProbeGround()
        {
            RaycastHit hit;
            int mask = ~(1 << Potato.Layer);
            bool was = Grounded;
            float vyBefore = Phys.GetVelocity(Body).y;
            const float r = 0.38f;
            bool hitGround = Physics.SphereCast(Body.position, r, Vector3.down, out hit, Potato.HalfHeight - r + 0.12f, mask, QueryTriggerInteraction.Ignore);
            // wznoszący się skok nie "przykleja się" do krawędzi platformy, przez którą przelatuje
            Grounded = hitGround && hit.normal.y > 0.55f && !(jumping && vyBefore > 0.5f);
            GroundCollider = Grounded ? hit.collider : null;
            GroundNormal = Grounded ? hit.normal : Vector3.up;
            if (Grounded && !was)
            {
                jumping = false;
                Owner.OnLand(-vyBefore, hit.point);
            }
        }
    }
}
