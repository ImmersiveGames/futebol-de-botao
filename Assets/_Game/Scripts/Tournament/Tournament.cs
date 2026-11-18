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
    public static class Tournament
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

        /// <summary>
        /// Jogos dos grupos (a liga é um grupo só): os times de <see cref="SaveData.TournamentRun.slots"/> em sequência
        /// formam os grupos; todos contra todos (método do círculo), no 2º turno com o mando invertido. Ficam na ordem das
        /// rodadas, para o próximo jogo do jogador ser sempre o primeiro que falta.
        /// </summary>
        private static void BuildGroupMatches(SaveData.TournamentRun run, TournamentConfig config)
        {
            int groups = config.GroupCount;
            int size = config.IsLeague ? run.slots.Count : config.GroupSize;
            var all = new List<SaveData.TournamentMatch>();
            for (int g = 0; g < groups; g++)
            {
                var teams = new List<int>();
                for (int i = g * size; i < (g + 1) * size && i < run.slots.Count; i++) teams.Add(run.slots[i]);
                all.AddRange(RoundRobin(teams, config.LegsFor(run.legs), g));
            }
            all.Sort((a, b) => a.round != b.round ? a.round.CompareTo(b.round)
                : a.group != b.group ? a.group.CompareTo(b.group) : a.order.CompareTo(b.order));
            run.matches.AddRange(all);
        }

        private static List<SaveData.TournamentMatch> RoundRobin(List<int> teams, int legs, int group)
        {
            var list = new List<SaveData.TournamentMatch>();
            var circle = new List<int>(teams);
            if (circle.Count % 2 == 1) circle.Add(Bye);
            int n = circle.Count;
            int roundsPerLeg = n - 1;
            for (int r = 0; r < roundsPerLeg; r++)
            {
                int order = 0;
                for (int i = 0; i < n / 2; i++)
                {
                    int a = circle[i], b = circle[n - 1 - i];
                    if (a == Bye || b == Bye) continue;
                    // Mando alternado: o fixo do círculo troca a cada rodada, os outros pela posição.
                    bool aHome = i == 0 ? r % 2 == 0 : (r + i) % 2 == 0;
                    int home = aHome ? a : b, away = aHome ? b : a;
                    for (int leg = 0; leg < legs; leg++)
                        list.Add(new SaveData.TournamentMatch
                        {
                            groupStage = true, group = group, round = r + leg * roundsPerLeg, order = order,
                            home = leg == 0 ? home : away, away = leg == 0 ? away : home
                        });
                    order++;
                }
                // Gira: o primeiro fica, o último passa a ser o segundo.
                int last = circle[n - 1];
                circle.RemoveAt(n - 1);
                circle.Insert(1, last);
            }
            return list;
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

        /// <summary>Fase atual do mata-mata: a primeira com jogo por jogar (ou a última).</summary>
        public static int CurrentRound()
        {
            int round = Mathf.Max(0, Rounds - 1);
            foreach (var match in Run.matches)
                if (!match.groupStage && !match.played && match.round < round) round = match.round;
            return round;
        }

        /// <summary>Rodada atual dos grupos ou da liga: a primeira com jogo por jogar (ou a última).</summary>
        public static int CurrentMatchday()
        {
            int last = 0;
            foreach (var match in Run.matches)
            {
                if (!match.groupStage) continue;
                if (!match.played) return match.round;
                last = Mathf.Max(last, match.round);
            }
            return last;
        }

        /// <summary>Rodadas dos grupos ou da liga.</summary>
        public static int Matchdays()
        {
            int count = 0;
            foreach (var match in Run.matches)
                if (match.groupStage) count = Mathf.Max(count, match.round + 1);
            return count;
        }

        /// <summary>Jogos de uma fase do mata-mata, de cima para baixo (na final: a final e depois o 3º lugar).</summary>
        public static List<SaveData.TournamentMatch> MatchesOf(int round)
        {
            var list = new List<SaveData.TournamentMatch>();
            foreach (var match in Run.matches)
                if (!match.groupStage && match.round == round) list.Add(match);
            list.Sort((a, b) => a.third != b.third ? a.third.CompareTo(b.third) : a.order.CompareTo(b.order));
            return list;
        }

        /// <summary>Jogos de um grupo (-1 = todos os grupos) numa rodada (-1 = todas), na ordem da tabela de jogos.</summary>
        public static List<SaveData.TournamentMatch> GroupMatches(int group, int matchday)
        {
            var list = new List<SaveData.TournamentMatch>();
            foreach (var match in Run.matches)
                if (match.groupStage && (group < 0 || match.group == group) && (matchday < 0 || match.round == matchday)) list.Add(match);
            return list;
        }

        /// <summary>Grupos guardados (liga = 1).</summary>
        public static int GroupCount()
        {
            int count = 0;
            foreach (var match in Run.matches)
                if (match.groupStage) count = Mathf.Max(count, match.group + 1);
            return count;
        }

        /// <summary>Grupo do jogador (-1: sem grupos).</summary>
        public static int PlayerGroup()
        {
            foreach (var match in Run.matches)
                if (match.groupStage && match.Has(Player)) return match.group;
            return -1;
        }

        public static string GroupName(int group) => Txt.Format("Grupo {0}", (char)('A' + Mathf.Clamp(group, 0, 25)));

        /// <summary>Uma linha da tabela.</summary>
        public sealed class Standing
        {
            public int team;
            public int played, won, drawn, lost, goalsFor, goalsAgainst;
            public int Points => won * 3 + drawn;
            public int GoalDifference => goalsFor - goalsAgainst;
        }

        /// <summary>Classificação de um grupo (na liga, o 0): pontos, saldo, gols pró, confronto direto e sorteio.</summary>
        public static List<Standing> Table(int group)
        {
            var rows = new Dictionary<int, Standing>();
            var matches = GroupMatches(group, -1);
            foreach (var match in matches)
            {
                foreach (int team in new[] { match.home, match.away })
                    if (!rows.ContainsKey(team)) rows[team] = new Standing { team = team };
                if (!match.played) continue;
                Count(rows[match.home], match.homeGoals, match.awayGoals);
                Count(rows[match.away], match.awayGoals, match.homeGoals);
            }
            var list = new List<Standing>(rows.Values);
            int seed = Run.seed;
            list.Sort((a, b) =>
            {
                if (a.Points != b.Points) return b.Points.CompareTo(a.Points);
                if (a.GoalDifference != b.GoalDifference) return b.GoalDifference.CompareTo(a.GoalDifference);
                if (a.goalsFor != b.goalsFor) return b.goalsFor.CompareTo(a.goalsFor);
                int headToHead = HeadToHead(matches, a.team, b.team);
                if (headToHead != 0) return -headToHead;
                return Draw(seed, a.team).CompareTo(Draw(seed, b.team));
            });
            return list;
        }

        private static void Count(Standing row, int goalsFor, int goalsAgainst)
        {
            row.played++;
            row.goalsFor += goalsFor;
            row.goalsAgainst += goalsAgainst;
            if (goalsFor > goalsAgainst) row.won++;
            else if (goalsFor == goalsAgainst) row.drawn++;
            else row.lost++;
        }

        /// <summary>Pontos de <paramref name="a"/> menos os de <paramref name="b"/> nos jogos entre os dois.</summary>
        private static int HeadToHead(List<SaveData.TournamentMatch> matches, int a, int b)
        {
            int points = 0;
            foreach (var match in matches)
            {
                if (!match.played || !match.Has(a) || !match.Has(b)) continue;
                int goalsA = match.home == a ? match.homeGoals : match.awayGoals;
                int goalsB = match.home == a ? match.awayGoals : match.homeGoals;
                points += goalsA > goalsB ? 3 : goalsA < goalsB ? -3 : 0;
            }
            return points;
        }

        /// <summary>Sorteio fixo do torneio (mesmo número toda vez para o mesmo time).</summary>
        private static int Draw(int seed, int team) => unchecked((team * 486187739) ^ seed) & int.MaxValue;

        /// <summary>Nome da fase pela distância até a final: Final, Semifinal, Quartas de final, Oitavas de final...</summary>
        public static string RoundName(int round, int rounds)
        {
            return Txt.Get((rounds - 1 - round) switch
            {
                0 => "Final",
                1 => "Semifinal",
                2 => "Quartas de final",
                3 => "Oitavas de final",
                _ => "Primeira fase"
            });
        }

        /// <summary>Nome curto para a aba.</summary>
        public static string TabName(int round, int rounds)
        {
            return Txt.Get((rounds - 1 - round) switch
            {
                0 => "Final",
                1 => "Semi",
                2 => "Quartas",
                3 => "Oitavas",
                _ => "1ª fase"
            });
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

        /// <summary>
        /// Grupos e liga: sorteia os jogos das rodadas que o jogador já passou (todos, se ele não joga mais). Com todos
        /// jogados, a liga termina e a copa monta o mata-mata. Devolve se a fase de grupos acabou.
        /// </summary>
        private static bool AdvanceGroups()
        {
            var run = Run;
            var config = Config;
            var next = PlayerMatch();
            int upTo = next != null && next.groupStage ? next.round : int.MaxValue;
            bool done = true;
            foreach (var match in run.matches)
            {
                if (!match.groupStage || match.played) continue;
                if (match.round < upTo && !(run.active && match.Has(Player))) Simulate(match);
                if (!match.played) done = false;
            }
            if (!done) return false;

            if (config.IsLeague)
            {
                if (run.active) Finish(TablePosition(0, Player));
                return false;
            }
            if (MatchesOf(0).Count == 0) BuildKnockoutFromGroups(config);
            return true;
        }

        /// <summary>Posição (1 = primeiro) de um time na tabela do grupo.</summary>
        private static int TablePosition(int group, int team)
        {
            var table = Table(group);
            for (int i = 0; i < table.Count; i++)
                if (table[i].team == team) return i + 1;
            return table.Count;
        }

        /// <summary>
        /// 1ª fase do mata-mata da copa: os classificados de cada par de grupos se cruzam (1º A x 2º B, 1º B x 2º A),
        /// com os jogos dos grupos A/B, C/D... de um lado da chave e os cruzados do outro. Com mais de 2 classificados por
        /// grupo, o 1º pega o último classificado do grupo vizinho, o 2º o penúltimo... O jogador fora: caiu nos grupos.
        /// </summary>
        private static void BuildKnockoutFromGroups(TournamentConfig config)
        {
            var run = Run;
            int groups = GroupCount();
            int qualifiers = Mathf.Max(1, config.Qualifiers);
            var tables = new List<List<Standing>>();
            for (int g = 0; g < groups; g++) tables.Add(Table(g));

            var firstHalf = new List<(int, int)>();
            var secondHalf = new List<(int, int)>();
            if (groups == 1)
            {
                // Um grupo só: 1º x último classificado, 2º x penúltimo...
                var table = tables[0];
                int count = Mathf.Min(qualifiers, table.Count);
                for (int i = 0; i < count / 2; i++) firstHalf.Add((table[i].team, table[count - 1 - i].team));
            }
            else
            {
                for (int g = 0; g + 1 < groups; g += 2)
                {
                    var a = tables[g];
                    var b = tables[g + 1];
                    // 1º A x último classificado de B... (metade de cima) e 1º B x último de A... (metade de baixo).
                    for (int i = 0; i < qualifiers; i++)
                    {
                        int j = qualifiers - 1 - i;
                        if (i >= a.Count || j >= b.Count) continue;
                        if (i < (qualifiers + 1) / 2) firstHalf.Add((a[i].team, b[j].team));
                        else secondHalf.Add((b[j].team, a[i].team));
                    }
                }
            }
            var pairs = new List<(int home, int away)>(firstHalf);
            pairs.AddRange(secondHalf);
            int order = 0;
            var qualified = new HashSet<int>();
            foreach (var (home, away) in pairs)
            {
                run.matches.Add(new SaveData.TournamentMatch { round = 0, order = order++, home = home, away = away });
                qualified.Add(home);
                qualified.Add(away);
            }

            if (run.active && !qualified.Contains(Player))
            {
                run.eliminatedInGroups = true;
                Finish(0);
            }
        }

        /// <summary>Mata-mata: sorteia os outros jogos da fase, monta as próximas fases e vê se o jogador terminou.</summary>
        private static void AdvanceKnockout()
        {
            var run = Run;
            int rounds = Rounds;
            CheckPlayerOut();
            for (int round = 0; round < rounds; round++)
            {
                var matches = MatchesOf(round);
                if (matches.Count == 0) break;
                // Os outros jogos da fase só saem depois do jogo do jogador nela.
                if (run.active && matches.Exists(m => !m.played && m.Has(Player))) return;
                foreach (var match in matches)
                {
                    // O jogo do jogador ainda ativo espera ele jogar.
                    if (match.played || (run.active && match.Has(Player))) continue;
                    Simulate(match);
                }
                if (!matches.TrueForAll(m => m.played)) return;
                if (round + 1 < rounds && MatchesOf(round + 1).Count == 0) BuildNextRound(round, rounds);
                CheckPlayerOut();
            }
        }

        /// <summary>Monta a fase seguinte pelos vencedores (e o 3º lugar pelos perdedores das semifinais).</summary>
        private static void BuildNextRound(int round, int rounds)
        {
            var previous = MatchesOf(round);
            int next = round + 1;
            var winners = new List<SaveData.TournamentMatch>();
            foreach (var match in previous)
                if (!match.third) winners.Add(match);
            for (int i = 0; i + 1 < winners.Count; i += 2)
                Run.matches.Add(new SaveData.TournamentMatch
                {
                    round = next, order = i / 2, home = winners[i].Winner, away = winners[i + 1].Winner
                });
            if (next == rounds - 1 && winners.Count == 2)
                Run.matches.Add(new SaveData.TournamentMatch
                {
                    round = next, order = 1, third = true, home = winners[0].Loser, away = winners[1].Loser
                });
        }

        /// <summary>O jogador perdeu fora da semifinal, ou jogou a final ou o 3º lugar: termina o torneio para ele.</summary>
        private static void CheckPlayerOut()
        {
            var run = Run;
            if (!run.active) return;
            int rounds = Rounds;
            SaveData.TournamentMatch last = null;
            foreach (var match in run.matches)
                if (!match.groupStage && match.played && match.Has(Player) && (last == null || match.round > last.round || match.third))
                    last = match;
            if (last == null) return;

            bool won = last.Winner == Player;
            int toFinal = rounds - 1 - last.round;
            int position;
            if (toFinal == 0) position = last.third ? (won ? 3 : 4) : (won ? 1 : 2);
            else if (won || toFinal == 1) return;     // ganhou, ou perdeu a semifinal (vai para o 3º lugar)
            else
            {
                position = 0;
                run.eliminatedRound = last.round;
            }
            Finish(position);
        }

        private static void Finish(int position)
        {
            var run = Run;
            var config = Config;
            bool league = config.IsLeague;
            run.active = false;
            run.playing = false;
            run.position = run.abandoned ? 0 : position;
            int legs = config.LegsFor(run.legs);
            run.prize = run.abandoned ? 0 : config.PrizeFor(position, legs);

            var stats = GameSave.Data.stats;
            int level = Mathf.Clamp(run.level, 0, AiDifficulty.LevelCount - 1);
            foreach (var s in new[] { stats.total, stats.levels[level] })
            {
                if (league)
                {
                    s.leagues++;
                    if (!run.abandoned && run.position == 1) s.leagueTitles++;
                }
                else
                {
                    s.tournaments++;
                    if (run.abandoned) s.tournamentsAbandoned++;
                    else if (run.position == 1) s.tournamentTitles++;
                    else if (run.position == 2) s.tournamentRunnerUps++;
                    else if (run.position == 3) s.tournamentThirds++;
                }
                s.coins += run.prize;
            }
            // Ranking semanal e conquistas.
            var played = new List<(int, int)>();
            foreach (var match in run.matches)
            {
                if (!match.played || !match.Has(Player)) continue;
                bool home = match.home == Player;
                played.Add(home ? (match.homeGoals, match.awayGoals) : (match.awayGoals, match.homeGoals));
            }
            int stageFromFinal = run.eliminatedRound >= 0 ? Rounds - 1 - run.eliminatedRound : -1;
            int placement = league ? WeeklyRanking.LeaguePlacement(run.position, config.TeamCount)
                : run.eliminatedInGroups ? WeeklyRanking.GroupStagePlacement
                : WeeklyRanking.KnockoutPlacement(run.position, stageFromFinal);
            bool title = !run.abandoned && run.position == 1;
            WeeklyRanking.TournamentFinished(placement, title, run.abandoned, level, config.RankingWeightFor(legs),
                WeeklyRanking.GoalDifference(played));
            if (title && !league) Achievements.TournamentWon(config.TeamCount);
            else Achievements.Report();
            if (run.prize > 0)
            {
                string text = league
                    ? run.position == 1 ? Txt.Get("Prêmio da liga (campeão)") : Txt.Format("Prêmio da liga ({0}º lugar)", run.position)
                    : run.position == 1 ? Txt.Get("Prêmio do torneio (campeão)") : Txt.Format("Prêmio do torneio ({0}º lugar)", run.position);
                Finance.Earn(FinanceSource.Tournament, text, run.prize);
            }
            GameAnalytics.Log("tournament_end", ("teams", (long)config.TeamCount),
                ("format", config.format.ToString().ToLowerInvariant()),
                ("level", ((AiLevel)level).ToString().ToLowerInvariant()),
                ("position", (long)run.position), ("abandoned", run.abandoned ? "yes" : "no"),
                // Fase em que caiu, contando da final: 0 = final, 1 = semifinal, 2 = quartas... (-1 = não foi eliminado;
                // 99 = fase de grupos).
                ("eliminated_stage", (long)(run.eliminatedInGroups ? 99 : stageFromFinal)));
        }

        /// <summary>Pode dobrar o prêmio do torneio terminado com um anúncio (uma vez).</summary>
        public static bool CanDoublePrize => Run.exists && !Run.active && !Run.abandoned && Run.prize > 0 && !Run.prizeDoubled;

        /// <summary>"Dobrar prêmio" no fim do torneio (assistiu ao anúncio): paga o prêmio de novo e grava.</summary>
        public static void DoublePrize()
        {
            if (!CanDoublePrize) return;
            var run = Run;
            run.prizeDoubled = true;
            var stats = GameSave.Data.stats;
            int level = Mathf.Clamp(run.level, 0, AiDifficulty.LevelCount - 1);
            stats.total.coins += run.prize;
            stats.levels[level].coins += run.prize;
            Finance.Earn(FinanceSource.Tournament, Txt.Get("Prêmio em dobro (anúncio)"), run.prize);
            GameSave.WriteOnly("prêmio em dobro");
        }

        // ---- Sorteio ----

        private static void Simulate(SaveData.TournamentMatch match)
        {
            var scores = Config.Scores;
            match.homeGoals = scores.Roll();
            match.awayGoals = scores.Roll();
            if (match.homeGoals == match.awayGoals && !match.groupStage) SimulateShootout(out match.homePenalties, out match.awayPenalties);
            else match.homePenalties = match.awayPenalties = -1;
            match.played = true;
        }

        /// <summary>Disputa sorteada: 5 cobranças para cada lado (para antes quando não dá mais) e morte súbita.</summary>
        private static void SimulateShootout(out int home, out int away)
        {
            home = away = 0;
            for (int kick = 0; kick < 5; kick++)
            {
                if (Random.value < SimulatedPenaltyChance) home++;
                if (home > away + (5 - kick) || away > home + (4 - kick)) return;
                if (Random.value < SimulatedPenaltyChance) away++;
                int left = 4 - kick;
                if (home > away + left || away > home + left) return;
            }
            while (home == away)
            {
                if (Random.value < SimulatedPenaltyChance) home++;
                if (Random.value < SimulatedPenaltyChance) away++;
            }
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
