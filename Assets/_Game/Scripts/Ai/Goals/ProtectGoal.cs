using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Proteger (último toque, bola longe do próprio gol): deixar a bola onde o adversário não tenha chute livre.
    /// A conta: sem chute livre para ele, quanto mais longe do próprio gol melhor.
    /// </summary>
    public sealed class ProtectGoal : AiGoal
    {
        /// <summary>Até onde o adversário pode anunciar o Vai chutar (o mesmo do Difícil).</summary>
        private const float OpponentReach = 6f;
        /// <summary>Desconto de deixar chute livre para ele (maior que qualquer distância do gol).</summary>
        private const float OpenShotPenalty = 30f;

        private static readonly AiAction[] ActionList = { AiAction.KickBallForward };

        public override string Name => "Proteger";
        public override IReadOnlyList<AiAction> Actions => ActionList;

        public override float? Score(in SimResult result, in ShotCandidate candidate, AiAction action, AiSituation situation,
            in AiScoreContext context, Vector2? from = null)
        {
            if (GoalOutcome(result, situation, context) is float goal) return goal;
            if (result.End == null) return null;
            float score = Vector2.Distance(result.BallEnd, OwnGoal(situation)) * 2f;
            if (context.OpponentPlanner != null &&
                context.OpponentPlanner.HasShotSetup(result.End, situation.OpponentDiscs, OpponentReach))
                score -= OpenShotPenalty;
            return score;
        }

        // Deixou chute livre para ele em todas: melhor fechar o gol com um botão.
        public override bool Accepts(in AiVerdict verdict, AiSituation situation, AiDifficulty difficulty) =>
            verdict.SafeFor(difficulty) && verdict.Score > 0f;
    }
}
