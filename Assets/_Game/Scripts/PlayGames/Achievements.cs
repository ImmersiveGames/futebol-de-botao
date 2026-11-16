using System.Collections.Generic;

namespace FutebolDeBotao
{
    /// <summary>
    /// As 21 conquistas do Play Games (lista aprovada em 16/11; só partidas contra a IA contam, como as estatísticas).
    /// A maioria sai das estatísticas do save (por isso vale para quem já jogava antes: libera no primeiro login); as
    /// que não dá para tirar delas (goleada, muralha, virada, prorrogação, copa 16) ficam em
    /// <see cref="SaveData.achievements"/> e vão junto no save da nuvem. <see cref="Report"/> manda tudo ao Google
    /// quando há login; sem login fica guardado e sobe no próximo.
    /// </summary>
    public static class Achievements
    {
        // Já enviadas nesta sessão (não repetir a cada partida) e último valor das que contam passos.
        private static readonly HashSet<string> sent = new();
        private static readonly Dictionary<string, int> sentSteps = new();

        /// <summary>Fim de uma partida contra a IA que entrou nas estatísticas.</summary>
        public static void MatchFinished(int playerScore, int aiScore, bool walkover, bool wonShootout, bool wonInExtraTime, bool trailed)
        {
            bool won = playerScore > aiScore || wonShootout;
            if (won && !walkover)
            {
                if (playerScore - aiScore >= 3) Earn(GPGSIds.achievement_rout);
                if (aiScore == 0 && !wonShootout) Earn(GPGSIds.achievement_brick_wall);
                if (trailed) Earn(GPGSIds.achievement_comeback);
                if (wonInExtraTime) Earn(GPGSIds.achievement_extra_time_hero);
            }
            Report();
        }

        /// <summary>Torneio terminado em 1º lugar (a copa de 16 tem conquista própria).</summary>
        public static void TournamentWon(int teams)
        {
            if (teams >= 16) Earn(GPGSIds.achievement_big_cup_champion);
            Report();
        }

        /// <summary>Guarda a conquista no save (vai para a nuvem) e manda ao Google.</summary>
        private static void Earn(string id)
        {
            var list = GameSave.Data.achievements;
            if (list.Contains(id)) return;
            list.Add(id);
            GameSave.WriteOnly("conquista");
        }

        /// <summary>Manda ao Google tudo o que o save já merece (guardadas + tiradas das estatísticas). Sem login, nada.</summary>
        public static void Report()
        {
            if (!PlayGames.SignedIn) return;
            var data = GameSave.Data;
            var stats = data.stats;
            var total = stats.total;
            var levels = stats.levels;

            foreach (var id in data.achievements) Unlock(id);
            if (data.tutorial != null && data.tutorial.bonusPaid) Unlock(GPGSIds.achievement_first_flick);
            if (total.wins > 0) Unlock(GPGSIds.achievement_first_win);
            if (total.numbers != null && total.numbers.shotGoals > 0) Unlock(GPGSIds.achievement_pro_gol);
            if (total.shootoutWins > 0) Unlock(GPGSIds.achievement_nerves_of_steel);
            if (Wins(levels, AiLevel.Easy) > 0) Unlock(GPGSIds.achievement_too_easy);
            if (Wins(levels, AiLevel.Medium) > 0) Unlock(GPGSIds.achievement_level_up);
            if (Wins(levels, AiLevel.Hard) > 0) Unlock(GPGSIds.achievement_respect);
            if (Wins(levels, AiLevel.Pro) > 0) Unlock(GPGSIds.achievement_professional);
            if (total.bestStreak >= 5) Unlock(GPGSIds.achievement_streak_of_5);
            if (total.bestStreak >= 10) Unlock(GPGSIds.achievement_streak_of_10);
            if (total.tournamentTitles > 0) Unlock(GPGSIds.achievement_champion);
            if ((int)AiLevel.Pro < levels.Length && levels[(int)AiLevel.Pro].tournamentTitles > 0) Unlock(GPGSIds.achievement_pro_champion);
            var profile = data.profile;
            if (profile != null && (profile.teamName != PlayerProfile.DefaultName || profile.primary != PlayerProfile.DefaultPrimary ||
                                    profile.secondary != PlayerProfile.DefaultSecondary))
                Unlock(GPGSIds.achievement_my_team);

            Steps(GPGSIds.achievement_top_scorer, total.numbers != null ? total.numbers.goals : 0, 100);
            Steps(GPGSIds.achievement_veteran, total.Played, 50);
            Steps(GPGSIds.achievement_collector, stats.itemsBought, 10);
        }

        private static int Wins(SaveData.LevelStats[] levels, AiLevel level) =>
            (int)level < levels.Length && levels[(int)level] != null ? levels[(int)level].wins : 0;

        private static void Unlock(string id)
        {
            if (!sent.Add(id)) return;
            PlayGames.Unlock(id);
        }

        private static void Steps(string id, int value, int max)
        {
            value = System.Math.Min(value, max);
            if (value <= 0 || (sentSteps.TryGetValue(id, out int last) && last >= value)) return;
            sentSteps[id] = value;
            PlayGames.SetSteps(id, value);
        }

        /// <summary>Novo login (outra conta): manda tudo de novo.</summary>
        internal static void ForgetSent()
        {
            sent.Clear();
            sentSteps.Clear();
        }
    }
}
