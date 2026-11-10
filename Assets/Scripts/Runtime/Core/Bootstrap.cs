using UnityEngine;

namespace PotatoParty
{
    /// <summary>
    /// Uruchamia grę automatycznie po wciśnięciu Play w dowolnej scenie,
    /// więc projekt działa od razu po otwarciu, bez ręcznego ustawiania sceny.
    /// </summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            // Jeden obiekt Collision na wszystkie OnCollisionEnter (zamiast nowego przy każdym zderzeniu):
            // 6 ziemniaków + piłka + przeszkody = dziesiątki alokacji na sekundę mniej.
            // Bezpieczne, bo nigdzie nie przechowujemy obiektu Collision poza callbackiem.
            Physics.reuseCollisionCallbacks = true;

            // Fizyka pod game feel (patrz docs/Rework.md → "Fizyka"):
            // 100 Hz kroku (TimeDirector), 8/2 iteracje solvera – stabilne stosy 6 ziemniaków przy zderzeniach,
            // próg odbicia 1.5 m/s – miękkie lądowania nie "sprężynują", kontakt 0.01 m – brak lewitowania.
            Time.fixedDeltaTime = TimeDirector.BaseFixedDelta;
            Time.maximumDeltaTime = 0.1f;
            Physics.defaultSolverIterations = 8;
            Physics.defaultSolverVelocityIterations = 2;
            Physics.bounceThreshold = 1.5f;
            Physics.defaultContactOffset = 0.01f;
            Physics.sleepThreshold = 0.005f;
            GameManager.EnsureExists();
        }
    }
}
