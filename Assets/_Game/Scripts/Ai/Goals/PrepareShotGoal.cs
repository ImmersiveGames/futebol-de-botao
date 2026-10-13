using System.Collections.Generic;

namespace FutebolDeBotao
{
    /// <summary>
    /// Preparar o chute: com toques sobrando e a bola chegando perto do gol, deixar a bola onde algum botão tenha chute
    /// livre no toque seguinte (aí dá para anunciar o "Vai chutar"; sem goleiro, já é o chute).
    /// </summary>
    public sealed class PrepareShotGoal : AiGoal
    {
        /// <summary>Até esta distância a mais que a do Vai chutar a IA já pensa em preparar o chute.</summary>
        private const float ExtraReach = 3f;
        /// <summary>Nota mínima (sem contar o toque seguinte) para a jogada contar como chute preparado.</summary>
        private const float AcceptScore = 30f;

        private static readonly AiAction[] ActionList = { AiAction.KickBallForward };

        public override string Name => "Preparar o chute";
        public override IReadOnlyList<AiAction> Actions => ActionList;

        public override float Priority(AiSituation situation)
        {
            if (!AiAction.KickBallForward.CanRun(situation) || situation.TouchesLeft < 2) return 0f;
            if (AiAction.CallShot.CanRun(situation)) return 0f;
            return situation.BallToOpponentGoal <= situation.ShotCallDistance + ExtraReach ? 50f : 0f;
        }

        public override float? Score(in SimResult result, in ShotCandidate candidate, AiAction action, AiSituation situation,
            in AiScoreContext context)
        {
            float score = BaseScore(result, situation, context);
            if (KeepsBall(result, situation) &&
                context.Planner.HasShotSetup(result.End, situation.TeamDiscs, situation.ShotCallDistance))
                score += ShotPlanner.ShotSetupBonus;
            return score;
        }

        public override bool Accepts(in AiVerdict verdict, AiSituation situation, AiDifficulty difficulty) =>
            verdict.SafeFor(difficulty) && verdict.Score - verdict.Follow >= AcceptScore;
    }
}
