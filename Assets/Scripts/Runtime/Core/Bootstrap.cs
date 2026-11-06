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
            GameManager.EnsureExists();
        }
    }
}
