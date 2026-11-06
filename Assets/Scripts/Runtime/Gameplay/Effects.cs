using UnityEngine;

namespace PotatoParty
{
    /// <summary>Proste efekty z brył: wybuch, rozbłysk, plusk.</summary>
    public static class Effects
    {
        public static void Explosion(Transform parent, Vector3 pos, float size)
        {
            Burst(parent, pos, size, Shapes.Hex("#FF9A1F"), 0.45f);
            Burst(parent, pos, size * 0.6f, Shapes.Hex("#FFE066"), 0.3f);
            for (int i = 0; i < 10; i++)
                Debris(parent, pos, i % 2 == 0 ? Shapes.Hex("#5A4636") : Shapes.Hex("#FF7B1F"), 7f);
        }

        public static void Sparkle(Transform parent, Vector3 pos, Color c)
        {
            Burst(parent, pos, 1.2f, c, 0.25f);
            for (int i = 0; i < 5; i++) Debris(parent, pos, c, 4f, 0.15f);
        }

        public static void Splash(Transform parent, Vector3 pos, Color c)
        {
            for (int i = 0; i < 10; i++) Debris(parent, pos, c, 6f, 0.3f, true);
        }

        static Material particleMat;
        static Mesh confettiMesh;

        static Material ParticleMat()
        {
            if (particleMat != null) return particleMat;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) return null;
            particleMat = new Material(shader) { name = "Konfetti" };
            if (particleMat.HasProperty("_BaseColor")) particleMat.SetColor("_BaseColor", Color.white);
            if (particleMat.HasProperty("_Cull")) particleMat.SetFloat("_Cull", 0f); // obie strony
            return particleMat;
        }

        static readonly Color[] confettiColors =
        {
            Shapes.Hex("#FF4F9A"), Shapes.Hex("#FFD23F"), Shapes.Hex("#3FC1FF"), Shapes.Hex("#7CF25A"), Shapes.Hex("#B57BFF"), Shapes.Hex("#FF8A3D"), Color.white,
        };

        /// <summary>Wybuch kolorowego konfetti (np. zwycięstwo, gol, meta).</summary>
        public static ParticleSystem Confetti(Transform parent, Vector3 pos, int count = 90, bool loop = false, float radius = 0.5f)
        {
            var mat = ParticleMat();
            if (mat == null) return null;
            var go = new GameObject("Konfetti");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(-90f, 0, 0); // stożek w górę
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = loop;
            main.duration = loop ? 1f : 0.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.5f);
            main.startSpeed = loop ? new ParticleSystem.MinMaxCurve(1f, 3f) : new ParticleSystem.MinMaxCurve(6f, 12f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0.55f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 600;
            var grad = new Gradient();
            var keys = new GradientColorKey[confettiColors.Length > 8 ? 8 : confettiColors.Length];
            for (int i = 0; i < keys.Length; i++) keys[i] = new GradientColorKey(confettiColors[i], i / (float)Mathf.Max(1, keys.Length - 1));
            grad.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            main.startColor = new ParticleSystem.MinMaxGradient(grad) { mode = ParticleSystemGradientMode.RandomColor };

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = loop ? 5f : 35f;
            shape.radius = radius;

            var emission = ps.emission;
            emission.enabled = loop;
            if (loop) emission.rateOverTime = count;

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-6f, 6f);
            rot.y = new ParticleSystem.MinMaxCurve(-6f, 6f);
            rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);

            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 1.6f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            if (confettiMesh == null)
            {
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Quad);
                confettiMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
                Object.Destroy(tmp);
            }
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = confettiMesh;
            r.alignment = ParticleSystemRenderSpace.Local;

            if (loop) ps.Play();
            else
            {
                ps.Emit(count);
                Object.Destroy(go, 5f);
            }
            return ps;
        }

        /// <summary>Obłoczek kurzu (lądowanie, skok).</summary>
        public static void Puff(Transform parent, Vector3 pos, float size = 1f)
        {
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f + Random.value;
                var dir = new Vector3(Mathf.Cos(a), 0.25f, Mathf.Sin(a));
                var go = Shapes.Prim(PrimitiveType.Sphere, parent, pos + dir * 0.3f, Vector3.one * 0.3f * size, Color.white, false, 0.1f);
                go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var fx = go.AddComponent<TimedFx>();
                fx.Life = 0.45f;
                fx.Velocity = dir * 2.5f * size;
                fx.Gravity = -1f;
            }
        }

        static void Burst(Transform parent, Vector3 pos, float size, Color c, float time)
        {
            var go = Shapes.Prim(PrimitiveType.Sphere, parent, pos, Vector3.one * 0.2f, c);
            go.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(c, 0.1f, 1.5f);
            var fx = go.AddComponent<TimedFx>();
            fx.Life = time;
            fx.GrowTo = size;
        }

        static void Debris(Transform parent, Vector3 pos, Color c, float speed, float scale = 0.25f, bool upward = false)
        {
            var go = Shapes.Prim(PrimitiveType.Cube, parent, pos, Vector3.one * scale, c);
            var fx = go.AddComponent<TimedFx>();
            fx.Life = 0.9f;
            var dir = Random.onUnitSphere;
            if (upward || dir.y < 0) dir.y = Mathf.Abs(dir.y) + 0.5f;
            fx.Velocity = dir.normalized * speed * Random.Range(0.6f, 1.1f);
            fx.Gravity = 18f;
            fx.Spin = Random.insideUnitSphere * 600f;
        }
    }

    public class TimedFx : MonoBehaviour
    {
        public float Life = 0.5f;
        public float GrowTo = 0f;
        public Vector3 Velocity;
        public float Gravity;
        public Vector3 Spin;
        float t;
        Vector3 startScale;

        void Start() { startScale = transform.localScale; }

        void Update()
        {
            t += Time.deltaTime;
            float k = t / Life;
            if (k >= 1f) { Destroy(gameObject); return; }
            if (GrowTo > 0f)
            {
                float s = Mathf.Lerp(startScale.x, GrowTo * 2f, Mathf.Sqrt(k)) * (1f - k * k);
                transform.localScale = Vector3.one * s;
            }
            else
            {
                Velocity += Vector3.down * Gravity * Time.deltaTime;
                transform.position += Velocity * Time.deltaTime;
                transform.Rotate(Spin * Time.deltaTime);
                transform.localScale = startScale * (1f - k);
            }
        }
    }
}
