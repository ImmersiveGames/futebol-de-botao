using System;
using System.Collections.Generic;

namespace FutebolDeBotao
{
    /// <summary>
    /// Tudo o que o jogo guarda no aparelho, num arquivo só (JSON). <see cref="version"/> sobe quando o formato mudar,
    /// para converter o save de quem já joga. Estatísticas, desbloqueios e torneio já têm lugar, mas ainda não são usados.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public SettingsData settings = new();
        public OptionsData options = new();
        /// <summary>Últimas escolhas da Seleção de times: 0 = Jogador 1 (baixo), 1 = Jogador 2 (cima).</summary>
        public PlayerChoices[] players = { new() { team = 0 }, new() { team = 1 } };

        // Reservados para as próximas fases.
        public StatsData stats = new();
        public List<string> unlocked = new();
        public List<TournamentData> tournaments = new();

        [Serializable]
        public sealed class SettingsData
        {
            public bool music = true;
            public int aiLevel = (int)AiLevel.Medium;
        }

        [Serializable]
        public sealed class OptionsData
        {
            /// <summary>Falso: ainda não salvou opções; vale o asset padrão.</summary>
            public bool saved;
            public int durationMinutes = 5;
            public int touchesPerTurn = 3;
            public bool goalAfterWallIsValid;
            public bool fouls = true;
            public int discsPerTeam = 3;
            public bool hasGoalkeeper = true;
            public int aimAssist;
        }

        [Serializable]
        public sealed class PlayerChoices
        {
            public int team;
            public int secondary;
            public int discPattern;
            public int keeperPattern;
            public int scheme3;
            public int scheme5;
        }

        [Serializable]
        public sealed class StatsData
        {
            public int played;
            public int wins;
            public int draws;
            public int losses;
            public int goalsFor;
            public int goalsAgainst;
        }

        [Serializable]
        public sealed class TournamentData
        {
            /// <summary>Tamanho da torre: 3, 5, 7 ou 10 degraus.</summary>
            public int steps;
            public int bestStep;
            public int completed;
        }
    }
}
