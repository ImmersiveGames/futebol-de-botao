using System.Collections.Generic;

namespace FutebolDeBotao
{
    /// <summary>Toque seguro: nada bom na bola; posiciona um botão sem tocar em nada (a vez passa).</summary>
    public sealed class SafeTouchGoal : AiGoal
    {
        private static readonly AiAction[] ActionList = { AiAction.PlaceDisc };

        public override string Name => "Toque seguro";
        public override IReadOnlyList<AiAction> Actions => ActionList;

        public override float Priority(AiSituation situation) => AiAction.PlaceDisc.CanRun(situation) ? 10f : 0f;

        public override float? Score(in SimResult result, in ShotCandidate candidate, AiAction action, AiSituation situation,
            in AiScoreContext context) => context.Planner.ScorePosition(result, candidate);

        public override bool Accepts(in AiVerdict verdict, AiSituation situation, AiDifficulty difficulty) => true;
    }
}
