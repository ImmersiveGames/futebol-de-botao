using System;
using System.Collections.Generic;

namespace FutebolDeBotao
{
    /// <summary>
    /// Tudo o que o jogo guarda no aparelho, num arquivo só (JSON). <see cref="version"/> sobe quando o formato mudar,
    /// para converter o save de quem já joga. Desbloqueios e torneio já têm lugar, mas ainda não são usados.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 2;

        public int version = CurrentVersion;
        public SettingsData settings = new();
        public OptionsData options = new();
        /// <summary>Últimas escolhas da Seleção de times: 0 = Jogador 1 (baixo), 1 = Jogador 2 (cima).</summary>
        public PlayerChoices[] players = { new() { team = 0 }, new() { team = 1 } };

        /// <summary>Estatísticas do jogador contra a IA (versão 2). Partidas de 2 jogadores não entram.</summary>
        public StatsData stats = new();
        // Reservados para as próximas fases.
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
            /// <summary>Todos os níveis juntos (a sequência de vitórias é a de todas as partidas, de qualquer nível).</summary>
            public LevelStats total = new();
            /// <summary>Por nível da IA: 0 = Fácil, 1 = Médio, 2 = Difícil.</summary>
            public LevelStats[] levels = { new(), new(), new() };
            /// <summary>Nível da partida contra a IA em andamento; -1 = nenhuma. Sobrou ao abrir o jogo = abandonada.</summary>
            public int inProgressLevel = -1;
            /// <summary>Já houve uma partida terminada (a aba Última partida).</summary>
            public bool hasLast;
            public LastMatchData last = new();
        }

        /// <summary>Números do jogador numa partida (ou somados em várias).</summary>
        [Serializable]
        public sealed class MatchNumbers
        {
            /// <summary>Gols de verdade (sem os 3 do W.O.).</summary>
            public int goals;
            /// <summary>Gols sofridos, contando os gols contra do próprio jogador.</summary>
            public int conceded;
            public int ownGoals;
            /// <summary>"Pro Gol!" avisados.</summary>
            public int shots;
            /// <summary>Gols que saíram de um "Pro Gol!" (para a conversão).</summary>
            public int shotGoals;
            public int fouls;
            public int yellow;
            public int red;
            /// <summary>Petelecos (e tiros de meta) em que o time tocou na bola.</summary>
            public int touches;
            /// <summary>Petelecos em que nenhum botão do time tocou na bola.</summary>
            public int misses;

            public void Add(MatchNumbers other)
            {
                goals += other.goals;
                conceded += other.conceded;
                ownGoals += other.ownGoals;
                shots += other.shots;
                shotGoals += other.shotGoals;
                fouls += other.fouls;
                yellow += other.yellow;
                red += other.red;
                touches += other.touches;
                misses += other.misses;
            }
        }

        [Serializable]
        public sealed class LevelStats
        {
            public int wins;
            public int draws;
            /// <summary>Inclui o W.O. contra o jogador.</summary>
            public int losses;
            /// <summary>Saiu para o menu, reiniciou ou fechou o jogo no meio da partida.</summary>
            public int abandoned;
            public int streak;
            public int bestStreak;
            /// <summary>Somados só das partidas terminadas.</summary>
            public MatchNumbers numbers = new();

            public int Played => wins + draws + losses + abandoned;
            public int Finished => wins + draws + losses;
        }

        /// <summary>A última partida terminada contra a IA.</summary>
        [Serializable]
        public sealed class LastMatchData
        {
            public int level;
            /// <summary>Placar final (com os 3 gols do W.O.).</summary>
            public int playerScore;
            public int aiScore;
            public bool walkover;
            public MatchNumbers numbers = new();
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
