using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Torneio mata-mata contra a IA, guardado no <see cref="GameSave"/> (<see cref="SaveData.TournamentRun"/>).
    /// O jogador + times sorteados do catálogo em posições sorteadas na chave; depois a chave segue o padrão (vencedor do
    /// jogo 1 x vencedor do jogo 2...), e os perdedores das semifinais disputam o 3º lugar junto com a final. No jogo, o
    /// mandante (mesa de casa) é o de cima. Os jogos dos outros times são sorteados quando o jogador termina o dele.
    /// Empate nos jogos do jogador: prorrogação e pênaltis sempre. Sair no meio de uma partida = derrota e fim do torneio.
    /// </summary>
    public static class Tournament
    {
        /// <summary>Time do jogador nas posições da chave.</summary>
        public const int Player = -1;
        /// <summary>Chance de gol de cada cobrança nos pênaltis sorteados.</summary>
        private const float SimulatedPenaltyChance = 0.75f;

        public static SaveData.TournamentRun Run => GameSave.Data.tournament ??= new SaveData.TournamentRun();
        public static TournamentConfig Config => TournamentConfig.Find(Run.configId);

        /// <summary>Há um torneio em andamento (dá para continuar).</summary>
        public static bool InProgress => Run.exists && Run.active;

        /// <summary>Número de fases do torneio guardado.</summary>
        public static int Rounds => Mathf.Max(1, Mathf.RoundToInt(Mathf.Log(Mathf.Max(2, Run.slots.Count), 2f)));

        // ---- Começo ----

        /// <summary>Torneio novo com as opções escolhidas e o nível da IA; substitui o guardado.</summary>
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

            for (int i = 0; i + 1 < run.slots.Count; i += 2)
                run.matches.Add(new SaveData.TournamentMatch { round = 0, order = i / 2, home = run.slots[i], away = run.slots[i + 1] });

            GameSave.Data.tournament = run;
            GameSave.Save("torneio novo");
        }

        // ---- Consultas ----

        /// <summary>O próximo jogo do jogador (nulo: torneio terminado para ele).</summary>
        public static SaveData.TournamentMatch PlayerMatch()
        {
            if (!InProgress) return null;
            foreach (var match in Run.matches)
                if (!match.played && match.Has(Player)) return match;
            return null;
        }

        /// <summary>Fase atual: a primeira com jogo por jogar (ou a última).</summary>
        public static int CurrentRound()
        {
            int round = Rounds - 1;
            foreach (var match in Run.matches)
                if (!match.played && match.round < round) round = match.round;
            return round;
        }

        /// <summary>Jogos de uma fase, de cima para baixo (na final: a final e depois o 3º lugar).</summary>
        public static List<SaveData.TournamentMatch> MatchesOf(int round)
        {
            var list = new List<SaveData.TournamentMatch>();
            foreach (var match in Run.matches)
                if (match.round == round) list.Add(match);
            list.Sort((a, b) => a.third != b.third ? a.third.CompareTo(b.third) : a.order.CompareTo(b.order));
            return list;
        }

        /// <summary>Nome da fase pela distância até a final: Final, Semifinal, Quartas de final, Oitavas de final...</summary>
        public static string RoundName(int round, int rounds)
        {
            return (rounds - 1 - round) switch
            {
                0 => "Final",
                1 => "Semifinal",
                2 => "Quartas de final",
                3 => "Oitavas de final",
                _ => "Primeira fase"
            };
        }

        /// <summary>Nome curto para a aba.</summary>
        public static string TabName(int round, int rounds)
        {
            return (rounds - 1 - round) switch
            {
                0 => "Final",
                1 => "Semi",
                2 => "Quartas",
                3 => "Oitavas",
                _ => "1ª fase"
            };
        }

        public static TeamData TeamOf(int team) => team == Player ? PlayerProfile.Team : TeamCatalog.Load().Team(team);

        public static string TeamName(int team) => TeamOf(team).TeamName;

        /// <summary>Como o jogador terminou: Campeão, Vice, 3º lugar, Eliminado nas quartas de final, Abandonou...</summary>
        public static string PositionText(SaveData.TournamentRun run)
        {
            if (run.abandoned) return "Abandonou o torneio";
            if (run.position == 1) return "Campeão!";
            if (run.position == 2) return "Vice-campeão";
            if (run.position > 2) return $"{run.position}º lugar";
            if (run.eliminatedRound >= 0)
            {
                string round = RoundName(run.eliminatedRound, Rounds).ToLowerInvariant();
                return (round.StartsWith("primeira") ? "Eliminado na " : "Eliminado nas ") + round;
            }
            return "Em andamento";
        }

        // ---- Partida do jogador ----

        /// <summary>
        /// Prepara a sessão para o próximo jogo do jogador: contra a IA no nível do torneio, opções do torneio (com goleiro,
        /// prorrogação e pênaltis), o Meu time embaixo, o adversário em cima (esquema e camisas sorteados) e a mesa do
        /// mandante. Devolve falso se o jogador não tem jogo.
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
            options.name = "Opções do torneio";
            options.hideFlags = HideFlags.DontSave;
            options.durationMinutes = run.durationMinutes;
            options.touchesPerTurn = Mathf.Clamp(run.touchesPerTurn, 1, 3);
            options.goalAfterWallIsValid = run.goalAfterWallIsValid;
            options.fouls = run.fouls;
            options.discsPerTeam = run.discsPerTeam == 5 ? 5 : 3;
            options.aimAssist = (AimAssistMode)Mathf.Clamp(run.aimAssist, 0, 2);
            options.hasGoalkeeper = true;
            options.extraTimePercent = config.extraTimePercent;
            options.penalties = true;
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

        /// <summary>A partida do torneio começou (fechar o jogo no meio dela conta como abandono).</summary>
        public static void MarkPlaying()
        {
            if (!InProgress) return;
            Run.playing = true;
        }

        /// <summary>
        /// Fim da partida do jogador (gols dele e da IA, pênaltis -1 sem disputa): grava o jogo, sorteia os outros da fase,
        /// monta a próxima e, se o jogador terminou, a colocação e o prêmio. Grava o save.
        /// </summary>
        public static void RecordPlayerMatch(int playerGoals, int aiGoals, int playerPenalties, int aiPenalties, bool walkover, int coins)
        {
            var match = PlayerMatch();
            if (match == null) return;
            var run = Run;
            run.playing = false;
            run.matchCoins += coins;

            // Empate sem disputa (não devia acontecer: o torneio liga prorrogação e pênaltis): pênaltis sorteados.
            if (playerGoals == aiGoals && (playerPenalties < 0 || aiPenalties < 0 || playerPenalties == aiPenalties))
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
            GameSave.Save("jogo do torneio");
        }

        /// <summary>Saiu da partida no meio (menu, ou fechou o jogo): derrota por 3 x 0 e fim do torneio.</summary>
        public static void Forfeit()
        {
            if (!InProgress || !Run.playing) return;
            var match = PlayerMatch();
            Run.playing = false;
            Run.abandoned = true;
            if (match != null)
            {
                bool home = match.home == Player;
                match.played = true;
                match.walkover = true;
                match.homeGoals = home ? 0 : 3;
                match.awayGoals = home ? 3 : 0;
                Run.eliminatedRound = match.round;
            }
            Finish(0);
            Advance();
            GameSave.Save("torneio abandonado");
        }

        /// <summary>Abriu o Menu com uma partida do torneio marcada em andamento (o jogo fechou no meio): abandono.</summary>
        public static void ResolveLeftover()
        {
            if (InProgress && Run.playing && !MatchSession.InTournamentMatch) Forfeit();
        }

        // ---- Chave ----

        /// <summary>Depois do jogo do jogador: sorteia os outros, monta as próximas fases e vê se o jogador terminou.</summary>
        private static void Advance()
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
                if (match.played && match.Has(Player) && (last == null || match.round > last.round || match.third)) last = match;
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
            run.active = false;
            run.playing = false;
            run.position = run.abandoned ? 0 : position;
            run.prize = run.abandoned ? 0 : config.PrizeFor(position);

            var stats = GameSave.Data.stats;
            int level = Mathf.Clamp(run.level, 0, AiDifficulty.LevelCount - 1);
            foreach (var s in new[] { stats.total, stats.levels[level] })
            {
                s.tournaments++;
                if (run.abandoned) s.tournamentsAbandoned++;
                else if (run.position == 1) s.tournamentTitles++;
                else if (run.position == 2) s.tournamentRunnerUps++;
                else if (run.position == 3) s.tournamentThirds++;
                s.coins += run.prize;
            }
            if (run.prize > 0)
            {
                string place = run.position == 1 ? "campeão" : $"{run.position}º lugar";
                Finance.Earn(FinanceSource.Tournament, $"Prêmio do torneio ({place})", run.prize);
            }
        }

        // ---- Sorteio ----

        private static void Simulate(SaveData.TournamentMatch match)
        {
            int max = Mathf.Max(0, Config.maxSimulatedGoals);
            match.homeGoals = Random.Range(0, max + 1);
            match.awayGoals = Random.Range(0, max + 1);
            if (match.homeGoals == match.awayGoals) SimulateShootout(out match.homePenalties, out match.awayPenalties);
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
