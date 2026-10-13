using System.Collections.Generic;

namespace FutebolDeBotao
{
    /// <summary>
    /// Marcar: chutar quando o gol vale (chute anunciado, ou sem goleiro com a bola no ataque) ou anunciar o
    /// "Vai chutar" quando há chute livre perto do gol.
    /// </summary>
    public sealed class ScoreGoal : AiGoal
    {
        private static readonly AiAction[] ActionList = { AiAction.CallShot, AiAction.ShootAtGoal };

        public override string Name => "Marcar";
        public override IReadOnlyList<AiAction> Actions => ActionList;

        public override float Priority(AiSituation situation)
        {
            if (AiAction.ShootAtGoal.CanRun(situation)) return 100f;
            if (AiAction.CallShot.CanRun(situation)) return 90f;
            return 0f;
        }

        public override float? Score(in SimResult result, in ShotCandidate candidate, AiAction action, AiSituation situation,
            in AiScoreContext context) => BaseScore(result, situation, context);

        // Chute anunciado tem que sair. Sem goleiro, um chute arriscado pode dar lugar a outro objetivo.
        public override bool Accepts(in AiVerdict verdict, AiSituation situation, AiDifficulty difficulty) =>
            situation.State == MatchState.ShotAim || verdict.SafeFor(difficulty);
    }
}
