using UnityEngine;

namespace PotatoParty
{
    /// <summary>Dekoracje kolejnych scenerii (każda minigra ma swoją).</summary>
    public static partial class MapBuilder
    {
        static void Decorate(Transform env, MapTheme t, float inner, float y, System.Random rng)
        {
            switch (t.Id)
            {
                case MapId.Orchard: Orchard(env, inner, y, rng); break;
                case MapId.Kitchen: Kitchen(env, inner, y, rng); break;
                case MapId.Volcano: Volcano(env, t, inner, y, rng); break;
                case MapId.Canteen: Canteen(env, inner, y, rng); break;
                case MapId.Stadium: Stadium(env, inner, y, rng); break;
                case MapId.Playground: Playground(env, inner, y, rng); break;
                case MapId.Forest: Forest(env, inner, y, rng); break;
                case MapId.Cornfield: Cornfield(env, inner, y, rng); break;
                case MapId.Garden: Garden(env, inner, y, rng); break;
            }
        }

        static Transform Group(Transform env, string name, Vector3 pos, float yRot = 0f)
        {
            var g = Shapes.Empty(name, env, pos).transform;
            g.localRotation = Quaternion.Euler(0, yRot, 0);
            return g;
        }

        // ---------------- Sad ----------------
        static void Orchard(Transform env, float inner, float y, System.Random rng)
        {
            for (int i = 0; i < 24; i++)
            {
                var p = RingPoint(rng, inner, y, 3f, 26f);
                Tree(env, p, R(rng, 0.9f, 1.3f), Shapes.Hex("#5CBF4F"));
                for (int k = 0; k < 6; k++)
                {
                    var off = Random.onUnitSphere * 1.3f;
                    off.y = Mathf.Abs(off.y) * 0.8f;
                    Shapes.Prim(PrimitiveType.Sphere, env, p + Vector3.up * 3.2f + off, Vector3.one * 0.35f, k % 3 == 0 ? Shapes.Hex("#F2C230") : Shapes.Hex("#E2463F"), false, 0.6f);
                }
            }
            for (int i = 0; i < 10; i++)
            {
                var p = RingPoint(rng, inner, y, 1f, 8f);
                var crate = Group(env, "Skrzynka", p, R(rng, 0, 90));
                Shapes.Box(crate, new Vector3(0, 0.4f, 0), new Vector3(1.2f, 0.8f, 0.9f), Shapes.Hex("#B07A45"), false);
                for (int k = 0; k < 4; k++)
                    Shapes.Prim(PrimitiveType.Sphere, crate, new Vector3(-0.35f + (k % 2) * 0.7f, 0.85f, -0.2f + (k / 2) * 0.4f), Vector3.one * 0.35f, Shapes.Hex("#E2463F"));
            }
            var ladder = Group(env, "Drabina", new Vector3(6, y, inner + 4f), 20f);
            for (int s = -1; s <= 1; s += 2) Shapes.Box(ladder, new Vector3(0.5f * s, 1.5f, 0), new Vector3(0.12f, 3f, 0.12f), Shapes.Hex("#C9874A"), false);
            for (int k = 0; k < 5; k++) Shapes.Box(ladder, new Vector3(0, 0.4f + k * 0.6f, 0), new Vector3(1f, 0.08f, 0.1f), Shapes.Hex("#C9874A"), false);
        }

        // ---------------- Kuchnia ----------------
        static void Kitchen(Transform env, float inner, float y, System.Random rng)
        {
            // Olbrzymie naczynia na blacie
            for (int i = 0; i < 6; i++)
            {
                var p = RingPoint(rng, inner, y, 4f, 22f);
                MapBuilder.Disc(env, p + Vector3.up * 0.15f, R(rng, 2.5f, 4f), 0.3f, Color.white, false);
            }
            for (int i = 0; i < 6; i++)
            {
                var p = RingPoint(rng, inner, y, 4f, 22f);
                var cup = Group(env, "Kubek", p);
                Shapes.Prim(PrimitiveType.Cylinder, cup, new Vector3(0, 2f, 0), new Vector3(3f, 2f, 3f), i % 2 == 0 ? Shapes.Hex("#FF6F91") : Shapes.Hex("#2EC4B6"));
                var handle = Shapes.Prim(PrimitiveType.Cylinder, cup, new Vector3(1.8f, 2.2f, 0), new Vector3(0.4f, 0.8f, 0.4f), Color.white);
                handle.transform.localRotation = Quaternion.Euler(0, 0, 90);
            }
            for (int i = 0; i < 5; i++)
            {
                var p = RingPoint(rng, inner, y, 3f, 20f);
                var bottle = Group(env, "Butelka", p);
                Shapes.Prim(PrimitiveType.Cylinder, bottle, new Vector3(0, 2.5f, 0), new Vector3(1.6f, 2.5f, 1.6f), Shapes.Hex("#7FD38A"), false, 0.9f);
                Shapes.Prim(PrimitiveType.Cylinder, bottle, new Vector3(0, 5.6f, 0), new Vector3(0.6f, 0.7f, 0.6f), Shapes.Hex("#7FD38A"), false, 0.9f);
                Shapes.Prim(PrimitiveType.Cylinder, bottle, new Vector3(0, 6.4f, 0), new Vector3(0.65f, 0.15f, 0.65f), Shapes.Hex("#E2463F"));
            }
            // Kuchenka z palnikami w tle
            var stove = Group(env, "Kuchenka", new Vector3(-8f, y, inner + 12f));
            Shapes.Box(stove, new Vector3(0, 1.5f, 0), new Vector3(14f, 3f, 8f), Shapes.Hex("#ECECEC"), false);
            for (int k = 0; k < 4; k++)
                MapBuilder.Disc(stove, new Vector3(-3.5f + (k % 2) * 7f, 3.05f, -1.8f + (k / 2) * 3.6f), 1.4f, 0.1f, Shapes.Hex("#333333"), false);
            var pot = Shapes.Prim(PrimitiveType.Cylinder, stove, new Vector3(-3.5f, 4.6f, -1.8f), new Vector3(3f, 1.5f, 3f), Shapes.Hex("#9AA7B2"), false, 0.8f);
            pot.name = "Garnek";
            // Kafelki na ścianie
            for (int k = 0; k < 10; k++)
                Shapes.Box(env, new Vector3(-30f + k * 6.5f, y + 6f, inner + 26f), new Vector3(6.2f, 12f, 0.5f), k % 2 == 0 ? Color.white : Shapes.Hex("#CFE8FF"), false);
        }

        // ---------------- Wulkan ----------------
        static void Volcano(Transform env, MapTheme t, float inner, float y, System.Random rng)
        {
            var cone = Group(env, "Wulkan", new Vector3(0, y, inner + 22f));
            for (int k = 0; k < 6; k++)
                Shapes.Prim(PrimitiveType.Cylinder, cone, new Vector3(0, k * 2.4f + 1.2f, 0), new Vector3(28f - k * 4f, 1.2f, 28f - k * 4f), k % 2 == 0 ? t.Land : t.LandAlt);
            var crater = Shapes.Prim(PrimitiveType.Cylinder, cone, new Vector3(0, 14.6f, 0), new Vector3(5f, 0.2f, 5f), t.Pit);
            crater.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(t.Pit, 0.3f, 1.5f);
            for (int k = 0; k < 4; k++)
            {
                var smoke = Shapes.Prim(PrimitiveType.Sphere, cone, new Vector3(R(rng, -1, 1), 17f + k * 2.5f, R(rng, -1, 1)), Vector3.one * (3f + k), Shapes.Hex("#8C8380"));
                smoke.AddComponent<Bobber>().Amplitude = 0.6f;
            }
            for (int i = 0; i < 26; i++)
            {
                var p = RingPoint(rng, inner, y + 0.4f, 1f, 24f);
                var rock = Shapes.Prim(PrimitiveType.Cube, env, p, new Vector3(R(rng, 0.8f, 2.4f), R(rng, 0.6f, 2f), R(rng, 0.8f, 2.4f)), Shapes.Hex("#2E2624"));
                rock.transform.localRotation = Quaternion.Euler(R(rng, -20, 20), R(rng, 0, 90), R(rng, -20, 20));
            }
            for (int i = 0; i < 12; i++)
            {
                var p = RingPoint(rng, inner, y + 0.05f, 1f, 18f);
                var crack = Shapes.Prim(PrimitiveType.Cube, env, p, new Vector3(0.25f, 0.1f, R(rng, 2f, 5f)), t.Pit);
                crack.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(t.Pit, 0.3f, 1.5f);
                crack.transform.localRotation = Quaternion.Euler(0, R(rng, 0, 180), 0);
            }
        }

        // ---------------- Stołówka ----------------
        static void Canteen(Transform env, float inner, float y, System.Random rng)
        {
            Color[] trays = { Shapes.Hex("#FF6F91"), Shapes.Hex("#2EC4B6"), Shapes.Hex("#FFB000"), Shapes.Hex("#8E6CEF") };
            for (int i = 0; i < 10; i++)
            {
                var p = RingPoint(rng, inner, y, 3f, 18f);
                var table = Group(env, "Stol", p, R(rng, 0, 180));
                Shapes.Box(table, new Vector3(0, 1.1f, 0), new Vector3(4f, 0.2f, 1.6f), Shapes.Hex("#C98F5A"), false);
                for (int k = 0; k < 4; k++)
                    Shapes.Box(table, new Vector3(k < 2 ? -1.8f : 1.8f, 0.5f, k % 2 == 0 ? -0.6f : 0.6f), new Vector3(0.15f, 1f, 0.15f), Shapes.Hex("#7D8C99"), false);
                for (int k = -1; k <= 1; k += 2)
                    Shapes.Box(table, new Vector3(0, 0.6f, 1.3f * k), new Vector3(3.6f, 0.15f, 0.6f), Shapes.Hex("#7FB7E6"), false);
                Shapes.Box(table, new Vector3(R(rng, -1, 1), 1.25f, 0), new Vector3(0.9f, 0.08f, 0.6f), trays[i % trays.Length], false);
            }
            // Lada z garnkami
            var counter = Group(env, "Lada", new Vector3(0, y, inner + 10f));
            Shapes.Box(counter, new Vector3(0, 1.2f, 0), new Vector3(20f, 2.4f, 3f), Shapes.Hex("#B8C4CE"), false);
            for (int k = 0; k < 5; k++)
                Shapes.Prim(PrimitiveType.Cylinder, counter, new Vector3(-8f + k * 4f, 3f, 0), new Vector3(2.2f, 0.7f, 2.2f), Shapes.Hex("#9AA7B2"), false, 0.85f);
            var sign = Shapes.Box(counter, new Vector3(0, 6f, 1.4f), new Vector3(12f, 2f, 0.2f), Shapes.Hex("#FFB000"), false);
            sign.name = "Napis";
        }

        // ---------------- Stadion ----------------
        static void Stadium(Transform env, float inner, float y, System.Random rng)
        {
            Color[] fans = { Shapes.Hex("#E2463F"), Shapes.Hex("#3F7FE8"), Shapes.Hex("#FFD23F"), Color.white, Shapes.Hex("#5CC157") };
            for (int side = 0; side < 4; side++)
            {
                var stand = Group(env, "Trybuna", Quaternion.Euler(0, side * 90f, 0) * new Vector3(0, y, inner + 4f), side * 90f);
                for (int row = 0; row < 4; row++)
                {
                    float h = 1f + row * 1.2f;
                    Shapes.Box(stand, new Vector3(0, h * 0.5f, row * 1.6f), new Vector3(40f, h, 1.6f), row % 2 == 0 ? Shapes.Hex("#7A8796") : Shapes.Hex("#6A7686"), false);
                    for (int f = 0; f < 18; f++)
                    {
                        var fan = Shapes.Prim(PrimitiveType.Sphere, stand, new Vector3(-17f + f * 2f + R(rng, -0.3f, 0.3f), h + 0.45f, row * 1.6f), new Vector3(0.7f, 0.9f, 0.7f), fans[rng.Next(fans.Length)]);
                        var b = fan.AddComponent<Bobber>();
                        b.Amplitude = 0.25f;
                        b.Speed = R(rng, 4f, 7f);
                    }
                }
            }
            for (int k = 0; k < 4; k++)
            {
                var corner = Quaternion.Euler(0, 45f + k * 90f, 0) * new Vector3(0, y, inner + 12f);
                var tower = Group(env, "Reflektor", corner);
                Shapes.Prim(PrimitiveType.Cylinder, tower, new Vector3(0, 7f, 0), new Vector3(0.6f, 7f, 0.6f), Shapes.Hex("#B5B5B5"));
                var lamp = Shapes.Box(tower, new Vector3(0, 14.5f, 0), new Vector3(3f, 2f, 0.6f), Color.white, false);
                lamp.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(Color.white, 0.5f, 1.5f);
            }
        }

        // ---------------- Plac zabaw ----------------
        static void Playground(Transform env, float inner, float y, System.Random rng)
        {
            var slide = Group(env, "Zjezdzalnia", new Vector3(-8f, y, inner + 6f), 20f);
            Shapes.Box(slide, new Vector3(0, 2f, 0), new Vector3(2f, 4f, 2f), Shapes.Hex("#2EC4B6"), false);
            var chute = Shapes.Box(slide, new Vector3(0, 2f, -3f), new Vector3(1.6f, 0.2f, 5.5f), Shapes.Hex("#FFD23F"), false);
            chute.transform.localRotation = Quaternion.Euler(-38f, 0, 0);
            var swing = Group(env, "Hustawka", new Vector3(9f, y, inner + 7f), -15f);
            for (int s = -1; s <= 1; s += 2)
            {
                var leg = Shapes.Box(swing, new Vector3(3f * s, 2f, 0), new Vector3(0.25f, 4.2f, 0.25f), Shapes.Hex("#FF6F91"), false);
                leg.transform.localRotation = Quaternion.Euler(0, 0, 8f * s);
            }
            Shapes.Box(swing, new Vector3(0, 4f, 0), new Vector3(6.4f, 0.25f, 0.25f), Shapes.Hex("#FF6F91"), false);
            for (int s = -1; s <= 1; s += 2)
            {
                var seat = Shapes.Empty("Siedzisko", swing, new Vector3(1.4f * s, 4f, 0)).transform;
                Shapes.Box(seat, new Vector3(0, -1.6f, 0), new Vector3(0.05f, 3.2f, 0.05f), Color.gray, false);
                Shapes.Box(seat, new Vector3(0, -3.2f, 0), new Vector3(0.9f, 0.1f, 0.5f), Shapes.Hex("#8E6CEF"), false);
                seat.gameObject.AddComponent<Swinger>().Phase = s;
            }
            var sandbox = Group(env, "Piaskownica", new Vector3(0, y, -(inner + 7f)));
            Shapes.Box(sandbox, new Vector3(0, 0.1f, 0), new Vector3(7f, 0.2f, 7f), Shapes.Hex("#F1DC9B"), false);
            for (int s = 0; s < 4; s++)
            {
                var edge = Shapes.Box(sandbox, Quaternion.Euler(0, s * 90f, 0) * new Vector3(0, 0.3f, 3.6f), new Vector3(7.4f, 0.6f, 0.4f), Shapes.Hex("#E2463F"), false);
                edge.transform.localRotation = Quaternion.Euler(0, s * 90f, 0);
            }
            Color[] balls = { Shapes.Hex("#FF6F91"), Shapes.Hex("#FFD23F"), Shapes.Hex("#2EC4B6"), Shapes.Hex("#8E6CEF") };
            for (int i = 0; i < 12; i++)
            {
                var p = RingPoint(rng, inner, y + 0.5f, 1f, 16f);
                Shapes.Prim(PrimitiveType.Sphere, env, p, Vector3.one * R(rng, 0.6f, 1.2f), balls[i % balls.Length], false, 0.6f);
            }
            for (int i = 0; i < 12; i++) Tree(env, RingPoint(rng, inner, y, 12f, 28f), R(rng, 0.9f, 1.2f), Shapes.Hex("#5CC157"));
        }

        // ---------------- Las ----------------
        static void Forest(Transform env, float inner, float y, System.Random rng)
        {
            for (int i = 0; i < 40; i++)
            {
                var p = RingPoint(rng, inner, y, 2f, 30f);
                float s = R(rng, 1f, 1.6f);
                var pine = Group(env, "Sosna", p);
                Shapes.Prim(PrimitiveType.Cylinder, pine, new Vector3(0, 1f * s, 0), new Vector3(0.5f, 1f, 0.5f) * s, Shapes.Hex("#6B4A2B"));
                for (int k = 0; k < 3; k++)
                {
                    float w = (2.6f - k * 0.7f) * s;
                    Shapes.Prim(PrimitiveType.Sphere, pine, new Vector3(0, (2.2f + k * 1.2f) * s, 0), new Vector3(w, 1.5f * s, w), k % 2 == 0 ? Shapes.Hex("#2F7D4B") : Shapes.Hex("#3A8F56"));
                }
            }
            for (int i = 0; i < 14; i++)
            {
                var p = RingPoint(rng, inner, y, 0.5f, 10f);
                var m = Group(env, "Grzyb", p);
                float s = R(rng, 0.6f, 1.3f);
                Shapes.Prim(PrimitiveType.Cylinder, m, new Vector3(0, 0.4f * s, 0), new Vector3(0.35f, 0.4f, 0.35f) * s, Shapes.Hex("#F3EBDD"));
                var cap = Shapes.Prim(PrimitiveType.Sphere, m, new Vector3(0, 0.85f * s, 0), new Vector3(1.2f, 0.6f, 1.2f) * s, Shapes.Hex("#E2463F"));
                for (int k = 0; k < 4; k++)
                    Shapes.Prim(PrimitiveType.Sphere, cap.transform, new Vector3(Mathf.Cos(k * 1.6f) * 0.3f, 0.35f, Mathf.Sin(k * 1.6f) * 0.3f), Vector3.one * 0.18f, Color.white);
            }
            for (int i = 0; i < 8; i++)
            {
                var p = RingPoint(rng, inner, y + 0.35f, 1f, 12f);
                var log = Shapes.Prim(PrimitiveType.Cylinder, env, p, new Vector3(0.7f, R(rng, 1.2f, 2f), 0.7f), Shapes.Hex("#8A5A33"));
                log.transform.localRotation = Quaternion.Euler(90, R(rng, 0, 180), 0);
                var rock = Shapes.Prim(PrimitiveType.Sphere, env, RingPoint(rng, inner, y + 0.3f, 1f, 14f), new Vector3(R(rng, 1f, 2f), R(rng, 0.6f, 1.2f), R(rng, 1f, 2f)), Shapes.Hex("#8C9488"));
                rock.name = "Kamien";
            }
        }

        // ---------------- Pole kukurydzy ----------------
        static void Cornfield(Transform env, float inner, float y, System.Random rng)
        {
            for (int i = 0; i < 160; i++)
            {
                var p = RingPoint(rng, inner, y, 1f, 22f);
                CornStalk(env, p, R(rng, 0.9f, 1.3f));
            }
            var scare = Group(env, "Strach na wroble", new Vector3(5f, y, inner + 5f));
            Shapes.Box(scare, new Vector3(0, 1.8f, 0), new Vector3(0.2f, 3.6f, 0.2f), Shapes.Hex("#8A5A33"), false);
            Shapes.Box(scare, new Vector3(0, 2.8f, 0), new Vector3(2.6f, 0.2f, 0.2f), Shapes.Hex("#8A5A33"), false);
            Shapes.Box(scare, new Vector3(0, 2.4f, 0), new Vector3(1.2f, 1.2f, 0.5f), Shapes.Hex("#3F7FE8"), false);
            Shapes.Prim(PrimitiveType.Sphere, scare, new Vector3(0, 3.6f, 0), Vector3.one * 0.8f, Shapes.Hex("#E8C35A"));
            Shapes.Prim(PrimitiveType.Cylinder, scare, new Vector3(0, 4.05f, 0), new Vector3(1.4f, 0.05f, 1.4f), Shapes.Hex("#C9A45C"));
            Shapes.Prim(PrimitiveType.Cylinder, scare, new Vector3(0, 4.25f, 0), new Vector3(0.7f, 0.25f, 0.7f), Shapes.Hex("#C9A45C"));
            var silo = Group(env, "Silos", new Vector3(-12f, y, inner + 14f));
            Shapes.Prim(PrimitiveType.Cylinder, silo, new Vector3(0, 5f, 0), new Vector3(5f, 5f, 5f), Shapes.Hex("#D8DDE2"), false, 0.7f);
            Shapes.Prim(PrimitiveType.Sphere, silo, new Vector3(0, 10f, 0), new Vector3(5f, 3f, 5f), Shapes.Hex("#E2463F"));
        }

        public static GameObject CornStalk(Transform parent, Vector3 p, float s)
        {
            var stalk = Shapes.Empty("Kukurydza", parent, p);
            Shapes.Prim(PrimitiveType.Cylinder, stalk.transform, new Vector3(0, 1.2f * s, 0), new Vector3(0.15f, 1.2f * s, 0.15f), Shapes.Hex("#7DB548"));
            var leaf = Shapes.Prim(PrimitiveType.Cube, stalk.transform, new Vector3(0, 1.4f * s, 0), new Vector3(1.2f, 0.05f, 0.3f), Shapes.Hex("#6FA83F"));
            leaf.transform.localRotation = Quaternion.Euler(0, Random.Range(0, 180f), 25f);
            Shapes.Prim(PrimitiveType.Sphere, stalk.transform, new Vector3(0.12f, 1.9f * s, 0), new Vector3(0.22f, 0.5f, 0.22f), Shapes.Hex("#F5D042"));
            Shapes.Prim(PrimitiveType.Sphere, stalk.transform, new Vector3(0, 2.5f * s, 0), new Vector3(0.3f, 0.4f, 0.3f), Shapes.Hex("#D9C27A"));
            return stalk;
        }

        // ---------------- Ogród ----------------
        static void Garden(Transform env, float inner, float y, System.Random rng)
        {
            Color[] flowers = { Shapes.Hex("#FF8FB1"), Shapes.Hex("#FFD23F"), Shapes.Hex("#B98CFF"), Color.white, Shapes.Hex("#FF7B54") };
            for (int i = 0; i < 70; i++)
            {
                var p = RingPoint(rng, inner, y, 0.5f, 12f);
                Shapes.Prim(PrimitiveType.Cylinder, env, p + Vector3.up * 0.3f, new Vector3(0.06f, 0.3f, 0.06f), Shapes.Hex("#3E9A3A"));
                Shapes.Prim(PrimitiveType.Sphere, env, p + Vector3.up * 0.65f, Vector3.one * 0.35f, flowers[rng.Next(flowers.Length)]);
            }
            var fountain = Group(env, "Fontanna", new Vector3(0, y, inner + 9f));
            MapBuilder.Disc(fountain, new Vector3(0, 0.4f, 0), 3.5f, 0.8f, Shapes.Hex("#D9D4CC"), false);
            MapBuilder.Disc(fountain, new Vector3(0, 0.75f, 0), 3.1f, 0.1f, Shapes.Hex("#6EC6F0"), false).AddComponent<Bobber>().Amplitude = 0.03f;
            Shapes.Prim(PrimitiveType.Cylinder, fountain, new Vector3(0, 1.6f, 0), new Vector3(0.6f, 1.2f, 0.6f), Shapes.Hex("#D9D4CC"));
            var water = Shapes.Prim(PrimitiveType.Sphere, fountain, new Vector3(0, 3.1f, 0), new Vector3(1.4f, 0.8f, 1.4f), Shapes.Hex("#6EC6F0"), false, 0.9f);
            water.AddComponent<Bobber>().Amplitude = 0.15f;
            for (int i = 0; i < 4; i++)
            {
                var p = RingPoint(rng, inner, y, 1f, 8f);
                var gnome = Group(env, "Krasnal", p, R(rng, 0, 360));
                Shapes.Prim(PrimitiveType.Sphere, gnome, new Vector3(0, 0.45f, 0), new Vector3(0.7f, 0.9f, 0.7f), Shapes.Hex("#3F7FE8"));
                Shapes.Prim(PrimitiveType.Sphere, gnome, new Vector3(0, 1.0f, 0), Vector3.one * 0.5f, Shapes.Hex("#F2C9A0"));
                Shapes.Prim(PrimitiveType.Sphere, gnome, new Vector3(0, 0.85f, 0.15f), new Vector3(0.4f, 0.45f, 0.3f), Color.white);
                Shapes.Prim(PrimitiveType.Cylinder, gnome, new Vector3(0, 1.45f, 0), new Vector3(0.45f, 0.35f, 0.45f), Shapes.Hex("#E2463F"));
            }
            for (int i = 0; i < 16; i++)
            {
                var p = RingPoint(rng, inner, y, 8f, 26f);
                Shapes.Box(env, p + Vector3.up * 1f, new Vector3(R(rng, 2f, 5f), 2f, R(rng, 1f, 2f)), Shapes.Hex("#3F8F3A"), false, R(rng, 0, 180));
            }
        }
    }

    /// <summary>Huśtanie się (np. huśtawki na placu zabaw).</summary>
    public class Swinger : MonoBehaviour
    {
        public float Phase;
        void Update() { transform.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 1.8f + Phase) * 25f, 0, 0); }
    }
}
