using UnityEngine;

namespace PotatoParty
{
    /// <summary>Polecenie dla bota: dokąd biec i czy użyć akcji/skoku.</summary>
    public struct BotCommand
    {
        public Vector3 Target;
        public bool Action;
        public bool Jump;
        public bool Idle;

        public static BotCommand Go(Vector3 target, bool action = false, bool jump = false)
        {
            return new BotCommand { Target = target, Action = action, Jump = jump };
        }

        public static BotCommand Stay() { return new BotCommand { Idle = true }; }
    }

    /// <summary>Ziemniaczek: postać gracza lub bota. Ruch, skok, zryw (akcja) i popychanie.</summary>
    public class Potato : MonoBehaviour
    {
        public const int Layer = 2; // "Ignore Raycast": raycasty podłoża nie trafiają w innych graczy
        public const float HalfHeight = 0.65f;
        const float VisualScale = 1.3f;

        public PlayerSlot Slot;
        public Rigidbody Body;
        public Transform Visual;
        public Transform HeadAnchor;

        public bool Frozen = true;
        public bool Eliminated;
        public float Grip = 1f;
        public float SpeedMultiplier = 1f;
        public float MoveSpeed = 7.5f;
        public float DashSpeed = 17f;
        public float DashPower = 13f;
        public bool CanDash = true;
        public bool CanJump = true;
        /// <summary>Zewnętrzne "pchanie" podłoża (np. taśmociąg). Minigra ustawia je co klatkę.</summary>
        public Vector3 Push;

        public System.Func<Potato, BotCommand> BotBrain;

        public Vector3 Facing = Vector3.back;
        public bool Grounded { get; private set; }
        public Collider GroundCollider { get; private set; }
        public bool IsDashing { get { return dashTimer > 0f; } }
        public bool IsStunned { get { return stunTimer > 0f; } }
        public float DashCooldownLeft { get { return dashCooldown; } }
        public Color Color { get { return Slot != null ? Slot.Color : Color.white; } }

        Vector2 moveInput;
        bool jumpQueued, actionQueued;
        float dashTimer, dashCooldown, stunTimer, coyote, botThinkTimer, waddle, squash;
        BotCommand botCmd;
        Vector3 baseVisualScale = Vector3.one;
        float tumble, tumbleSpeed, yaw = 180f;
        Transform[] eyes, arms, feet;
        GameObject mouthSmile, mouthO;
        float blinkTimer, blink;
        Transform dizzy;

        public static Potato Create(PlayerSlot slot, Transform parent, Vector3 position)
        {
            var go = new GameObject("Ziemniak " + slot.Name);
            go.layer = Layer;
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.5f;
            col.height = HalfHeight * 2f;
            Phys.Slippery(col);

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Phys.SetDamping(rb, 0f);

            var p = go.AddComponent<Potato>();
            p.Slot = slot;
            p.Body = rb;
            p.BuildVisual();
            return p;
        }

        void BuildVisual()
        {
            // Gładki, błyszczący ziemniaczek z dużą buzią, rączkami i nóżkami
            var c = Color;
            var body = Color.Lerp(c, Color.white, 0.08f);
            var dark = Color.Lerp(c, Color.black, 0.25f);
            var skin = Shapes.Hex("#FFD9A8");
            Visual = Shapes.Empty("Wyglad", transform, Vector3.zero).transform;

            var torso = Shapes.Prim(PrimitiveType.Sphere, Visual, new Vector3(0, 0.02f, 0), new Vector3(1.02f, 1.32f, 0.96f), body, false, 0.7f, "Cialo");
            Shapes.AddOutline(torso);
            // jaśniejszy brzuszek i "plamki ziemniaka"
            Shapes.Prim(PrimitiveType.Sphere, Visual, new Vector3(0, -0.22f, 0.3f), new Vector3(0.7f, 0.72f, 0.4f), Color.Lerp(c, Color.white, 0.5f), false, 0.6f);
            Shapes.Prim(PrimitiveType.Sphere, Visual, new Vector3(0.4f, -0.1f, -0.25f), new Vector3(0.16f, 0.16f, 0.1f), dark, false, 0.5f);
            Shapes.Prim(PrimitiveType.Sphere, Visual, new Vector3(-0.38f, 0.3f, -0.28f), new Vector3(0.12f, 0.12f, 0.08f), dark, false, 0.5f);

            // twarz w "okienku"
            Shapes.Prim(PrimitiveType.Sphere, Visual, new Vector3(0, 0.22f, 0.33f), new Vector3(0.76f, 0.54f, 0.34f), skin, false, 0.45f, "Twarz");
            eyes = new Transform[2];
            for (int s = -1; s <= 1; s += 2)
            {
                var eye = Shapes.Prim(PrimitiveType.Sphere, Visual, new Vector3(0.15f * s, 0.26f, 0.47f), new Vector3(0.21f, 0.27f, 0.1f), Color.white, false, 0.8f, "Oko");
                Shapes.Prim(PrimitiveType.Sphere, eye.transform, new Vector3(0.05f * s, -0.08f, 0.38f), new Vector3(0.58f, 0.6f, 0.5f), Shapes.Hex("#1E1430"), false, 0.9f);
                var glint = Shapes.Prim(PrimitiveType.Sphere, eye.transform, new Vector3(0.18f, 0.12f, 0.6f), new Vector3(0.22f, 0.2f, 0.2f), Color.white);
                glint.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(Color.white, 0.9f, 1.2f);
                eyes[s < 0 ? 0 : 1] = eye.transform;
                Shapes.Prim(PrimitiveType.Sphere, Visual, new Vector3(0.28f * s, 0.12f, 0.43f), new Vector3(0.13f, 0.07f, 0.05f), Shapes.Hex("#FF8FA3"));
            }
            mouthSmile = Shapes.Prim(PrimitiveType.Sphere, Visual, new Vector3(0, 0.08f, 0.5f), new Vector3(0.17f, 0.06f, 0.05f), Shapes.Hex("#6B1E2E"), false, 0.3f, "Usmiech");
            mouthO = Shapes.Prim(PrimitiveType.Sphere, Visual, new Vector3(0, 0.07f, 0.5f), new Vector3(0.11f, 0.13f, 0.05f), Shapes.Hex("#6B1E2E"), false, 0.3f, "Buzia O");
            mouthO.SetActive(false);

            // kiełek na czubku (ziemniak!)
            var sprout = Shapes.Empty("Kielek", Visual, new Vector3(0, 0.64f, 0)).transform;
            Shapes.Prim(PrimitiveType.Cylinder, sprout, new Vector3(0, 0.08f, 0), new Vector3(0.06f, 0.09f, 0.06f), Shapes.Hex("#4CC24A"));
            for (int s = -1; s <= 1; s += 2)
            {
                var leaf = Shapes.Prim(PrimitiveType.Sphere, sprout, new Vector3(0.12f * s, 0.2f, 0), new Vector3(0.24f, 0.07f, 0.13f), Shapes.Hex("#5BD65A"), false, 0.6f);
                leaf.transform.localRotation = Quaternion.Euler(0, 0, 25f * s);
            }

            // rączki z białymi rękawiczkami
            arms = new Transform[2];
            for (int s = -1; s <= 1; s += 2)
            {
                var pivot = Shapes.Empty("Reka", Visual, new Vector3(0.48f * s, 0.02f, 0.02f)).transform;
                var arm = Shapes.Prim(PrimitiveType.Sphere, pivot, new Vector3(0.04f * s, -0.18f, 0), new Vector3(0.2f, 0.4f, 0.2f), body, false, 0.7f);
                Shapes.AddOutline(arm);
                Shapes.Prim(PrimitiveType.Sphere, pivot, new Vector3(0.06f * s, -0.4f, 0), Vector3.one * 0.2f, Color.white, false, 0.6f);
                arms[s < 0 ? 0 : 1] = pivot;
            }
            // nóżki
            feet = new Transform[2];
            for (int s = -1; s <= 1; s += 2)
            {
                var foot = Shapes.Prim(PrimitiveType.Sphere, Visual, new Vector3(0.21f * s, -0.6f, 0.06f), new Vector3(0.27f, 0.16f, 0.36f), dark, false, 0.6f, "Stopa");
                Shapes.AddOutline(foot);
                feet[s < 0 ? 0 : 1] = foot.transform;
            }

            HeadAnchor = Shapes.Empty("NadGlowa", transform, new Vector3(0, 1.35f, 0)).transform;

            // pierścień w kolorze gracza pod stopami: łatwo znaleźć swojego ziemniaka
            var ring = new GameObject("Pierscien gracza");
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = new Vector3(0, -HalfHeight + 0.06f, 0);
            ring.transform.localScale = new Vector3(1.15f, 0.06f, 1.15f);
            ring.AddComponent<MeshFilter>().sharedMesh = Look.Torus();
            var ringRend = ring.AddComponent<MeshRenderer>();
            ringRend.sharedMaterial = Shapes.Mat(c, 0.5f, 0.35f);
            ringRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Gwiazdki nad głową po mocnym uderzeniu
            dizzy = Shapes.Empty("Zawroty", transform, new Vector3(0, 1.05f, 0)).transform;
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f;
                Shapes.Prim(PrimitiveType.Sphere, dizzy, new Vector3(Mathf.Cos(a) * 0.45f, 0, Mathf.Sin(a) * 0.45f), Vector3.one * 0.16f, new Color(1f, 0.85f, 0.2f), false, 0.6f);
            }
            dizzy.gameObject.SetActive(false);
            // postać jest trochę większa niż jej kolizja: lepiej ją widać z kamery, a fizyka się nie zmienia
            Visual.localScale = Vector3.one * VisualScale;
            HeadAnchor.localPosition = new Vector3(0, 1.35f * VisualScale, 0);
            baseVisualScale = Visual.localScale;
            blinkTimer = Random.Range(1.5f, 4f);
        }

        public void Teleport(Vector3 pos)
        {
            Body.position = pos;
            transform.position = pos;
            Phys.SetVelocity(Body, Vector3.zero);
        }

        public void FaceTowards(Vector3 dir)
        {
            dir.y = 0;
            if (dir.sqrMagnitude > 0.001f)
            {
                Facing = dir.normalized;
                yaw = Mathf.Atan2(Facing.x, Facing.z) * Mathf.Rad2Deg;
                Visual.localRotation = Quaternion.Euler(0, yaw, 0);
            }
        }

        public void Knock(Vector3 impulse, float stun = 0.45f)
        {
            var v = Phys.GetVelocity(Body);
            v.x = impulse.x;
            v.z = impulse.z;
            v.y = Mathf.Max(v.y, impulse.y);
            Phys.SetVelocity(Body, v);
            stunTimer = Mathf.Max(stunTimer, stun);
            dashTimer = 0f;
            squash = 1f;
            // mocne uderzenie = fikołek w powietrzu
            if (stun >= 0.5f && new Vector2(impulse.x, impulse.z).magnitude > 7f) tumbleSpeed = Random.value < 0.5f ? 720f : -720f;
        }

        public void SetHidden(bool hidden)
        {
            Visual.gameObject.SetActive(!hidden);
            GetComponent<Collider>().enabled = !hidden;
            Body.isKinematic = hidden;
        }

        void Update()
        {
            if (Slot == null) return;
            if (Slot.IsBot)
            {
                botThinkTimer -= Time.deltaTime;
                if (botThinkTimer <= 0f && BotBrain != null)
                {
                    botThinkTimer = Random.Range(0.1f, 0.25f);
                    botCmd = BotBrain(this);
                    if (botCmd.Jump) jumpQueued = true;
                    if (botCmd.Action) actionQueued = true;
                }
                if (BotBrain == null || botCmd.Idle) moveInput = Vector2.zero;
                else
                {
                    Vector3 d = botCmd.Target - transform.position;
                    d.y = 0;
                    moveInput = d.magnitude < 0.4f ? Vector2.zero : new Vector2(d.x, d.z).normalized * Mathf.Clamp01(d.magnitude / 1.2f);
                }
            }
            else
            {
                moveInput = Slot.ReadMove();
                if (Slot.ReadJump()) jumpQueued = true;
                if (Slot.ReadAction()) actionQueued = true;
            }

            if (Frozen || Eliminated)
            {
                jumpQueued = actionQueued = false;
            }

            // Animacja: kołysanie podczas biegu, "spłaszczenie" przy skoku/uderzeniu
            Vector3 flat = Phys.GetVelocity(Body);
            flat.y = 0;
            float speed = flat.magnitude;
            if (speed > 0.6f && !IsDashing)
            {
                float targetYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
                yaw = Mathf.LerpAngle(yaw, targetYaw, Time.deltaTime * 12f);
                Facing = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            }
            waddle += Time.deltaTime * (Grounded ? speed * 2.2f : 0f);
            float tilt = Grounded ? Mathf.Sin(waddle) * Mathf.Clamp01(speed / 6f) * 9f : 0f;
            float bob = Grounded ? Mathf.Abs(Mathf.Sin(waddle)) * 0.08f * Mathf.Clamp01(speed / 6f) : 0f;
            squash = Mathf.MoveTowards(squash, 0f, Time.deltaTime * 4f);
            float sq = Mathf.Sin(squash * Mathf.PI) * 0.28f;
            // rozciąganie w locie (w górę wyższy i chudszy, przy spadaniu odwrotnie)
            float vy = Phys.GetVelocity(Body).y;
            float stretch = Grounded ? 0f : Mathf.Clamp(vy / 12f, -0.12f, 0.18f);
            Visual.localPosition = new Vector3(0, bob + (VisualScale - 1f) * 0.72f, 0);
            Visual.localScale = new Vector3(baseVisualScale.x * (1 + sq - stretch * 0.5f), baseVisualScale.y * (1 - sq + stretch), baseVisualScale.z * (1 + sq - stretch * 0.5f));
            AnimateLimbs(speed, vy);
            if (tumbleSpeed != 0f)
            {
                tumble += tumbleSpeed * Time.deltaTime;
                if (Grounded && !IsStunned)
                {
                    // po wylądowaniu dokończ obrót do pionu
                    float target = Mathf.Round(tumble / 360f) * 360f;
                    tumble = Mathf.MoveTowards(tumble, target, 900f * Time.deltaTime);
                    if (Mathf.Approximately(tumble, target)) { tumble = 0f; tumbleSpeed = 0f; }
                }
            }
            Visual.localRotation = Quaternion.Euler((IsDashing ? 25f : 0f) + tumble, yaw, tilt);
            bool showDizzy = stunTimer > 0.25f && !Eliminated && Visual.gameObject.activeSelf;
            if (dizzy.gameObject.activeSelf != showDizzy) dizzy.gameObject.SetActive(showDizzy);
            if (showDizzy) dizzy.localRotation = Quaternion.Euler(0, Time.time * 400f, 0);
        }

        void AnimateLimbs(float speed, float vy)
        {
            if (arms == null) return;
            float run = Mathf.Clamp01(speed / 6f);
            float t = Time.time;
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Quaternion target;
                if (IsStunned || Eliminated)
                    target = Quaternion.Euler(Mathf.Sin(t * 22f + i * 2f) * 50f, 0, side * (110f + Mathf.Sin(t * 28f + i) * 40f));
                else if (!Grounded)
                    target = Quaternion.Euler(-20f, 0, side * (vy > 0 ? 140f : 100f) + Mathf.Sin(t * 18f) * 10f);
                else if (IsDashing)
                    target = Quaternion.Euler(-70f, 0, side * 30f);
                else
                    target = Quaternion.Euler(Mathf.Sin(waddle + i * Mathf.PI) * 55f * run, 0, side * (12f + run * 10f));
                arms[i].localRotation = Quaternion.Slerp(arms[i].localRotation, target, Time.deltaTime * 18f);

                var f = feet[i];
                float phase = waddle + i * Mathf.PI;
                float step = Grounded ? Mathf.Sin(phase) * 0.17f * run : (i == 0 ? 0.12f : -0.08f);
                float lift = Grounded ? Mathf.Max(0f, Mathf.Cos(phase)) * 0.1f * run : 0.05f;
                f.localPosition = new Vector3(0.21f * side, -0.6f + lift, 0.06f + step);
            }

            // mruganie
            blinkTimer -= Time.deltaTime;
            if (blinkTimer <= 0f) { blink = 0.14f; blinkTimer = Random.Range(2f, 5f); }
            blink -= Time.deltaTime;
            bool scared = IsStunned || (!Grounded && Mathf.Abs(vy) > 7f) || Eliminated;
            float eyeY = blink > 0f ? 0.03f : (scared ? 0.32f : 0.27f);
            foreach (var e in eyes)
            {
                var sc = e.localScale;
                e.localScale = new Vector3(sc.x, Mathf.MoveTowards(sc.y, eyeY, Time.deltaTime * 4f), sc.z);
            }
            if (mouthO.activeSelf != scared) { mouthO.SetActive(scared); mouthSmile.SetActive(!scared); }
        }

        void FixedUpdate()
        {
            if (Body.isKinematic) return;
            float dt = Time.fixedDeltaTime;
            CheckGround();

            dashTimer -= dt;
            dashCooldown -= dt;
            stunTimer -= dt;
            coyote = Grounded ? 0.12f : coyote - dt;

            Vector3 v = Phys.GetVelocity(Body);
            Vector2 input = (Frozen || Eliminated) ? Vector2.zero : moveInput;

            if (actionQueued)
            {
                actionQueued = false;
                if (CanDash && dashCooldown <= 0f && !IsStunned)
                {
                    Vector3 dir = input.sqrMagnitude > 0.01f ? new Vector3(input.x, 0, input.y).normalized : Facing;
                    Facing = dir;
                    yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                    dashTimer = 0.22f;
                    dashCooldown = 1.0f;
                    v.x = dir.x * DashSpeed;
                    v.z = dir.z * DashSpeed;
                    squash = 0.6f;
                    AudioManager.Play(Sfx.Dash, 0.6f);
                }
            }

            if (jumpQueued)
            {
                jumpQueued = false;
                if (CanJump && coyote > 0f && !IsStunned)
                {
                    v.y = 7.2f;
                    coyote = 0f;
                    squash = 0.5f;
                    Effects.Puff(transform.parent, transform.position + Vector3.down * (HalfHeight - 0.1f), 0.6f);
                    AudioManager.Play(Sfx.Jump, 0.45f);
                }
            }

            if (!IsDashing)
            {
                Vector3 desired = new Vector3(input.x, 0, input.y) * MoveSpeed * SpeedMultiplier + (Grounded ? Push : Vector3.zero);
                float accel = (Grounded ? 55f : 30f) * Grip;
                if (IsStunned) accel *= 0.12f;
                Vector3 flat = new Vector3(v.x, 0, v.z);
                // Na lodzie (niski grip) bez wciśniętego kierunku ślizgamy się dłużej
                if (input.sqrMagnitude < 0.01f && Grounded && Push.sqrMagnitude < 0.01f) accel = 30f * Grip * Grip + (IsStunned ? 0 : 2f);
                flat = Vector3.MoveTowards(flat, desired, accel * dt);
                v.x = flat.x;
                v.z = flat.z;
            }

            Phys.SetVelocity(Body, v);
            // Dodatkowa grawitacja: skoki są krótsze i bardziej "skoczne"
            Body.AddForce(Vector3.down * (v.y < 0 ? 22f : 12f), ForceMode.Acceleration);
        }

        void CheckGround()
        {
            RaycastHit hit;
            int mask = ~(1 << Layer);
            bool was = Grounded;
            Grounded = Physics.SphereCast(Body.position, 0.38f, Vector3.down, out hit, HalfHeight - 0.38f + 0.12f, mask, QueryTriggerInteraction.Ignore);
            GroundCollider = Grounded ? hit.collider : null;
            if (Grounded && !was && Phys.GetVelocity(Body).y < -4f)
            {
                squash = 0.45f;
                Effects.Puff(transform.parent, hit.point + Vector3.up * 0.1f, 0.9f);
            }
        }

        void OnCollisionEnter(Collision c)
        {
            var other = c.collider.GetComponent<Potato>();
            if (other == null || other.Eliminated || Eliminated) return;

            if (IsDashing)
            {
                Vector3 dir = other.transform.position - transform.position;
                dir.y = 0;
                if (dir.sqrMagnitude < 0.001f) dir = Facing;
                dir.Normalize();
                other.Knock(dir * DashPower + Vector3.up * 3.5f, 0.55f);
                // Odbicie uderzającego, żeby nie "wjeżdżał" w przeciwnika
                Knock(-dir * 3f, 0.15f);
                AudioManager.Play(Sfx.Bump, 0.8f);
                if (GameManager.Instance != null) GameManager.Instance.OnPotatoBump(this, other, true);
            }
            else if (!other.IsDashing)
            {
                if (GameManager.Instance != null) GameManager.Instance.OnPotatoBump(this, other, false);
            }
        }
    }
}
