using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace PotatoParty
{
    /// <summary>
    /// Kamera filmowa na Cinemachine 3. Jedna wirtualna kamera z <c>CinemachinePositionComposer</c>
    /// podąża za "punktem kadru" liczonym tu co klatkę z pozycji i prędkości graczy. Zamiast dalekiego
    /// widoku z góry kamera siedzi <b>nisko i blisko</b>: pochylenie 34° przy ciasnym kadrze, do 52° tylko
    /// wtedy, gdy gracze rozbiegną się po całej arenie.
    /// <para>Warstwy (od dołu):</para>
    /// <list type="number">
    /// <item>Kadrowanie grupy: środek ważony (ludzie &gt; boty), wyprzedzenie o 0.3 s ruchu grupy, dystans z rozrzutu.</item>
    /// <item>Najazd akcji (<see cref="FocusOn"/>): na 0.3–1 s kadr ściąga się do miejsca trafienia i przybliża o 6–22%.</item>
    /// <item><see cref="CameraDynamics"/> (rozszerzenie Cinemachine): FOV i dystans rosną z prędkością, przechył (dutch)
    ///       z bocznego ruchu kamery, oddech "z ręki", odrzut ciosu i puls FOV.</item>
    /// <item><c>CinemachineImpulseListener</c>: wstrząsy z <see cref="Impulses"/>.</item>
    /// </list>
    /// Wszystko w czasie rzeczywistym – kamera żyje w hit-stopie i slow-motion.
    /// </summary>
    [DefaultExecutionOrder(-50)] // przed CinemachineBrain (LateUpdate)
    public class CinematicCamera : MonoBehaviour
    {
        // ---------------- API zgodne z poprzednią kamerą (GameManager, minigry) ----------------
        public Camera Cam;
        public readonly List<Transform> Targets = new List<Transform>();
        public Vector3 ArenaCenter;
        public float ArenaRadius = 10f;
        /// <summary>Przesunięcie kadru w świecie (menu: postacie po prawej, miejsce na przyciski po lewej).</summary>
        public Vector3 ScreenOffset = Vector3.zero;
        /// <summary>Najbliższy dystans kamery od punktu kadru [m]. Gra: 9, lobby: 12, menu: 13.</summary>
        public float MinDistance = 6f;
        public float MaxDistance = 22f;
        /// <summary>0 = kadr tylko na graczy, 1 = kadr przyklejony do środka areny.</summary>
        public float FollowArenaPull = 0.1f;
        /// <summary>Minimalny rozrzut grupy [m] – poniżej tego kamera już się nie przybliża.</summary>
        public float MinSpread = 5f;

        [Header("Kadr (Inspektor: wartości gry)")]
        public float PitchNear = 20f;            // ° przy MinDistance (nisko, "z poziomu ziemniaka")
        public float PitchFar = 38f;             // ° przy MaxDistance
        public float Yaw = 0f;
        public float FrameMargin = 1.3f;         // m zapasu wokół skrajnych graczy
        public float LeadTime = 0.3f;            // s wyprzedzenia ruchu grupy
        public float MaxLead = 2.5f;             // m
        public float FocusSmoothTime = 0.22f;    // s (SmoothDamp punktu kadru)
        public float DistanceSmoothTime = 0.45f; // s (SmoothDamp dystansu: oddalanie wolniej niż ruch)
        public float BaseFov = 34f;              // ° pionowo, gra
        public float ShowcaseFov = 26f;          // ° menu / lobby / podium

        [Header("Tryb pokazowy (menu, lobby, podium)")]
        public float ShowcasePitch = 12f;
        public float ShowcaseOrbit = 10f;        // ° wahania kąta
        public float ShowcaseOrbitSpeed = 0.07f; // Hz

        public CinemachineBrain Brain { get; private set; }
        public CinemachineCamera Virtual { get; private set; }
        public CameraDynamics Dynamics { get; private set; }
        public bool Showcase { get; set; }

        /// <summary>Punkt, na który patrzy kadr (DoF ostrzy tutaj).</summary>
        public Vector3 FocusPoint { get { return focus; } }
        /// <summary>Średnia prędkość pozioma śledzonych graczy [m/s] (wygładzona).</summary>
        public float GroupSpeed { get; private set; }
        /// <summary>Prędkość kamery [m/s] – do motion bluru.</summary>
        public float CameraSpeed { get; private set; }
        public float Distance { get { return distance; } }

        CinemachinePositionComposer composer;
        Transform rig;
        Vector3 focus, focusVel, lastCamPos;
        float distance = 14f, distanceVel;
        float showcaseTime;
        Vector3 groupVelocity;

        // najazd akcji
        Vector3 actionPoint;
        float actionStrength, actionTimer;
        Spring action;

        readonly Dictionary<Transform, Potato> potatoCache = new Dictionary<Transform, Potato>();

        public static CinematicCamera Create()
        {
            var root = new GameObject("Kamera filmowa");

            // --- kamera fizyczna + mózg Cinemachine
            var camGo = new GameObject("Main Camera");
            camGo.transform.SetParent(root.transform, false);
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 38f;
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.Skybox;
            camGo.AddComponent<AudioListener>();
            var brain = camGo.AddComponent<CinemachineBrain>();
            brain.IgnoreTimeScale = true;                              // kamera płynie w hit-stopie
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate; // ziemniaki mają interpolację Rigidbody
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.8f);

            // --- punkt kadru, za którym jedzie kamera wirtualna
            var rig = new GameObject("Punkt kadru").transform;
            rig.SetParent(root.transform, false);

            // --- kamera wirtualna
            var vGo = new GameObject("CM Kamera areny");
            vGo.transform.SetParent(root.transform, false);
            var v = vGo.AddComponent<CinemachineCamera>();
            var lens = v.Lens;
            lens.FieldOfView = 38f;
            lens.NearClipPlane = 0.2f;
            lens.FarClipPlane = 400f;
            v.Lens = lens;
            v.Follow = rig;

            var comp = vGo.AddComponent<CinemachinePositionComposer>();
            comp.CameraDistance = 14f;
            comp.DeadZoneDepth = 0f;
            comp.Damping = new Vector3(0.18f, 0.18f, 0.3f); // dodatkowe, "organiczne" opóźnienie
            comp.CenterOnActivate = true;

            var dyn = vGo.AddComponent<CameraDynamics>();
            var listener = vGo.AddComponent<CinemachineImpulseListener>();
            listener.Gain = 1f;
            listener.Use2DDistance = true;   // gra imprezowa: wysokość nie osłabia wstrząsu
            listener.UseCameraSpace = true;
            listener.ChannelMask = 1;

            var cc = root.AddComponent<CinematicCamera>();
            cc.Cam = cam;
            cc.Brain = brain;
            cc.Virtual = v;
            cc.Dynamics = dyn;
            cc.composer = comp;
            cc.rig = rig;
            dyn.Owner = cc;
            return cc;
        }

        // ---------------- sprzężenie zwrotne ----------------

        /// <summary>Stare API (metry wstrząsu 0.1–0.6) – teraz impuls Cinemachine w punkcie kadru.</summary>
        public void Shake(float amount) { Feel.Shake(amount); }

        /// <summary>Zgodność: "trauma" zamieniana na impuls.</summary>
        public void AddTrauma(float amount) { Impulses.Fire(ImpulseKind.Slam, focus, Vector3.down, amount * 0.4f); }

        /// <summary>Kierunkowy odrzut kamery wzdłuż ciosu (sprężyna w <see cref="CameraDynamics"/>).</summary>
        public void Kick(Vector3 worldDir, float strength) { if (Dynamics != null) Dynamics.Kick(worldDir, strength); }

        /// <summary>Puls FOV w stopniach (ujemny = szybkie "zassanie" przy trafieniu).</summary>
        public void Punch(float degrees) { if (Dynamics != null) Dynamics.Punch(degrees); }

        /// <summary>
        /// Najazd na akcję: kadr na chwilę przesuwa się w stronę punktu i przybliża (zoom01: 0.1 = 10% bliżej).
        /// Silniejszy najazd nadpisuje słabszy.
        /// </summary>
        public void FocusOn(Vector3 worldPoint, float zoom01, float seconds)
        {
            if (zoom01 <= 0f) return;
            if (actionTimer > 0f && zoom01 < actionStrength * 0.8f) return;
            actionPoint = worldPoint;
            actionStrength = Mathf.Clamp(zoom01, 0f, 0.35f);
            actionTimer = seconds;
        }

        /// <summary>Natychmiastowe ustawienie kadru (zmiana mapy) – bez dojazdu i bez blendu.</summary>
        public void SnapNow()
        {
            Vector3 f;
            float d;
            Compute(out f, out d);
            focus = f;
            distance = d;
            focusVel = Vector3.zero;
            distanceVel = 0f;
            action.Reset();
            actionTimer = 0f;
            ApplyRig(0f);
            if (Virtual != null) Virtual.PreviousStateIsValid = false;
            if (Dynamics != null) Dynamics.ResetState();
            lastCamPos = Cam != null ? Cam.transform.position : Vector3.zero;
        }

        // ---------------- kadrowanie ----------------

        bool Usable(Transform t, bool humansOnly)
        {
            if (t == null || !t.gameObject.activeInHierarchy) return false;
            if (t.position.y < ArenaCenter.y - 6f) return false; // spadający nie ciągną kamery w przepaść
            var pot = PotatoOf(t);
            if (pot == null) return true; // np. piłka
            if (pot.Eliminated) return false;
            return !humansOnly || (pot.Slot != null && !pot.Slot.IsBot);
        }

        Potato PotatoOf(Transform t)
        {
            Potato p;
            if (potatoCache.TryGetValue(t, out p)) return p;
            if (potatoCache.Count > Targets.Count + 32) potatoCache.Clear();
            p = t.GetComponent<Potato>();
            potatoCache[t] = p;
            return p;
        }

        void Compute(out Vector3 f, out float d)
        {
            bool anyHuman = false;
            for (int i = 0; i < Targets.Count; i++)
            {
                var t = Targets[i];
                if (Usable(t, true) && PotatoOf(t) != null) { anyHuman = true; break; }
            }

            Bounds b = new Bounds(ArenaCenter, Vector3.one);
            Vector3 weighted = Vector3.zero, vel = Vector3.zero;
            float wsum = 0f;
            int count = 0, moving = 0;
            for (int i = 0; i < Targets.Count; i++)
            {
                var t = Targets[i];
                if (!Usable(t, anyHuman)) continue;
                var p = t.position;
                if (count == 0) b = new Bounds(p, Vector3.one);
                else b.Encapsulate(p);
                count++;
                var pot = PotatoOf(t);
                // ważenie: postać w powietrzu nie szarpie kadrem w pionie
                float w = 1f;
                weighted += new Vector3(p.x, ArenaCenter.y, p.z) * w;
                wsum += w;
                if (pot != null && pot.Body != null && !pot.Body.isKinematic)
                {
                    var v = Phys.GetVelocity(pot.Body);
                    v.y = 0f;
                    vel += v;
                    moving++;
                }
            }
            if (count == 0) b = new Bounds(ArenaCenter, Vector3.one * ArenaRadius);

            Vector3 center = wsum > 0f ? Vector3.Lerp(b.center, weighted / wsum, 0.5f) : b.center;
            groupVelocity = moving > 0 ? vel / moving : Vector3.zero;

            f = Vector3.Lerp(center, ArenaCenter, FollowArenaPull);
            f += Vector3.ClampMagnitude(groupVelocity * LeadTime, MaxLead);
            f.y = ArenaCenter.y + 0.9f; // celujemy w oczy, nie w stopy
            f += ScreenOffset;

            // rozrzut w osiach kadru: głębokość (Z) liczy się mocniej, bo kamera patrzy pod kątem
            float spread = Mathf.Max(b.size.x, b.size.z * 1.35f, MinSpread);
            float fov = Showcase ? ShowcaseFov : BaseFov;
            float halfV = fov * 0.5f * Mathf.Deg2Rad;
            float aspect = Cam != null ? Mathf.Max(0.5f, Cam.aspect) : 16f / 9f;
            float halfH = Mathf.Atan(Mathf.Tan(halfV) * aspect);
            // kadr musi zmieścić rozrzut w poziomie (szerokość) – pionowo ratuje nas pochylenie
            float needH = (spread * 0.5f + FrameMargin) / Mathf.Tan(halfH);
            float needV = (b.size.z * 0.5f + FrameMargin) / Mathf.Tan(halfV) * 0.8f;
            d = Mathf.Clamp(Mathf.Max(needH, needV), MinDistance, MaxDistance);
        }

        void LateUpdate()
        {
            if (Virtual == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);

            Vector3 f;
            float d;
            Compute(out f, out d);

            // najazd akcji: szybkie wejście, miękkie wyjście (sprężyna krytycznie tłumiona)
            actionTimer -= dt;
            action.Step(actionTimer > 0f ? actionStrength : 0f, actionTimer > 0f ? 3.2f : 1.1f, 0.95f, dt);
            float a = Mathf.Clamp01(action.Value);
            if (a > 0.001f)
            {
                var toward = actionPoint - f;
                toward.y = 0f;
                f += Vector3.ClampMagnitude(toward, 4f) * Mathf.Clamp01(a * 2.5f);
                d *= 1f - a;
            }

            // prędkość grupy (wygładzona) → dynamika kamery i motion blur
            GroupSpeed = Mathf.Lerp(GroupSpeed, groupVelocity.magnitude, 1f - Mathf.Exp(-dt * 4f));
            d *= 1f + 0.08f * Dynamics.SpeedFactor;

            focus = Vector3.SmoothDamp(focus, f, ref focusVel, FocusSmoothTime, Mathf.Infinity, dt);
            // oddalanie wolniej niż przybliżanie: kamera "niechętnie" odjeżdża od akcji
            float st = d > distance ? DistanceSmoothTime : DistanceSmoothTime * 0.6f;
            distance = Mathf.SmoothDamp(distance, d, ref distanceVel, st, Mathf.Infinity, dt);

            if (Showcase) showcaseTime += dt;
            ApplyRig(dt);

            if (Cam != null && dt > 0f)
            {
                var p = Cam.transform.position;
                CameraSpeed = Mathf.Lerp(CameraSpeed, (p - lastCamPos).magnitude / dt, 1f - Mathf.Exp(-dt * 10f));
                lastCamPos = p;
            }
        }

        void ApplyRig(float dt)
        {
            float t = Mathf.InverseLerp(MinDistance, MaxDistance, distance);
            float pitch = Showcase ? ShowcasePitch : Mathf.Lerp(PitchNear, PitchFar, t * t * (3f - 2f * t));
            float yaw = Yaw + (Showcase ? Mathf.Sin(showcaseTime * ShowcaseOrbitSpeed * 6.2831853f) * ShowcaseOrbit : 0f);
            rig.position = focus;
            Virtual.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            composer.CameraDistance = distance;
            if (Dynamics != null) Dynamics.BaseFov = Showcase ? ShowcaseFov : BaseFov;
        }
    }
}
