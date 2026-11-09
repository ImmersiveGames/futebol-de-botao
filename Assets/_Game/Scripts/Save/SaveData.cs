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
        public const int CurrentVersion = 4;

        public int version = CurrentVersion;
        public SettingsData settings = new();
        public OptionsData options = new();
        /// <summary>Últimas escolhas da Seleção de times: 0 = Jogador 1 (baixo), 1 = Jogador 2 (cima).</summary>
        public PlayerChoices[] players = { new() { team = 0 }, new() { team = 1 } };

        /// <summary>Estatísticas do jogador contra a IA (versão 2). Partidas de 2 jogadores não entram.</summary>
        public StatsData stats = new();
        /// <summary>Meu time (versão 3): nome, sigla, cores, camisas e moedas do jogador.</summary>
        public ProfileData profile = new();
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
            /// <summary>Prorrogação: 0 (Desligado), 50 ou 100 (% do tempo).</summary>
            public int extraTimePercent;
            public bool penalties;
        }

        [Serializable]
        public sealed class PlayerChoices
        {
            public int team;
            public int secondary;
            /// <summary>Saves antigos: posição do padrão na folha (agora vale o id).</summary>
            public int discPattern;
            public int keeperPattern;
            /// <summary>Id do padrão de camisa (<see cref="ShirtPattern"/>) dos botões e do goleiro.</summary>
            public string discPatternId;
            public string keeperPatternId;
            public int scheme3;
            public int scheme5;
        }

        /// <summary>O time do jogador (tela Meu time). As cores são guardadas em si, não a posição na lista de cores.</summary>
        [Serializable]
        public sealed class ProfileData
        {
            public string teamName = PlayerProfile.DefaultName;
            public string abbreviation = PlayerProfile.DefaultAbbreviation;
            public UnityEngine.Color primary = PlayerProfile.DefaultPrimary;
            public UnityEngine.Color secondary = PlayerProfile.DefaultSecondary;
            public string discPatternId;
            public string keeperPatternId;
            public int coins;
            /// <summary>Mesa de casa (aba Mesa do Meu time): ids da mesa, do feltro e da bola e o nome da cor das traves. Vazio: a primeira liberada.</summary>
            public string tableId;
            public string feltId;
            public string goalColorName;
            public string ballId;
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
            /// <summary>Moedas ganhas em partidas (versão 4; as do menu de teste não contam).</summary>
            public int coinsEarned;
            public int coinsSpent;
            public int itemsBought;
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
            /// <summary>Disputas de pênaltis (no fim da partida empatada; pênalti de falta não conta).</summary>
            public int shootoutWins;
            public int shootoutLosses;
            public int shootoutGoals;
            public int shootoutConceded;
            /// <summary>Somados só das partidas terminadas.</summary>
            public MatchNumbers numbers = new();
            /// <summary>Moedas que as partidas pagaram.</summary>
            public int coins;

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
            public int coins;
            /// <summary>Teve disputa de pênaltis: gols do jogador e da IA nela.</summary>
            public bool hasPenalties;
            public int playerPenalties;
            public int aiPenalties;
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
