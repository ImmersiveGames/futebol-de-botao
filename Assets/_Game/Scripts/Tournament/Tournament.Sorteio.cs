using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>Jogos dos outros times sorteados (placar e pênaltis) e embaralhar. Parte do <see cref="Tournament"/>.</summary>
    public static partial class Tournament
    {
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
