using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>Avançar com a bola: levar a bola para a frente ficando com o último toque. O objetivo de sempre.</summary>
    public sealed class AdvanceGoal : AiGoal
    {
        private static readonly AiAction[] ActionList = { AiAction.KickBallForward };

        public override string Name => "Avançar";
        public override IReadOnlyList<AiAction> Actions => ActionList;

        public override float Priority(AiSituation situation) => AiAction.KickBallForward.CanRun(situation) ? 40f : 0f;

        public override float? Score(in SimResult result, in ShotCandidate candidate, AiAction action, AiSituation situation,
            in AiScoreContext context) => BaseScore(result, situation, context);

        /// <summary>
        /// Falta certa nunca. Jogada com risco ou ruim: às vezes a IA arrisca mesmo assim, às vezes desiste e vai para o
        /// próximo objetivo (fechar o gol ou só posicionar). As chances vêm do nível.
        /// </summary>
        public override bool Accepts(in AiVerdict verdict, AiSituation situation, AiDifficulty difficulty)
        {
            if (verdict.Foul) return false;
            if (verdict.OwnGoalRisk) return Random.value >= difficulty.ownGoalDefendChance;
            bool poor = verdict.FoulChance > difficulty.maxFoulChance || verdict.Score < difficulty.poorShotScore;
            // Com toques sobrando, posicionar entrega a vez: só vale desistir se a jogada também perderia a bola.
            if (poor && !situation.IsLastTouch && verdict.FoulChance <= difficulty.maxFoulChance && verdict.LoseChance < 0.5f)
                return true;
            return !poor || Random.value >= difficulty.repositionChance;
        }
    }
}
