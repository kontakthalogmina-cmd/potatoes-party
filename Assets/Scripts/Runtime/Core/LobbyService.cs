using System.Collections.Generic;
using UnityEngine;

namespace PotatoParty
{
    public class LobbyInfo
    {
        public string Name;
        public string Code;
        public bool IsPrivate;
        public int Players;
        public int MaxPlayers = MatchSettings.MaxPlayers;
        public int RoundOptionIndex = 0;
        public MapSelectMode MapMode;
        public string HostName;
        public bool HostedByBots;
    }

    /// <summary>
    /// Miejsce, w którym gra szuka i ogłasza lobby. Teraz działa lokalnie (na jednym komputerze);
    /// wersję online (np. Unity Lobby + Relay) można podłączyć, implementując ten sam interfejs.
    /// </summary>
    public interface ILobbyService
    {
        bool IsOnline { get; }
        string Description { get; }
        void Publish(LobbyInfo lobby);
        void Unpublish(string code);
        List<LobbyInfo> ListOpen();
        LobbyInfo FindByCode(string code);
    }

    public class LocalLobbyService : ILobbyService
    {
        readonly List<LobbyInfo> published = new List<LobbyInfo>();
        readonly List<LobbyInfo> botRooms = new List<LobbyInfo>();

        public bool IsOnline { get { return false; } }
        public string Description { get { return "Tryb lokalny: grasz na jednym komputerze (klawiatura + pady). Otwarte pokoje prowadzą boty."; } }

        public LocalLobbyService()
        {
            string[] names = { "Pokój Frytka", "Kartoflisko", "Impreza u Pyry", "Purée Party" };
            for (int i = 0; i < names.Length; i++)
            {
                botRooms.Add(new LobbyInfo
                {
                    Name = names[i],
                    Code = MatchSettings.NewCode(),
                    IsPrivate = false,
                    Players = Random.Range(1, 5),
                    RoundOptionIndex = Random.Range(0, MatchSettings.RoundOptions.Length),
                    MapMode = (MapSelectMode)(i % 3),
                    HostName = PlayerColors.BotNames[i],
                    HostedByBots = true,
                });
            }
        }

        public void Publish(LobbyInfo lobby)
        {
            Unpublish(lobby.Code);
            published.Add(lobby);
        }

        public void Unpublish(string code)
        {
            published.RemoveAll(l => l.Code == code);
        }

        public List<LobbyInfo> ListOpen()
        {
            var list = new List<LobbyInfo>();
            foreach (var l in published) if (!l.IsPrivate) list.Add(l);
            list.AddRange(botRooms);
            return list;
        }

        public LobbyInfo FindByCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            code = code.Trim().ToUpperInvariant();
            foreach (var l in published) if (l.Code == code) return l;
            foreach (var l in botRooms) if (l.Code == code) return l;
            return null;
        }
    }
}
