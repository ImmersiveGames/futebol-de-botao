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
        public const int CurrentVersion = 6;

        public int version = CurrentVersion;
        public SettingsData settings = new();
        public OptionsData options = new();
        /// <summary>Últimas escolhas da Seleção de times: 0 = Jogador 1 (baixo), 1 = Jogador 2 (cima).</summary>
        public PlayerChoices[] players = { new() { team = 0 }, new() { team = 1 } };

        /// <summary>Estatísticas do jogador contra a IA (versão 2). Partidas de 2 jogadores não entram.</summary>
        public StatsData stats = new();
        /// <summary>Meu time (versão 3): nome, sigla, cores, camisas e moedas do jogador.</summary>
        public ProfileData profile = new();
        /// <summary>Itens comprados na Loja ("camisa:id", "bola:id"...).</summary>
        public List<string> unlocked = new();
        /// <summary>Torneio (versão 5): o em andamento, ou o último terminado (<see cref="TournamentRun.active"/> falso).</summary>
        public TournamentRun tournament = new();

        [Serializable]
        public sealed class SettingsData
        {
            public bool music = true;
            public int aiLevel = (int)AiLevel.Medium;
            /// <summary>Tamanho de torneio escolhido por último (id do asset em Resources/Torneios).</summary>
            public string tournamentId;
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
            /// <summary>Cor reserva do Meu time (cores parecidas com o adversário) e se ela troca a segunda cor.</summary>
            public bool hasReserve;
            public UnityEngine.Color reserve = UnityEngine.Color.white;
            public bool reserveReplacesSecondary;
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
            /// <summary>Por nível da IA: 0 = Fácil, 1 = Médio, 2 = Difícil, 3 = Pro (versão 6).</summary>
            public LevelStats[] levels = { new(), new(), new(), new() };
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
            /// <summary>Moedas que as partidas pagaram (e os prêmios dos torneios).</summary>
            public int coins;
            /// <summary>Torneios (versão 5): jogados (terminados ou abandonados), títulos, vices e 3º lugares.</summary>
            public int tournaments;
            public int tournamentTitles;
            public int tournamentRunnerUps;
            public int tournamentThirds;
            public int tournamentsAbandoned;

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

        /// <summary>Um torneio mata-mata: a chave (times por posição), os jogos e como o jogador terminou.</summary>
        [Serializable]
        public sealed class TournamentRun
        {
            /// <summary>Tem torneio guardado (em andamento ou terminado).</summary>
            public bool exists;
            /// <summary>Em andamento: falso depois que o jogador terminou (campeão, eliminado ou abandonou).</summary>
            public bool active;
            /// <summary>Partida do jogador começou e ainda não terminou (fechar o jogo no meio = abandono).</summary>
            public bool playing;
            /// <summary>Id do asset do torneio (<see cref="TournamentConfig"/>).</summary>
            public string configId;
            public int level;
            /// <summary>Opções escolhidas no começo (goleiro, prorrogação e pênaltis o torneio liga sempre).</summary>
            public int durationMinutes = 5;
            public int touchesPerTurn = 3;
            public bool goalAfterWallIsValid;
            public bool fouls = true;
            public int discsPerTeam = 3;
            public int aimAssist;
            /// <summary>Times nas posições da chave, de cima para baixo: índice no catálogo; -1 = o jogador.</summary>
            public List<int> slots = new();
            public List<TournamentMatch> matches = new();
            /// <summary>Colocação final do jogador: 1, 2, 3, 4...; 0 = ainda jogando ou abandonou.</summary>
            public int position;
            /// <summary>Fase em que o jogador saiu (0 = primeira); -1 = chegou ao fim.</summary>
            public int eliminatedRound = -1;
            public bool abandoned;
            public int matchCoins;
            public int prize;
        }

        /// <summary>Um jogo da chave. O mandante é o de cima (<see cref="home"/>).</summary>
        [Serializable]
        public sealed class TournamentMatch
        {
            /// <summary>Fase (0 = primeira) e posição na fase (de cima para baixo).</summary>
            public int round;
            public int order;
            /// <summary>Disputa de 3º lugar (na fase da final, junto com ela).</summary>
            public bool third;
            /// <summary>Times: índice no catálogo; -1 = o jogador.</summary>
            public int home;
            public int away;
            public bool played;
            public int homeGoals;
            public int awayGoals;
            /// <summary>Gols na disputa de pênaltis; -1 = não teve.</summary>
            public int homePenalties = -1;
            public int awayPenalties = -1;
            public bool walkover;

            public bool HasPenalties => homePenalties >= 0 && awayPenalties >= 0;
            public bool HomeWon => homeGoals != awayGoals ? homeGoals > awayGoals : homePenalties > awayPenalties;
            public int Winner => HomeWon ? home : away;
            public int Loser => HomeWon ? away : home;
            public bool Has(int team) => home == team || away == team;
        }
    }
}
