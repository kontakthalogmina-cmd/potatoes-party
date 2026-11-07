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
            GameManager.EnsureExists();
        }
    }
}
