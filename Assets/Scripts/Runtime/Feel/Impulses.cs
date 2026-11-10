using Unity.Cinemachine;
using UnityEngine;

namespace PotatoParty
{
    /// <summary>Rodzaje wstrząsu – każdy ma własny kształt sygnału Cinemachine.</summary>
    public enum ImpulseKind
    {
        /// <summary>Ciężkie lądowanie, tupnięcie (Bump, 0.14 s).</summary>
        Land,
        /// <summary>Trafienie zrywem (Recoil, 0.2 s) – kierunkowe, w stronę ciosu.</summary>
        Hit,
        /// <summary>Uderzenie z otoczenia: belka, wahadło, garnek (Bump, 0.26 s).</summary>
        Slam,
        /// <summary>Wybuch (Explosion, 0.55 s, zanika z odległością).</summary>
        Explosion,
        /// <summary>Długie dudnienie: kombajn, trzęsienie lodu (Rumble, 1.1 s).</summary>
        Rumble,
    }

    /// <summary>
    /// Wstrząsy kamery oparte na <b>Cinemachine Impulse</b>. Jedno ukryte źródło na rodzaj; każde wywołanie
    /// emituje impuls w punkcie świata z kierunkiem i siłą (= długość wektora prędkości). Kamera ma
    /// <c>CinemachineImpulseListener</c> (patrz <see cref="CinematicCamera"/>), więc wstrząs jest
    /// fizycznie "w świecie": słabnie z odległością od kamery i ma kierunek, który oko czyta.
    /// <para>Wartości (Inspektor → Impulse Source → Impulse Definition):</para>
    /// <code>
    ///            Shape      Duration  Type         Dissipation  Radius
    /// Land       Bump       0.14      Uniform      –            –
    /// Hit        Recoil     0.20      Uniform      –            –
    /// Slam       Bump       0.26      Dissipating  0.35         –
    /// Explosion  Explosion  0.55      Dissipating  0.25         –
    /// Rumble     Rumble     1.10      Uniform      –            –
    /// </code>
    /// </summary>
    public static class Impulses
    {
        /// <summary>Globalny mnożnik (ustawienia: "Wstrząsy ekranu"). 0 = wyłączone.</summary>
        public static float Strength = 1f;

        static CinemachineImpulseSource[] sources;

        static void Ensure()
        {
            if (sources != null && sources[0] != null) return;
            var go = new GameObject("Impulse Sources");
            go.hideFlags = HideFlags.HideInHierarchy;
            Object.DontDestroyOnLoad(go);
            int n = System.Enum.GetValues(typeof(ImpulseKind)).Length;
            sources = new CinemachineImpulseSource[n];
            for (int i = 0; i < n; i++)
            {
                var s = go.AddComponent<CinemachineImpulseSource>();
                Configure(s.ImpulseDefinition, (ImpulseKind)i);
                s.DefaultVelocity = Vector3.down;
                sources[i] = s;
            }
            // impulsy płyną w czasie rzeczywistym: wstrząs gra także w hit-stopie i slow-motion
            if (CinemachineImpulseManager.Instance != null) CinemachineImpulseManager.Instance.IgnoreTimeScale = true;
        }

        static void Configure(CinemachineImpulseDefinition d, ImpulseKind kind)
        {
            d.ImpulseChannel = 1;
            d.AmplitudeGain = 1f;
            d.FrequencyGain = 1f;
            switch (kind)
            {
                case ImpulseKind.Land:
                    d.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
                    d.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
                    d.ImpulseDuration = 0.14f;
                    break;
                case ImpulseKind.Hit:
                    d.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Recoil;
                    d.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
                    d.ImpulseDuration = 0.2f;
                    break;
                case ImpulseKind.Slam:
                    d.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
                    d.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Dissipating;
                    d.DissipationRate = 0.35f;
                    d.ImpulseDuration = 0.26f;
                    break;
                case ImpulseKind.Explosion:
                    d.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Explosion;
                    d.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Dissipating;
                    d.DissipationRate = 0.25f;
                    d.ImpulseDuration = 0.55f;
                    break;
                case ImpulseKind.Rumble:
                    d.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Rumble;
                    d.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
                    d.ImpulseDuration = 1.1f;
                    break;
            }
        }

        /// <summary>
        /// Wstrząs w punkcie świata. dir – kierunek "pchnięcia" kamery (np. kierunek ciosu), force w metrach
        /// wychylenia przy źródle: 0.05 muśnięcie, 0.15 cios, 0.35 wybuch, 0.6 bomba w twarz.
        /// </summary>
        public static void Fire(ImpulseKind kind, Vector3 position, Vector3 dir, float force)
        {
            if (Strength <= 0f || force <= 0f) return;
            Ensure();
            if (dir.sqrMagnitude < 1e-4f) dir = Vector3.down;
            // kierunek: składowa pionowa zawsze lekko w dół – kamera "siada" przy uderzeniu, nie podskakuje
            dir = dir.normalized;
            dir = (dir + Vector3.down * 0.35f).normalized;
            sources[(int)kind].GenerateImpulseAt(position, dir * force * Strength);
        }
    }
}
