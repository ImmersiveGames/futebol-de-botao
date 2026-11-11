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
        public const int CurrentVersion = 10;

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
        /// <summary>Tutorial (versão 7): lições completas, se o Menu já perguntou e se o bônus já foi pago.</summary>
        public TutorialData tutorial = new();
        /// <summary>Financeiro (versão 9): totais de moedas, ganho por origem e o extrato (aba Financeiro do Meu time).</summary>
        public FinanceData finance = new();

        [Serializable]
        public sealed class FinanceData
        {
            public int earned;
            public int spent;
            public int fromMatches;
            public int fromTournaments;
            public int fromTutorial;
            /// <summary>Últimas movimentações, da mais antiga para a mais nova.</summary>
            public List<FinanceEntry> entries = new();
        }

        [Serializable]
        public sealed class FinanceEntry
        {
            /// <summary>"yyyy-MM-dd HH:mm".</summary>
            public string date;
            public string text;
            /// <summary>Positivo = ganhou; negativo = gastou.</summary>
            public int amount;
        }

        [Serializable]
        public sealed class TutorialData
        {
            /// <summary>O Menu já perguntou "Quer aprender a jogar?" (respondido ou não).</summary>
            public bool asked;
            /// <summary>Ids das lições completas (<see cref="TutorialTexts.LessonId"/>).</summary>
            public List<string> done = new();
            /// <summary>Bônus de completar todas as lições já pago.</summary>
            public bool bonusPaid;
        }

        [Serializable]
        public sealed class SettingsData
        {
            /// <summary>Música Ligado/Desligado (até a versão 9); agora vale <see cref="musicVolume"/>.</summary>
            public bool music = true;
            /// <summary>Volumes da tela Configurações (versão 10), de 0 a <see cref="GameVolume.MaxLevel"/>.</summary>
            public int masterVolume = GameVolume.MaxLevel;
            public int musicVolume = GameVolume.MaxLevel;
            public int effectsVolume = GameVolume.MaxLevel;
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
            /// <summary>Uniforme escolhido na Seleção de times (versão 8): 0 = Uniforme 1, 1 = Uniforme 2.</summary>
            public int uniform;
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
            /// <summary>Uniforme 2 do Meu time (versão 8): cores e camisas; os campos de cima são o Uniforme 1.</summary>
            public UnityEngine.Color primary2 = PlayerProfile.DefaultSecondary;
            public UnityEngine.Color secondary2 = PlayerProfile.DefaultPrimary;
            public string discPatternId2;
            public string keeperPatternId2;
            /// <summary>Cor reserva (versão 3 a 7): convertida no Uniforme 2 ao abrir um save antigo.</summary>
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
            /// <summary>Moedas ganhas e gastas (versões 4 a 8; agora em <see cref="FinanceData"/>).</summary>
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
