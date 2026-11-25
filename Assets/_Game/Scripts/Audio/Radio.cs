using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Rádio da partida: Aleatória (padrão), uma música fixa ou Desligada, gravado em
    /// <see cref="SaveData.SettingsData.radio"/>. Só vale na partida; o menu sempre toca a música dele.
    /// Músicas trancadas (<see cref="RadioUnlock"/>) aparecem no seletor mas não tocam; a liberação sai das estatísticas,
    /// então quem já tem o título ganha a música na hora. Quem toca é o <see cref="MatchRadio"/>.
    /// </summary>
    public static class Radio
    {
        /// <summary>Valor gravado para "Desligada"; vazio = Aleatória.</summary>
        public const string Off = "off";

        private static RadioPlaylist playlist;
        private static bool loaded;

        public static IReadOnlyList<RadioTrack> Tracks
        {
            get
            {
                if (!loaded)
                {
                    loaded = true;
                    playlist = Resources.Load<RadioPlaylist>(RadioPlaylist.ResourcePath);
                }
                return playlist != null ? playlist.tracks : (IReadOnlyList<RadioTrack>)System.Array.Empty<RadioTrack>();
            }
        }

        /// <summary>Para os testes trocarem a lista.</summary>
        public static void SetPlaylistForTests(RadioPlaylist list)
        {
            playlist = list;
            loaded = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            playlist = null;
            loaded = false;
        }

        /// <summary>"" = Aleatória, <see cref="Off"/> = Desligada, senão o id da música.</summary>
        public static string Choice
        {
            get => GameSave.Data.settings.radio ?? string.Empty;
            set => GameSave.Data.settings.radio = value ?? string.Empty;
        }

        public static bool IsOff => Choice == Off;
        public static bool IsRandom => string.IsNullOrEmpty(Choice);

        public static bool IsUnlocked(RadioTrack track)
        {
            if (track == null) return false;
            var stats = GameSave.Data.stats.total;
            return track.unlock switch
            {
                RadioUnlock.TournamentTitle => stats.tournamentTitles > 0,
                RadioUnlock.LeagueTitle => stats.leagueTitles > 0,
                _ => true
            };
        }

        public static RadioTrack Find(string id)
        {
            foreach (var track in Tracks)
                if (track != null && track.id == id) return track;
            return null;
        }

        /// <summary>Nome da música no idioma do jogo.</summary>
        public static string NameOf(RadioTrack track) => track == null ? string.Empty : Txt.Get(track.displayName);

        /// <summary>O que liberar uma música trancada pede ("Ganhe um torneio").</summary>
        public static string UnlockHint(RadioTrack track) => track?.unlock switch
        {
            RadioUnlock.TournamentTitle => Txt.Get("Ganhe um torneio para liberar"),
            RadioUnlock.LeagueTitle => Txt.Get("Ganhe uma liga para liberar"),
            _ => string.Empty
        };

        private static List<RadioTrack> Playable(RadioTrack except = null)
        {
            var list = new List<RadioTrack>();
            foreach (var track in Tracks)
                if (track != null && track.cue != null && IsUnlocked(track) && track != except) list.Add(track);
            return list;
        }

        /// <summary>
        /// A música que deve tocar agora: a fixa (se liberada), uma sorteada na Aleatória (diferente da atual quando dá),
        /// ou nenhuma (Desligada ou sem músicas).
        /// </summary>
        public static RadioTrack Pick(RadioTrack current = null)
        {
            if (IsOff) return null;
            if (!IsRandom)
            {
                var fixedTrack = Find(Choice);
                if (fixedTrack != null && fixedTrack.cue != null && IsUnlocked(fixedTrack)) return fixedTrack;
            }
            var options = Playable(current);
            if (options.Count == 0) options = Playable();
            return options.Count == 0 ? null : options[Random.Range(0, options.Count)];
        }

        /// <summary>
        /// "Próxima" na pausa: na Aleatória sorteia outra; com música fixa passa para a próxima liberada da lista e ela vira
        /// a escolha (gravada). Desligada: nada.
        /// </summary>
        public static RadioTrack Next(RadioTrack current)
        {
            if (IsOff) return null;
            if (IsRandom) return Pick(current);
            var tracks = Tracks;
            int start = current != null ? IndexOf(current) : IndexOf(Find(Choice));
            for (int k = 1; k <= tracks.Count; k++)
            {
                var track = tracks[((start + k) % tracks.Count + tracks.Count) % tracks.Count];
                if (track == null || track.cue == null || !IsUnlocked(track)) continue;
                Choice = track.id;
                return track;
            }
            return current;
        }

        private static int IndexOf(RadioTrack track)
        {
            var tracks = Tracks;
            for (int i = 0; i < tracks.Count; i++)
                if (tracks[i] == track) return i;
            return -1;
        }

        // ---- Seletor das Configurações: Aleatória, as músicas (trancadas também, para o jogador saber que existem), Desligada ----

        /// <summary>Opções do seletor, na ordem: "" (Aleatória), ids das músicas, <see cref="Off"/>.</summary>
        public static List<string> Options()
        {
            var options = new List<string> { string.Empty };
            foreach (var track in Tracks)
                if (track != null && !string.IsNullOrEmpty(track.id)) options.Add(track.id);
            options.Add(Off);
            return options;
        }

        /// <summary>Texto de uma opção do seletor.</summary>
        public static string OptionLabel(string option)
        {
            if (string.IsNullOrEmpty(option)) return Txt.Get("Aleatória");
            if (option == Off) return Txt.Get("Desligada");
            return NameOf(Find(option));
        }

        /// <summary>Opção do seletor que é uma música trancada.</summary>
        public static bool IsLockedOption(string option)
        {
            var track = Find(option);
            return track != null && !IsUnlocked(track);
        }

        /// <summary>Músicas que um título recém-ganho libera (para avisar no fim do torneio/liga).</summary>
        public static List<RadioTrack> UnlockedBy(RadioUnlock unlock)
        {
            var list = new List<RadioTrack>();
            foreach (var track in Tracks)
                if (track != null && track.unlock == unlock) list.Add(track);
            return list;
        }
    }
}
