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

    /// <summary>
    /// Ziemniak: postać gracza lub bota. Ta klasa to tylko <b>fasada</b> i budowa wyglądu:
    /// ruch robi <see cref="PotatoMotor"/>, animację <see cref="PotatoBody"/>, powierzchnię (brud, woda,
    /// uszkodzenia) <see cref="PotatoSurface"/>, a sprzężenie zwrotne <see cref="Feel"/>.
    /// Publiczne API (pola strojenia, Knock, Teleport, FaceTowards…) jest takie samo jak wcześniej,
    /// więc 12 minigier działa bez zmian.
    /// </summary>
    public class Potato : MonoBehaviour
    {
        public const int Layer = 2; // "Ignore Raycast": raycasty podłoża nie trafiają w innych graczy
        public const float HalfHeight = 0.65f;
        const float VisualScale = 1.3f;

        public PlayerSlot Slot;
        public Rigidbody Body;
        public Transform Visual;
        public Transform HeadAnchor;
        /// <summary>Skórka, w której zbudowano postać.</summary>
        public PotatoSkin Skin { get; private set; }
        /// <summary>Brud / woda / szron / zwęglenie / uszkodzenia na shaderze bohatera (null, gdy shadera brak).</summary>
        public PotatoSurface Surface;
        public PotatoMotor Motor { get; private set; }
        public PotatoBody Anim { get; private set; }

        public bool Frozen = true;
        public bool Eliminated;
        public float Grip = 1f;
        public float SpeedMultiplier = 1f;
        public float MoveSpeed = 7.5f;
        /// <summary>Mnożnik odrzutu, który ten ziemniak dostaje (np. lekki ziemniak w Wielkim Smażeniu lata dalej).</summary>
        public float KnockMultiplier = 1f;
        /// <summary>Wciśnięta akcja (gracz albo bot) – minigry mogą podmienić znaczenie przycisku (np. atak Kreta).</summary>
        public event System.Action<Potato> ActionPressed;
        /// <summary>Lądowanie dowolnego ziemniaka: (kto, prędkość spadania m/s, punkt).</summary>
        public static event System.Action<Potato, float, Vector3> Landed;
        public float DashSpeed = 21f;
        public float DashPower = 13f;
        public bool CanDash = true;
        public bool CanJump = true;
        /// <summary>Zewnętrzne "pchanie" podłoża (np. taśmociąg). Minigra ustawia je co klatkę.</summary>
        public Vector3 Push;

        public System.Func<Potato, BotCommand> BotBrain;

        public Vector3 Facing
        {
            get { return Motor != null ? Motor.Facing : Vector3.back; }
            set { if (Motor != null) Motor.Facing = value; }
        }
        public bool Grounded { get { return Motor != null && Motor.Grounded; } }
        public Collider GroundCollider { get { return Motor != null ? Motor.GroundCollider : null; } }
        /// <summary>Faza aktywna zrywu: tylko wtedy zderzenie jest ciosem.</summary>
        public bool IsDashing { get { return Motor != null && Motor.Dash == DashPhase.Active; } }
        public bool IsStunned { get { return Motor != null && Motor.StunLeft > 0f; } }
        public float DashCooldownLeft { get { return Motor != null ? Motor.DashCooldownLeft : 0f; } }
        public Color Color { get { return Slot != null ? Slot.Color : Color.white; } }

        float botThinkTimer, botJumpHold;
        BotCommand botCmd;

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
            rb.useGravity = false; // grawitację liczy PotatoMotor (skok definiowany wysokością i czasem)
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Phys.SetDamping(rb, 0f);

            var p = go.AddComponent<Potato>();
            p.Slot = slot;
            p.Body = rb;
            p.Motor = go.AddComponent<PotatoMotor>();
            p.Motor.Owner = p;
            p.Motor.Body = rb;
            p.BuildVisual();
            return p;
        }

        /// <summary>Manekin do garderoby: sam wygląd (skórka), bez fizyki i sterowania.</summary>
        public static Potato CreateMannequin(PlayerSlot slot, Transform parent, Vector3 localPos)
        {
            var go = new GameObject("Manekin " + slot.Name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            var p = go.AddComponent<Potato>();
            p.Slot = slot;
            p.Body = rb;
            p.Frozen = true;
            p.Motor = go.AddComponent<PotatoMotor>();
            p.Motor.Owner = p;
            p.Motor.Body = rb;
            p.BuildVisual();
            p.Motor.ForceGrounded(true);
            return p;
        }

        static int anonSeed;

        void BuildVisual()
        {
            // Gładki ziemniaczek z dużą buzią, rączkami i nóżkami, w wybranej skórce
            var c = Color;
            int skinIndex = Slot != null ? Slot.Skin : 0;
            Skin = PotatoSkins.Get(skinIndex);
            var body = Skin.BodyColor(c);
            var dark = Skin.SpotColor(c);
            float gloss = Skin.Smoothness;
            Visual = Shapes.Empty("Wyglad", transform, Vector3.zero).transform;

            var heroParts = new System.Collections.Generic.List<Renderer>();
            System.Action<GameObject, Color, PotatoPart, float> hero = (go, col, part, sm) =>
            {
                var r = go.GetComponent<Renderer>();
                r.sharedMaterial = Shapes.HeroMat(col, part, sm, Skin, skinIndex, c);
                heroParts.Add(r);
            };
            // bulwa: proceduralna, asymetryczna, z guzami i "oczkami" – każdy gracz ma własny kształt
            var torso = Shapes.Prim(PrimitiveType.Sphere, Visual, new Vector3(0, 0.02f, 0), new Vector3(1.1f, 1.26f, 0.98f), body, false, gloss, "Cialo");
            torso.GetComponent<MeshFilter>().sharedMesh = PotatoMesh.Get(Slot != null ? Slot.Index * 31 + skinIndex : 1000 + (anonSeed++ % 8));
            hero(torso, body, PotatoPart.Body, gloss);
            Shapes.AddOutline(torso);
            // (bez brzuszka, "okienka" twarzy i doklejanych plamek – to robiło z ziemniaka zabawkę;
            //  plamki, oczka, przetchlinki i siateczkę skórki rysuje teraz shader na samej bulwie)
            var eyes = new Transform[2];
            for (int s = -1; s <= 1; s += 2)
            {
                var eye = Shapes.Prim(PrimitiveType.Sphere, Visual, new Vector3(0.15f * s, 0.26f, 0.47f), new Vector3(0.21f, 0.27f, 0.1f), Color.white, false, 0.8f, "Oko");
                Shapes.Prim(PrimitiveType.Sphere, eye.transform, new Vector3(0.05f * s, -0.08f, 0.38f), new Vector3(0.58f, 0.6f, 0.5f), Shapes.Hex("#1E1430"), false, 0.9f);
                var glint = Shapes.Prim(PrimitiveType.Sphere, eye.transform, new Vector3(0.18f, 0.12f, 0.6f), new Vector3(0.22f, 0.2f, 0.2f), Color.white);
                glint.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(Color.white, 0.9f, 1.2f);
                eyes[s < 0 ? 0 : 1] = eye.transform;
                Shapes.Prim(PrimitiveType.Sphere, Visual, new Vector3(0.28f * s, 0.12f, 0.43f), new Vector3(0.13f, 0.07f, 0.05f), Shapes.Hex("#FF8FA3"));
            }
            var mouthSmile = Shapes.Prim(PrimitiveType.Sphere, Visual, new Vector3(0, 0.08f, 0.5f), new Vector3(0.17f, 0.06f, 0.05f), Shapes.Hex("#6B1E2E"), false, 0.3f, "Usmiech");
            var mouthO = Shapes.Prim(PrimitiveType.Sphere, Visual, new Vector3(0, 0.07f, 0.5f), new Vector3(0.11f, 0.13f, 0.05f), Shapes.Hex("#6B1E2E"), false, 0.3f, "Buzia O");
            mouthO.SetActive(false);

            // kiełek na czubku albo dodatek skórki (kapelusz, czapka, rogi, okulary...)
            PotatoSkins.BuildAccessory(Skin, Visual, c);
            // (łańcuch "galaretki" dokładamy na końcu, gdy wszystkie części już są pod Visual)

            // rączki z rękawiczkami (w kolorze gracza, jeśli skórka nie ma jego koloru)
            var glove = Skin.PlayerGloves ? Color.Lerp(c, Color.white, 0.12f) : Color.white;
            var arms = new Transform[2];
            for (int s = -1; s <= 1; s += 2)
            {
                var pivot = Shapes.Empty("Reka", Visual, new Vector3(0.48f * s, 0.02f, 0.02f)).transform;
                var arm = Shapes.Prim(PrimitiveType.Sphere, pivot, new Vector3(0.04f * s, -0.18f, 0), new Vector3(0.2f, 0.4f, 0.2f), body, false, gloss);
                hero(arm, body, PotatoPart.Limb, gloss);
                Shapes.AddOutline(arm);
                Shapes.Prim(PrimitiveType.Sphere, pivot, new Vector3(0.06f * s, -0.4f, 0), Vector3.one * 0.2f, glove, false, 0.6f);
                arms[s < 0 ? 0 : 1] = pivot;
            }
            // nóżki
            var feet = new Transform[2];
            for (int s = -1; s <= 1; s += 2)
            {
                var foot = Shapes.Prim(PrimitiveType.Sphere, Visual, new Vector3(0.21f * s, -0.6f, 0.06f), new Vector3(0.27f, 0.16f, 0.36f), dark, false, 0.6f, "Stopa");
                hero(foot, dark, PotatoPart.Spot, 0.6f);
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
            var dizzy = Shapes.Empty("Zawroty", transform, new Vector3(0, 1.05f, 0)).transform;
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f;
                Shapes.Prim(PrimitiveType.Sphere, dizzy, new Vector3(Mathf.Cos(a) * 0.45f, 0, Mathf.Sin(a) * 0.45f), Vector3.one * 0.16f, new Color(1f, 0.85f, 0.2f), false, 0.6f);
            }
            dizzy.gameObject.SetActive(false);
            // postać jest trochę większa niż jej kolizja: lepiej ją widać z kamery, a fizyka się nie zmienia
            Visual.localScale = Vector3.one * VisualScale;
            HeadAnchor.localPosition = new Vector3(0, 1.35f * VisualScale, 0);

            // proceduralna animacja (łańcuch "galaretki" powstaje teraz, gdy wszystkie części są pod Visual)
            Anim = gameObject.AddComponent<PotatoBody>();
            Anim.Owner = this;
            Anim.Visual = Visual;
            Anim.Eyes = eyes;
            Anim.Arms = arms;
            Anim.Feet = feet;
            Anim.MouthSmile = mouthSmile;
            Anim.MouthO = mouthO;
            Anim.Dizzy = dizzy;
            Anim.VisualScale = VisualScale;
            Anim.Init();

            // maski (błoto, szron, zwęglenie) działają tylko na shaderze bohatera
            if (Shapes.HeroShaderAvailable)
            {
                Surface = gameObject.AddComponent<PotatoSurface>();
                Surface.Init(Visual, heroParts);
                SkinFx.Configure(this);
            }
        }



        // ---------------- API dla minigier ----------------

        public void Teleport(Vector3 pos)
        {
            Body.position = pos;
            transform.position = pos;
            Phys.SetVelocity(Body, Vector3.zero);
            if (Motor != null) Motor.ResetState();
        }

        public void FaceTowards(Vector3 dir)
        {
            dir.y = 0;
            if (dir.sqrMagnitude < 0.001f) return;
            Facing = dir.normalized;
            if (Anim != null) Anim.SnapYaw(dir);
        }

        /// <summary>Uderzenie z otoczenia lub od minigry (belka, wybuch, kombajn).</summary>
        public void Knock(Vector3 impulse, float stun = 0.45f)
        {
            KnockCore(impulse, stun, true);
        }

        void KnockCore(Vector3 impulse, float stun, bool environment)
        {
            if (Body == null || Body.isKinematic) return; // np. ziemniak niesiony na wieży – odczepia go minigra
            impulse.x *= KnockMultiplier;
            impulse.z *= KnockMultiplier;
            if (Motor != null) Motor.ApplyKnock(impulse, stun);
            float horizontal = new Vector2(impulse.x, impulse.z).magnitude;
            float power = Mathf.Clamp01(impulse.magnitude / 16f);
            Vector3 dir = impulse.sqrMagnitude > 1e-4f ? impulse.normalized : Vector3.up;

            if (stun >= 0.5f || horizontal > 6f)
            {
                if (Anim != null) Anim.OnImpact(dir, power, stun >= 0.5f && horizontal > 7f);
                if (Surface != null)
                {
                    Surface.HitFlash(0.55f);
                    Surface.Impact(dir, 0.35f + 0.65f * power);
                }
                if (environment) Feel.EnvironmentHit(this, dir, power);
            }
            else if (Anim != null) Anim.OnBump();
        }

        public void SetHidden(bool hidden)
        {
            Visual.gameObject.SetActive(!hidden);
            GetComponent<Collider>().enabled = !hidden;
            Body.isKinematic = hidden;
        }

        // ---------------- zdarzenia z PotatoMotor ----------------

        internal void OnJump()
        {
            if (Anim != null) Anim.OnJump();
            Vfx.JumpPuff(transform.parent, transform.position + Vector3.down * (HalfHeight - 0.1f));
            AudioManager.Play(Sfx.Jump, 0.45f);
            Feel.Jump(this);
        }

        internal void OnLand(float fallSpeed, Vector3 point)
        {
            if (Landed != null) Landed(this, fallSpeed, point);
            if (fallSpeed < 3f) return;
            if (Anim != null) Anim.OnLand(fallSpeed);
            SkinFx.OnLand(this, point + Vector3.up * 0.1f, fallSpeed);
            Feel.Land(this, fallSpeed);
        }

        internal void OnDashStart(Vector3 dir)
        {
            if (Anim != null) { Anim.SnapYaw(dir); Anim.OnDashStart(dir); }
        }

        internal void OnDashLaunch(Vector3 dir)
        {
            if (Anim != null) Anim.OnDashLaunch(dir);
            AudioManager.Play(Sfx.Dash, 0.6f);
            Feel.Dash(this);
        }

        // ---------------- wejście ----------------

        void Update()
        {
            if (Slot == null || Motor == null) return;
            if (Slot.IsBot) BotInput();
            else
            {
                Motor.Move = Slot.ReadMove();
                Motor.JumpHeld = Slot.ReadJumpHeld();
                if (Slot.ReadJump()) Motor.QueueJump();
                if (Slot.ReadAction()) { Motor.QueueAction(); if (ActionPressed != null && !Frozen && !Eliminated) ActionPressed(this); }
            }
            if (Frozen || Eliminated) Motor.ClearInput();
            SkinFx.Tick(this, Time.deltaTime);
        }

        void BotInput()
        {
            botThinkTimer -= Time.deltaTime;
            botJumpHold -= Time.deltaTime;
            if (botThinkTimer <= 0f && BotBrain != null)
            {
                botThinkTimer = Random.Range(0.1f, 0.25f);
                botCmd = BotBrain(this);
                if (botCmd.Jump) { Motor.QueueJump(); botJumpHold = Random.Range(0.12f, 0.3f); }
                if (botCmd.Action) { Motor.QueueAction(); if (ActionPressed != null && !Frozen && !Eliminated) ActionPressed(this); }
            }
            Motor.JumpHeld = botJumpHold > 0f;
            if (BotBrain == null || botCmd.Idle) { Motor.Move = Vector2.zero; return; }
            Vector3 d = botCmd.Target - transform.position;
            d.y = 0;
            Motor.Move = d.magnitude < 0.4f ? Vector2.zero : new Vector2(d.x, d.z).normalized * Mathf.Clamp01(d.magnitude / 1.2f);
        }

        // ---------------- zderzenia ----------------

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
                // moc trafienia 0..1: prędkość zderzenia + siła zrywu tej minigry (Sumo ma mocniejszy)
                float power = Mathf.Clamp01(c.relativeVelocity.magnitude / 18f + (DashPower - 13f) / 16f);
                other.KnockCore(dir * DashPower + Vector3.up * 3.5f, 0.55f, false);
                SkinFx.OnHit(other, this);
                // odbicie uderzającego, żeby nie "wjeżdżał" w przeciwnika
                KnockCore(-dir * 3f, 0.15f, false);
                if (Anim != null) Anim.OnImpact(dir, 0.35f, false); // uderzający też się "rozpłaszcza"
                Feel.DashHit(this, other, dir, power);
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
