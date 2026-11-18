using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Torneio e liga contra a IA, guardados no <see cref="GameSave"/> (<see cref="SaveData.TournamentRun"/>): o torneio
    /// em <see cref="SaveData.tournament"/> e a liga em <see cref="SaveData.league"/>, separados (dá para ter os dois em
    /// andamento). <see cref="LeagueMode"/> diz com qual dos dois o Menu, a partida e o Resultado estão mexendo.
    /// Formatos (<see cref="TournamentConfig.format"/>):
    /// <list type="bullet">
    /// <item>Mata-mata: o jogador + times sorteados em posições sorteadas na chave; vencedor do jogo 1 x vencedor do
    /// jogo 2...; os perdedores das semifinais disputam o 3º lugar junto com a final.</item>
    /// <item>Copa: grupos sorteados, todos contra todos no grupo (vitória 3, empate 1); os primeiros vão para o mata-mata,
    /// cruzados como na Copa do Mundo (1º A x 2º B, 1º C x 2º D... e 1º B x 2º A, 1º D x 2º C...).</item>
    /// <item>Liga: pontos corridos, todos contra todos; campeão quem fizer mais pontos.</item>
    /// </list>
    /// No jogo, o mandante (mesa de casa) é o de cima. Os jogos dos outros times são sorteados quando o jogador joga o
    /// dele (na rodada ou na fase). Empate no mata-mata: prorrogação e pênaltis; nos grupos e na liga, empate vale.
    /// Sair no meio de uma partida: no torneio é derrota e fim do torneio; na liga é derrota por W.O. só daquele jogo.
    /// Desempate da tabela: pontos, saldo, gols pró, confronto direto e sorteio.
    /// </summary>
    public static partial class Tournament
    {
        /// <summary>Time do jogador nas posições da chave.</summary>
        public const int Player = -1;
        /// <summary>Vaga vazia na tabela de jogos (número ímpar de times: quem pega a folga não joga na rodada).</summary>
        private const int Bye = int.MinValue;
        /// <summary>Chance de gol de cada cobrança nos pênaltis sorteados.</summary>
        private const float SimulatedPenaltyChance = 0.75f;

        /// <summary>Mexendo na liga (verdadeiro) ou no torneio (falso). O Menu troca ao entrar em Liga ou Torneio.</summary>
        public static bool LeagueMode { get; set; }

        // Com o "Enter Play Mode" sem recarregar o domínio, o estático sobrevive entre execuções no editor.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => LeagueMode = false;

        public static SaveData.TournamentRun Run => LeagueMode
            ? (GameSave.Data.league ??= new SaveData.TournamentRun())
            : (GameSave.Data.tournament ??= new SaveData.TournamentRun());

        public static TournamentConfig Config => TournamentConfig.Find(Run.configId);

        /// <summary>Há um torneio (ou liga) em andamento (dá para continuar).</summary>
        public static bool InProgress => Run.exists && Run.active;

        /// <summary>Número de fases do mata-mata do guardado (liga: 0).</summary>
        public static int Rounds => Config.Rounds;

        /// <summary>Turnos do guardado (gravados no começo; guardado antigo: o do asset).</summary>
        public static int Legs => Config.LegsFor(Run.legs);

        /// <summary>Nome para as telas; a liga diz os turnos ("Liga 10 · 2 turnos").</summary>
        public static string DisplayName
        {
            get
            {
                var config = Config;
                string name = Txt.Get(config.displayName);
                return config.IsLeague ? $"{name} · {Txt.Get(Legs >= 2 ? "2 turnos" : "1 turno")}" : name;
            }
        }

        /// <summary>O guardado tem fase de grupos (copa) ou tabela (liga).</summary>
        public static bool HasGroups => Config.HasGroups;

        /// <summary>"Torneio" ou "Liga", para os textos.</summary>
        public static string KindName => Txt.Get(LeagueMode ? "Liga" : "Torneio");

        // ---- Começo ----

        /// <summary>Torneio (ou liga) novo com as opções escolhidas e o nível da IA; substitui o guardado do mesmo tipo.</summary>
        public static void StartNew(TournamentConfig config, MatchOptions options, AiLevel level)
        {
            // O guardado que estava em andamento conta como abandonado.
            if (InProgress)
            {
                Run.abandoned = true;
                Finish(0);
            }

            var run = new SaveData.TournamentRun
            {
                exists = true,
                active = true,
                configId = config.id,
                level = (int)level,
                durationMinutes = options.durationMinutes,
                touchesPerTurn = options.touchesPerTurn,
                goalAfterWallIsValid = options.goalAfterWallIsValid,
                fouls = options.fouls,
                discsPerTeam = options.discsPerTeam,
                aimAssist = (int)options.aimAssist,
                seed = Random.Range(1, int.MaxValue),
                legs = config.LegsFor(config.IsLeague ? MatchSession.LeagueLegs : 0),
            };

            // Times do catálogo sorteados, sem repetir enquanto der.
            var catalog = TeamCatalog.Load();
            var pool = new List<int>();
            for (int i = 0; i < catalog.TeamCount; i++) pool.Add(i);
            Shuffle(pool);
            run.slots.Add(Player);
            for (int i = 0; run.slots.Count < config.TeamCount; i++)
            {
                if (pool.Count == 0) break;
                run.slots.Add(pool[i % pool.Count]);
            }
            Shuffle(run.slots);

            if (config.HasGroups) BuildGroupMatches(run, config);
            else
                for (int i = 0; i + 1 < run.slots.Count; i += 2)
                    run.matches.Add(new SaveData.TournamentMatch { round = 0, order = i / 2, home = run.slots[i], away = run.slots[i + 1] });

            if (LeagueMode) GameSave.Data.league = run;
            else GameSave.Data.tournament = run;
            GameSave.Save(LeagueMode ? "liga nova" : "torneio novo");
        }

        // ---- Consultas ----

        /// <summary>O próximo jogo do jogador (nulo: terminou para ele).</summary>
        public static SaveData.TournamentMatch PlayerMatch()
        {
            if (!InProgress) return null;
            foreach (var match in Run.matches)
                if (!match.played && match.Has(Player)) return match;
            return null;
        }

        /// <summary>Onde é o jogo: "Rodada 3" (liga), "Grupo B, 2ª rodada" (copa), "Semifinal", "Disputa de 3º lugar".</summary>
        public static string MatchName(SaveData.TournamentMatch match)
        {
            if (match.groupStage)
                return Config.IsLeague ? Txt.Format("Rodada {0}", match.round + 1)
                    : Txt.Format("{0}, {1}ª rodada", GroupName(match.group), match.round + 1);
            return match.third ? Txt.Get("Disputa de 3º lugar") : RoundName(match.round, Rounds);
        }

        public static TeamData TeamOf(int team) => team == Player ? PlayerProfile.Team : TeamCatalog.Load().Team(team);

        public static string TeamName(int team) => TeamOf(team).TeamName;

        /// <summary>Como o jogador terminou: Campeão, Vice, 3º lugar, Eliminado nas quartas de final, Abandonou...</summary>
        public static string PositionText(SaveData.TournamentRun run)
        {
            if (run.abandoned) return Txt.Get(LeagueMode ? "Abandonou a liga" : "Abandonou o torneio");
            if (run.position == 1) return Txt.Get("Campeão!");
            if (run.position == 2) return Txt.Get("Vice-campeão");
            if (run.position == 3) return Txt.Get("3º lugar");
            if (run.position > 3) return Txt.Format("{0}º lugar", run.position);
            if (run.eliminatedInGroups) return Txt.Get("Eliminado na fase de grupos");
            if (run.eliminatedRound >= 0)
                return Txt.Get((Rounds - 1 - run.eliminatedRound) switch
                {
                    0 => "Eliminado na final",
                    1 => "Eliminado na semifinal",
                    2 => "Eliminado nas quartas de final",
                    3 => "Eliminado nas oitavas de final",
                    _ => "Eliminado na primeira fase"
                });
            return Txt.Get("Em andamento");
        }

        // ---- Partida do jogador ----

        /// <summary>
        /// Prepara a sessão para o próximo jogo do jogador: contra a IA no nível do torneio, opções do torneio (com goleiro;
        /// prorrogação e pênaltis só no mata-mata), o Meu time embaixo, o adversário em cima (esquema e camisas sorteados)
        /// e a mesa do mandante. Devolve falso se o jogador não tem jogo.
        /// </summary>
        public static bool PrepareMatch(MatchOptions defaults)
        {
            var match = PlayerMatch();
            if (match == null) return false;
            var run = Run;
            var config = Config;

            MatchSession.VsAi = true;
            MatchSession.AiLevel = (AiLevel)Mathf.Clamp(run.level, 0, AiDifficulty.LevelCount - 1);
            var options = defaults != null ? Object.Instantiate(defaults) : MatchOptions.CreateDefault();
            options.name = LeagueMode ? "Opções da liga" : "Opções do torneio";
            options.hideFlags = HideFlags.DontSave;
            options.durationMinutes = run.durationMinutes;
            options.touchesPerTurn = Mathf.Clamp(run.touchesPerTurn, 1, 3);
            options.goalAfterWallIsValid = run.goalAfterWallIsValid;
            options.fouls = run.fouls;
            options.discsPerTeam = run.discsPerTeam == 5 ? 5 : 3;
            options.aimAssist = (AimAssistMode)Mathf.Clamp(run.aimAssist, 0, 2);
            options.hasGoalkeeper = true;
            // Grupos e liga: empate vale (sem prorrogação nem pênaltis).
            options.extraTimePercent = match.groupStage ? 0 : config.extraTimePercent;
            options.penalties = !match.groupStage;
            MatchSession.UseTournamentOptions(options);

            var catalog = TeamCatalog.Load();
            int opponent = match.home == Player ? match.away : match.home;
            MatchSession.UseProfile(TeamSide.Bottom);
            MatchSession.SetTeamIndex(TeamSide.Top, opponent);
            MatchSession.SetSchemeIndex(TeamSide.Top, options.discsPerTeam, Random.Range(0, catalog.SchemeCount(options.discsPerTeam)));
            MatchSession.UseTeamShirts(TeamSide.Top);

            MatchSession.PrepareVenues();
            MatchSession.SetAway(match.home != Player);
            return true;
        }

        /// <summary>A partida do torneio começou (fechar o jogo no meio dela conta como abandono, ou W.O. na liga).</summary>
        public static void MarkPlaying()
        {
            if (!InProgress) return;
            Run.playing = true;
        }

        /// <summary>
        /// Fim da partida do jogador (gols dele e da IA, pênaltis -1 sem disputa): grava o jogo, sorteia os outros,
        /// monta a próxima fase e, se o jogador terminou, a colocação e o prêmio. Grava o save.
        /// </summary>
        public static void RecordPlayerMatch(int playerGoals, int aiGoals, int playerPenalties, int aiPenalties, bool walkover, int coins)
        {
            var match = PlayerMatch();
            if (match == null) return;
            var run = Run;
            run.playing = false;
            run.matchCoins += coins;

            if (match.groupStage) playerPenalties = aiPenalties = -1;
            // Empate sem disputa no mata-mata (não devia acontecer: o torneio liga prorrogação e pênaltis): pênaltis sorteados.
            else if (playerGoals == aiGoals && (playerPenalties < 0 || aiPenalties < 0 || playerPenalties == aiPenalties))
                SimulateShootout(out playerPenalties, out aiPenalties);
            else if (playerGoals != aiGoals) playerPenalties = aiPenalties = -1;

            bool home = match.home == Player;
            match.played = true;
            match.walkover = walkover;
            match.homeGoals = home ? playerGoals : aiGoals;
            match.awayGoals = home ? aiGoals : playerGoals;
            match.homePenalties = home ? playerPenalties : aiPenalties;
            match.awayPenalties = home ? aiPenalties : playerPenalties;

            Advance();
            GameSave.Save(LeagueMode ? "jogo da liga" : "jogo do torneio");
        }

        /// <summary>
        /// Saiu da partida no meio (menu, ou fechou o jogo): derrota por 3 x 0. No torneio, é o fim dele; na liga, só
        /// aquele jogo (a liga continua).
        /// </summary>
        public static void Forfeit()
        {
            if (!InProgress || !Run.playing) return;
            var match = PlayerMatch();
            Run.playing = false;
            bool league = Config.IsLeague;
            if (!league) Run.abandoned = true;
            if (match != null)
            {
                bool home = match.home == Player;
                match.played = true;
                match.walkover = true;
                match.homeGoals = home ? 0 : 3;
                match.awayGoals = home ? 3 : 0;
                match.homePenalties = match.awayPenalties = -1;
                if (!league && !match.groupStage) Run.eliminatedRound = match.round;
                if (!league && match.groupStage) Run.eliminatedInGroups = true;
            }
            if (!league) Finish(0);
            Advance();
            GameSave.Save(league ? "W.O. na liga" : "torneio abandonado");
        }

        /// <summary>Abriu o Menu com uma partida do torneio ou da liga marcada em andamento (o jogo fechou no meio).</summary>
        public static void ResolveLeftover()
        {
            if (MatchSession.InTournamentMatch) return;
            bool mode = LeagueMode;
            foreach (bool league in new[] { false, true })
            {
                LeagueMode = league;
                if (InProgress && Run.playing) Forfeit();
            }
            LeagueMode = mode;
        }

        // ---- Andamento ----

        /// <summary>Depois do jogo do jogador: sorteia os outros, fecha os grupos, monta o mata-mata e vê se ele terminou.</summary>
        private static void Advance()
        {
            if (HasGroups && !AdvanceGroups()) return;
            AdvanceKnockout();
        }
    }
}
