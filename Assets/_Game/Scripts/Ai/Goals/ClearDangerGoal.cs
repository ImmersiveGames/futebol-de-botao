using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>Afastar (bola perto do próprio gol, antes de preparar): a conta é o quanto a bola fica mais longe do gol.</summary>
    public sealed class ClearDangerGoal : AiGoal
    {
        private static readonly AiAction[] ActionList = { AiAction.KickBallForward };

        /// <summary>A bola tem que terminar pelo menos isto mais longe do próprio gol.</summary>
        private const float MinGain = 0.3f;

        public override string Name => "Afastar";
        public override IReadOnlyList<AiAction> Actions => ActionList;

        public override float? Score(in SimResult result, in ShotCandidate candidate, AiAction action, AiSituation situation,
            in AiScoreContext context, Vector2? from = null)
        {
            if (GoalOutcome(result, situation, context) is float goal) return goal;
            Vector2 start = from ?? situation.Ball;
            float gained = Vector2.Distance(result.BallEnd, OwnGoal(situation)) - Vector2.Distance(start, OwnGoal(situation));
            float score = (gained - MinGain) * 10f;
            // Bola parada na própria área continua perigo.
            if (FieldLayout.InArea(result.BallEnd, situation.Side)) score -= 15f;
            return score;
        }

        public override bool Accepts(in AiVerdict verdict, AiSituation situation, AiDifficulty difficulty) =>
            verdict.SafeFor(difficulty) && verdict.Score > 0f;
    }
}
