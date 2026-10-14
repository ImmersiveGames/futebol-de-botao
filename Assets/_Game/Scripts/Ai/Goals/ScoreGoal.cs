using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Marcar: dá pra chutar? Com goleiro, anuncia o "Vai chutar" se a chance passa do mínimo (a conta da chance fica
    /// no AiBrain) e depois chuta. Sem goleiro, com a bola no ataque, chuta direto se a chance passa do mínimo.
    /// A conta: gol que vale.
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

        // Chute anunciado tem que sair. Sem goleiro: só se a chance passa do mínimo, senão segue para os outros objetivos.
        public override bool Accepts(in AiVerdict verdict, AiSituation situation, AiDifficulty difficulty) =>
            situation.State == MatchState.ShotAim ||
            (verdict.SafeFor(difficulty) && verdict.Score / 100f >= NeededChance(situation, difficulty));
    }
}
