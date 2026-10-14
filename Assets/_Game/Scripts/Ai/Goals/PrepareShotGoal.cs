using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Preparar o chute (tem toque sobrando): ficar com a bola e deixá-la onde algum botão tenha chute livre no toque
    /// seguinte. Sem isso, a conta é o quanto a bola avança. Com 3 toques, imagina também o toque seguinte.
    /// </summary>
    public sealed class PrepareShotGoal : AiGoal
    {
        /// <summary>Nota de deixar o chute pronto (sempre acima de só avançar).</summary>
        private const float SetupScore = 60f;

        private static readonly AiAction[] ActionList = { AiAction.KickBallForward };

        public override string Name => "Preparar o chute";
        public override IReadOnlyList<AiAction> Actions => ActionList;
        public override bool PlansAhead => true;

        public override float? Score(in SimResult result, in ShotCandidate candidate, AiAction action, AiSituation situation,
            in AiScoreContext context, Vector2? from = null)
        {
            if (GoalOutcome(result, situation, context) is float goal) return goal;
            if (!KeepsBall(result, situation) || result.End == null) return null;

            var opponent = situation.Side == TeamSide.Bottom ? TeamSide.Top : TeamSide.Bottom;
            var keeper = KeeperBall.Find(context.Planner.Keepers, opponent);
            float ballRadius = context.Planner.BallRadius;
            Vector2 end = result.BallEnd;
            // Bola parada na área do goleiro dele vira tiro de meta: perde a bola.
            if (keeper != null && KeeperBall.InZone(end, ballRadius, keeper)) return null;

            Vector2 goalCenter = new(0f, FieldLayout.GoalLineY(opponent));
            float toGoal = Vector2.Distance(end, goalCenter);
            // Chute pronto: algum botão tem chute livre e o goleiro não consegue chegar na bola.
            if (!KeeperBall.CanReach(end, ballRadius, keeper) &&
                context.Planner.HasShotSetup(result.End, situation.TeamDiscs, situation.ShotCallDistance))
                return SetupScore + (situation.ShotCallDistance - toGoal) * 3f;

            Vector2 start = from ?? situation.Ball;
            return Vector2.Dot(end - start, FieldLayout.AttackDirection(situation.Side)) * 3f;
        }

        // Precisa ficar com a bola: se perde, é melhor afastar ou proteger.
        public override bool Accepts(in AiVerdict verdict, AiSituation situation, AiDifficulty difficulty) =>
            verdict.SafeFor(difficulty) && verdict.LoseChance < 0.5f;
    }
}
