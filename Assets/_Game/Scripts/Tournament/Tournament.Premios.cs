using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>Fim do torneio ou da liga: colocação, prêmio, estatísticas, conquistas, ranking semanal e prêmio em dobro. Parte do <see cref="Tournament"/>.</summary>
    public static partial class Tournament
    {
        /// <summary>Pode dobrar o prêmio do torneio terminado com um anúncio (uma vez).</summary>
        public static bool CanDoublePrize => Run.exists && !Run.active && !Run.abandoned && Run.prize > 0 && !Run.prizeDoubled;

        /// <summary>Músicas do rádio que o título que acabou de sair liberou (1º torneio ou 1ª liga ganhos); vazio = nenhuma.</summary>
        public static List<RadioTrack> NewRadioSongs { get; private set; } = new();

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
            bool firstTitle = !run.abandoned && run.position == 1 &&
                              (league ? stats.total.leagueTitles == 0 : stats.total.tournamentTitles == 0);
            NewRadioSongs = firstTitle ? Radio.UnlockedBy(league ? RadioUnlock.LeagueTitle : RadioUnlock.TournamentTitle) : new List<RadioTrack>();
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
            if (title) Achievements.TournamentWon(config);
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
    }
}
