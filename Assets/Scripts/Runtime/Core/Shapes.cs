using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PotatoParty
{
    /// <summary>Część ciała ziemniaka – wybiera ustawienia shadera bohatera.</summary>
    public enum PotatoPart { Body, Belly, Face, Limb, Spot }

    /// <summary>
    /// Cała grafika gry jest budowana z prostych brył w kodzie, dlatego projekt nie potrzebuje
    /// żadnych modeli ani tekstur. Tu tworzymy bryły i materiały (URP Lit albo Standard).
    /// </summary>
    public static class Shapes
    {
        static Material baseMat;
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        static Material BaseMaterial()
        {
            if (baseMat != null) return baseMat;
            bool urp = GraphicsSettings.currentRenderPipeline != null;
            var fromResources = Resources.Load<Material>(urp ? "PotatoParty/BaseLit" : "PotatoParty/BaseStandard");
            if (fromResources != null)
            {
                baseMat = new Material(fromResources);
                return baseMat;
            }
            Shader s = urp ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");
            if (s == null) s = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (s == null) s = Shader.Find("Diffuse");
            if (s == null) s = Shader.Find("Sprites/Default");
            baseMat = new Material(s);
            return baseMat;
        }

        /// <summary>Mnożnik połysku wszystkich materiałów.</summary>
        public static float Gloss = 1f;
        /// <summary>Ciemny kontur wokół postaci (styl kreskówkowy). Wyłączony, bo czysty wygląd go nie potrzebuje.</summary>
        public static bool OutlinesEnabled = false;

        public static Material Mat(Color c, float smoothness = 0.42f, float emission = 0f)
        {
            smoothness *= Gloss;
            Color32 k = c;
            string key = k.r + "," + k.g + "," + k.b + "," + Mathf.RoundToInt(smoothness * 100) + "," + Mathf.RoundToInt(emission * 100);
            Material m;
            if (cache.TryGetValue(key, out m) && m != null) return m;
            m = new Material(BaseMaterial());
            m.enableInstancing = true; // setki takich samych drzewek, kamieni i kwiatków = mało draw calli
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (emission > 0f && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor("_EmissionColor", c * emission);
            }
            cache[key] = m;
            return m;
        }

        // ---------------- Shader Bohatera Ziemniaka ("Aksamitna Bulwa") ----------------

        static Material heroBase;
        static bool heroTried;

        /// <summary>Materiał wzorcowy PotatoParty/PotatoHero (z Resources, żeby shader trafił do buildu).</summary>
        static Material HeroBase()
        {
            if (heroTried) return heroBase;
            heroTried = true;
            if (GraphicsSettings.currentRenderPipeline == null) return null; // shader jest tylko pod URP
            var fromResources = Resources.Load<Material>("PotatoParty/PotatoHero");
            if (fromResources != null) heroBase = fromResources;
            else
            {
                var s = Shader.Find("PotatoParty/PotatoHero");
                if (s != null && s.isSupported) heroBase = new Material(s);
            }
            return heroBase;
        }

        /// <summary>Czy ziemniaki używają shadera bohatera (false = zwykły URP Lit jako zapas).</summary>
        public static bool HeroShaderAvailable { get { return HeroBase() != null; } }

        /// <summary>
        /// Materiał części ziemniaka na shaderze bohatera, z ustawieniami dobranymi do części ciała.
        /// Gdy shadera nie ma (Built-in, stara karta), zwraca zwykły materiał z <see cref="Mat"/>.
        /// </summary>
        public static Material HeroMat(Color c, PotatoPart part, float smoothness = 0.55f)
        {
            return HeroMat(c, part, smoothness, null, -1, c);
        }

        /// <summary>
        /// Materiał części ziemniaka w danej skórce. Kolor gracza trafia do konturu (_PlayerColor),
        /// a wzór, metal, miąższ i meszek – ze skórki. Pamięć podręczna po skórce, części i kolorach.
        /// </summary>
        public static Material HeroMat(Color c, PotatoPart part, float smoothness, PotatoSkin skin, int skinIndex, Color player)
        {
            var b = HeroBase();
            if (b == null)
            {
                // zapasowy URP Lit: chociaż metal i świecenie wzoru
                if (skin != null && skin.Metallic > 0.5f) return Mat(c, 0.85f);
                return Mat(c, smoothness);
            }
            smoothness *= Gloss;
            Color32 k = c, kp = player;
            string key = "hero," + (int)part + "," + k.r + "," + k.g + "," + k.b + "," + Mathf.RoundToInt(smoothness * 100) + ",s" + skinIndex + "," + kp.r + "," + kp.g + "," + kp.b;
            Material m;
            if (cache.TryGetValue(key, out m) && m != null) return m;
            m = new Material(b) { name = "Ziemniak " + part + (skin != null ? " (" + skin.Name + ")" : "") };
            m.SetColor("_BaseColor", c);
            m.SetColor("_PlayerColor", player);
            m.SetFloat("_Smoothness", smoothness);
            if (skin != null)
            {
                m.SetColor("_FleshColor", skin.Flesh);
                m.SetColor("_SheenColor", skin.Sheen);
                m.SetFloat("_SpeckleAmount", skin.Speckle);
                m.SetFloat("_ScatterStrength", skin.Scatter);
                bool patterned = part == PotatoPart.Body || part == PotatoPart.Limb || part == PotatoPart.Spot;
                if (patterned && skin.Pattern != SkinPattern.None)
                {
                    m.SetFloat("_PatternType", (float)(int)skin.Pattern);
                    m.SetColor("_PatternColor", skin.PatternColor);
                    m.SetColor("_PatternGlow", skin.PatternGlow);
                    m.SetFloat("_PatternScale", skin.PatternScale);
                }
                if (part != PotatoPart.Face) m.SetFloat("_Metallic", skin.Metallic);
            }
            switch (part)
            {
                case PotatoPart.Body:
                    m.SetFloat("_Thickness", 0.65f);
                    break;
                case PotatoPart.Belly:
                    // brzuszek nachodzi na tułów: słabszy kontur, żeby nie rysował "pierścienia" na ciele
                    m.SetFloat("_Thickness", 0.7f);
                    m.SetFloat("_RimStrength", 0.25f);
                    m.SetFloat("_SpeckleAmount", skin != null ? skin.Speckle * 0.5f : 0.3f);
                    break;
                case PotatoPart.Face:
                    m.SetFloat("_RimStrength", 0.15f);
                    m.SetFloat("_SpeckleAmount", 0.12f);
                    m.SetFloat("_ScatterStrength", 0.8f);
                    m.SetFloat("_Metallic", 0f);
                    m.SetColor("_FleshColor", new Color(1f, 0.62f, 0.5f));
                    m.SetFloat("_MaskReceive", 0.7f);
                    break;
                case PotatoPart.Limb:
                    // cienkie rączki mocno prześwitują pod światło
                    m.SetFloat("_Thickness", 0.15f);
                    m.SetFloat("_RimStrength", 0.6f);
                    m.SetFloat("_SpeckleAmount", skin != null ? skin.Speckle * 0.35f : 0.2f);
                    break;
                case PotatoPart.Spot:
                    m.SetFloat("_Thickness", 0.8f);
                    m.SetFloat("_RimStrength", 0.4f);
                    m.SetFloat("_SpeckleAmount", skin != null ? Mathf.Max(0.3f, skin.Speckle) : 0.8f);
                    break;
            }
            cache[key] = m;
            return m;
        }

        /// <summary>Osobny materiał (nie z pamięci podręcznej), np. do migania kolorem.</summary>
        public static Material UniqueMat(Color c, float smoothness = 0.42f)
        {
            var m = new Material(Mat(c, smoothness));
            return m;
        }

        public static void SetColor(Material m, Color c)
        {
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        }


        public static GameObject Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Color color,
            bool keepCollider = false, float smoothness = 0.42f, string name = null)
        {
            var go = GameObject.CreatePrimitive(type);
            if (name != null) go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = Mat(color, smoothness);
            if (type == PrimitiveType.Cube) RoundCorners(go, scale);
            if (!keepCollider)
            {
                var col = go.GetComponent<Collider>();
                if (col != null) { col.enabled = false; Object.Destroy(col); }
            }
            return go;
        }

        // ---------------- Kreskówkowy kontur ----------------

        static Material outlineMat;
        static bool outlineTried;

        /// <summary>Ciemny, nieoświetlony materiał rysujący tylko tylne ściany (kontur "inverted hull").</summary>
        public static Material OutlineMat()
        {
            if (outlineTried) return outlineMat;
            outlineTried = true;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null || GraphicsSettings.currentRenderPipeline == null) return null;
            outlineMat = new Material(shader) { name = "Kontur" };
            var c = Hex("#2A1838");
            outlineMat.color = c;
            if (outlineMat.HasProperty("_BaseColor")) outlineMat.SetColor("_BaseColor", c);
            if (outlineMat.HasProperty("_Cull")) outlineMat.SetFloat("_Cull", 1f); // rysuj tylko tył
            return outlineMat;
        }

        /// <summary>Dodaje czarny kontur wokół bryły (kopia siatki, trochę większa, odwrócona).</summary>
        public static void AddOutline(GameObject go, float thickness = 0.07f)
        {
            if (!OutlinesEnabled) return;
            var mat = OutlineMat();
            var mf = go.GetComponent<MeshFilter>();
            if (mat == null || mf == null) return;
            var o = new GameObject("Kontur");
            o.transform.SetParent(go.transform, false);
            o.transform.localScale = Vector3.one * (1f + thickness);
            o.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var r = o.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        // ---------------- Zaokrąglone klocki (styl "zabawkowy") ----------------

        static readonly Dictionary<Vector3Int, Mesh> roundedCache = new Dictionary<Vector3Int, Mesh>();

        /// <summary>Podmienia siatkę sześcianu na klocek z zaokrąglonymi krawędziami (collider zostaje pudełkiem).</summary>
        public static void RoundCorners(GameObject go, Vector3 size)
        {
            size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            float min = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
            if (min < 0.06f) return; // cienkie płytki zostają płaskie
            var key = new Vector3Int(Mathf.RoundToInt(size.x * 20f), Mathf.RoundToInt(size.y * 20f), Mathf.RoundToInt(size.z * 20f));
            Mesh mesh;
            if (!roundedCache.TryGetValue(key, out mesh) || mesh == null)
            {
                mesh = BuildRoundedBox(size, Mathf.Clamp(min * 0.3f, 0.02f, 0.35f));
                roundedCache[key] = mesh;
            }
            var mf = go.GetComponent<MeshFilter>();
            if (mf != null) mf.sharedMesh = mesh;
        }

        static Mesh BuildRoundedBox(Vector3 size, float r)
        {
            const int k = 3; // ile kroków na zaokrąglenie
            var h = size * 0.5f;
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();

            System.Func<float, float[]> coords = half =>
            {
                var list = new float[(k + 1) * 2];
                for (int i = 0; i <= k; i++)
                {
                    list[i] = -half + r * (i / (float)k);
                    list[k + 1 + i] = half - r + r * (i / (float)k);
                }
                return list;
            };

            // 6 ścian: oś normalnej, znak
            for (int axis = 0; axis < 3; axis++)
            {
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    int ua = (axis + 1) % 3, va = (axis + 2) % 3;
                    var uc = coords(h[ua]);
                    var vc = coords(h[va]);
                    int start = verts.Count;
                    for (int j = 0; j < vc.Length; j++)
                    {
                        for (int i = 0; i < uc.Length; i++)
                        {
                            var p = Vector3.zero;
                            p[axis] = h[axis] * sign;
                            p[ua] = uc[i];
                            p[va] = vc[j];
                            var inner = new Vector3(
                                Mathf.Clamp(p.x, -h.x + r, h.x - r),
                                Mathf.Clamp(p.y, -h.y + r, h.y - r),
                                Mathf.Clamp(p.z, -h.z + r, h.z - r));
                            var n = (p - inner).normalized;
                            var pos = inner + n * r;
                            // siatka jest w jednostkach 1x1x1, a obiekt ma skalę = rozmiar
                            verts.Add(new Vector3(pos.x / size.x, pos.y / size.y, pos.z / size.z));
                            normals.Add(new Vector3(n.x * size.x, n.y * size.y, n.z * size.z).normalized);
                        }
                    }
                    int w = uc.Length;
                    for (int j = 0; j < vc.Length - 1; j++)
                    {
                        for (int i = 0; i < w - 1; i++)
                        {
                            int a = start + j * w + i, b = a + 1, c = a + w, d = c + 1;
                            if (sign > 0) { tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(b); tris.Add(d); tris.Add(c); }
                            else { tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(d); }
                        }
                    }
                }
            }
            var mesh = new Mesh { name = "Klocek" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        public static GameObject Box(Transform parent, Vector3 pos, Vector3 size, Color color, bool collider = true, float yRot = 0f)
        {
            var go = Prim(PrimitiveType.Cube, parent, pos, size, color, collider);
            go.transform.localRotation = Quaternion.Euler(0, yRot, 0);
            return go;
        }

        // ---------------- Siatki low-poly ----------------
        static Mesh lowPolySphere, diamond;
        static readonly Dictionary<int, Mesh> rocks = new Dictionary<int, Mesh>();

        /// <summary>Kanciasta kula (ikosfera) z płaskim cieniowaniem, średnica 1 jak kula Unity.</summary>
        public static Mesh LowPolySphere()
        {
            if (lowPolySphere == null) lowPolySphere = Icosphere(2, 0.5f, null, "Kula low-poly");
            return lowPolySphere;
        }

        /// <summary>Nieregularna, kanciasta skała (kilka wariantów).</summary>
        public static Mesh Rock(int variant)
        {
            variant = Mathf.Abs(variant) % 6;
            Mesh m;
            if (rocks.TryGetValue(variant, out m) && m != null) return m;
            var rng = new System.Random(1000 + variant * 37);
            m = Icosphere(1, 0.5f, v =>
            {
                float k = 0.72f + (float)rng.NextDouble() * 0.42f;
                var p = v * k;
                if (p.y < -0.1f) p.y = -0.1f + (p.y + 0.1f) * 0.35f; // spłaszczony spód
                return p;
            }, "Skala " + variant);
            rocks[variant] = m;
            return m;
        }

        /// <summary>Wydłużony kryształ (ośmiościan), wysokość 1.</summary>
        public static Mesh Diamond()
        {
            if (diamond != null) return diamond;
            var top = new Vector3(0, 0.5f, 0);
            var bottom = new Vector3(0, -0.5f, 0);
            var ring = new Vector3[6];
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI * 2f / 6f;
                ring[i] = new Vector3(Mathf.Cos(a) * 0.5f, 0.12f, Mathf.Sin(a) * 0.5f);
            }
            var tris = new List<Vector3>();
            for (int i = 0; i < 6; i++)
            {
                var a = ring[i];
                var b = ring[(i + 1) % 6];
                tris.Add(top); tris.Add(b); tris.Add(a);
                tris.Add(bottom); tris.Add(a); tris.Add(b);
            }
            diamond = Flat(tris, "Krysztal");
            return diamond;
        }

        static Mesh Icosphere(int subdivisions, float radius, System.Func<Vector3, Vector3> shape, string name)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var verts = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;
            var faces = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            var mid = new Dictionary<long, int>();
            System.Func<int, int, int> midpoint = (a, b) =>
            {
                long key = a < b ? ((long)a << 32) + b : ((long)b << 32) + a;
                int idx;
                if (mid.TryGetValue(key, out idx)) return idx;
                verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
                idx = verts.Count - 1;
                mid[key] = idx;
                return idx;
            };
            for (int s = 0; s < subdivisions; s++)
            {
                var next = new List<int>();
                for (int i = 0; i < faces.Count; i += 3)
                {
                    int a = faces[i], b = faces[i + 1], c = faces[i + 2];
                    int ab = midpoint(a, b), bc = midpoint(b, c), ca = midpoint(c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                faces = next;
            }
            for (int i = 0; i < verts.Count; i++)
            {
                var v = verts[i] * radius;
                verts[i] = shape != null ? shape(v) : v;
            }
            var tris = new List<Vector3>(faces.Count);
            // ikosfera ma ściany zgodne z ruchem wskazówek zegara patrząc z zewnątrz w układzie Unity po zamianie kolejności
            for (int i = 0; i < faces.Count; i += 3)
            {
                tris.Add(verts[faces[i]]); tris.Add(verts[faces[i + 2]]); tris.Add(verts[faces[i + 1]]);
            }
            return Flat(tris, name);
        }

        /// <summary>Siatka z osobnymi wierzchołkami dla każdego trójkąta (płaskie, kanciaste cieniowanie).</summary>
        static Mesh Flat(List<Vector3> tris, string name)
        {
            var idx = new int[tris.Count];
            for (int i = 0; i < idx.Length; i++) idx[i] = i;
            var m = new Mesh { name = name };
            m.SetVertices(tris);
            m.SetTriangles(idx, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            // jeśli trójkąty wyszły odwrócone (normalne do środka), odwracamy kolejność
            var n = m.normals;
            if (n.Length > 0 && Vector3.Dot(n[0], tris[0] + tris[1] + tris[2]) < 0f)
            {
                for (int i = 0; i < idx.Length; i += 3) { int tmp = idx[i + 1]; idx[i + 1] = idx[i + 2]; idx[i + 2] = tmp; }
                m.SetTriangles(idx, 0);
                m.RecalculateNormals();
            }
            return m;
        }

        /// <summary>Podmienia siatkę bryły na kanciastą wersję low-poly.</summary>
        public static GameObject LowPoly(GameObject go)
        {
            var mf = go.GetComponent<MeshFilter>();
            if (mf != null) mf.sharedMesh = LowPolySphere();
            return go;
        }

        public static GameObject Empty(string name, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go;
        }

        public static void Tint(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = Mat(c);
        }

        public static Color Hex(string hex)
        {
            Color c;
            return ColorUtility.TryParseHtmlString(hex, out c) ? c : Color.magenta;
        }
    }

    /// <summary>Drobne różnice API fizyki między wersjami Unity.</summary>
    public static class Phys
    {
        public static Vector3 GetVelocity(Rigidbody rb)
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }

        public static void SetVelocity(Rigidbody rb, Vector3 v)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = v;
#else
            rb.velocity = v;
#endif
        }

        public static void SetDamping(Rigidbody rb, float d)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = d;
#else
            rb.drag = d;
#endif
        }

        public static void Slippery(Collider c)
        {
#if UNITY_6000_0_OR_NEWER
            var m = new PhysicsMaterial("Potato");
            m.frictionCombine = PhysicsMaterialCombine.Minimum;
            m.bounceCombine = PhysicsMaterialCombine.Maximum;
#else
            var m = new PhysicMaterial("Potato");
            m.frictionCombine = PhysicMaterialCombine.Minimum;
            m.bounceCombine = PhysicMaterialCombine.Maximum;
#endif
            m.dynamicFriction = 0f;
            m.staticFriction = 0f;
            m.bounciness = 0.2f;
            c.sharedMaterial = m;
        }
    }
}
