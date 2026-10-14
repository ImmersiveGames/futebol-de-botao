using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Marcar: dá pra chutar? Com goleiro, anuncia o "Vai chutar" sempre que tem chute livre (sem conta de chance) e
    /// depois chuta no canto que o goleiro cobre pior. Sem goleiro, com a bola no ataque, chuta direto se algum chute
    /// entra. A conta: gol que vale.
    /// </summary>
    public sealed class ScoreGoal : AiGoal
    {
        private static readonly AiAction[] ActionList = { AiAction.CallShot, AiAction.ShootAtGoal };

        public override string Name => "Marcar";
        public override IReadOnlyList<AiAction> Actions => ActionList;

        /// <summary>100 quando é gol que vale (a média com o erro do nível vira a chance x 100).</summary>
        public override float? Score(in SimResult result, in ShotCandidate candidate, AiAction action, AiSituation situation,
            in AiScoreContext context, Vector2? from = null)
        {
            if (result.Foul) return 0f;
            return GoalOutcome(result, situation, context) is float goal && goal > 0f ? 100f : 0f;
        }

        // Chute anunciado tem que sair. Sem goleiro: chuta se alguma versão do chute entra, senão segue para os outros.
        public override bool Accepts(in AiVerdict verdict, AiSituation situation, AiDifficulty difficulty) =>
            situation.State == MatchState.ShotAim || (verdict.SafeFor(difficulty) && verdict.Score > 0f);
    }
}
