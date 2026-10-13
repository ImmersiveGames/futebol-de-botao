using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>Tirar do perigo: bola perto do próprio gol vai para longe dele, de preferência para os lados.</summary>
    public sealed class ClearDangerGoal : AiGoal
    {
        private static readonly AiAction[] ActionList = { AiAction.KickBallForward };

        /// <summary>A bola tem que terminar pelo menos isto mais longe do próprio gol.</summary>
        private const float MinGain = 0.3f;

        public override string Name => "Tirar do perigo";
        public override IReadOnlyList<AiAction> Actions => ActionList;

        public override float Priority(AiSituation situation)
        {
            if (!AiAction.KickBallForward.CanRun(situation)) return 0f;
            if (situation.BallToOwnGoal >= AiSituation.DangerDistance) return 0f;
            // Quanto mais perto do gol, mais urgente: de 60 (na borda) a 90 (em cima da linha).
            return 60f + 30f * (1f - situation.BallToOwnGoal / AiSituation.DangerDistance);
        }

        public override float? Score(in SimResult result, in ShotCandidate candidate, AiAction action, AiSituation situation,
            in AiScoreContext context)
        {
            float score = BaseScore(result, situation, context);
            if (result.Foul || result.GoalOf != null) return score;
            // Além da conta padrão (que já gosta de afastar), cada unidade longe do gol vale mais.
            var ownGoal = new Vector2(0f, FieldLayout.GoalLineY(situation.Side));
            float gained = Vector2.Distance(result.BallEnd, ownGoal) - situation.BallToOwnGoal;
            score += gained * 2f;
            // Tirar do perigo é afastar: bola que fica onde estava ou chega mais perto do gol não serve.
            if (gained < MinGain) score -= 30f;
            // Bola parada na própria área continua perigo.
            if (FieldLayout.InArea(result.BallEnd, situation.Side)) score -= 15f;
            return score;
        }

        public override bool Accepts(in AiVerdict verdict, AiSituation situation, AiDifficulty difficulty) =>
            verdict.SafeFor(difficulty) && verdict.Score > 0f;
    }
}
