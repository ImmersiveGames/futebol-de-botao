using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Fechar o gol: nenhuma jogada na bola serviu (só falta, gol contra ou chute livre para o adversário); põe um botão
    /// entre a bola e o próprio gol, sem tocar em nada (a vez passa).
    /// </summary>
    public sealed class CloseGoalGoal : AiGoal
    {
        private const float CoverWidth = 0.35f;
        private static readonly AiAction[] ActionList = { AiAction.PlaceDisc };

        public override string Name => "Fechar o gol";
        public override IReadOnlyList<AiAction> Actions => ActionList;

        public override float? Score(in SimResult result, in ShotCandidate candidate, AiAction action, AiSituation situation,
            in AiScoreContext context, Vector2? from = null)
        {
            float? score = context.Planner.ScorePosition(result, candidate);
            if (score == null) return null;
            // Vale mais o ponto na linha entre a bola e o gol.
            bool covers = DistanceToSegment(candidate.Target, situation.Ball, OwnGoal(situation)) < CoverWidth;
            return score.Value + (covers ? 10f : 0f);
        }

        public override bool Accepts(in AiVerdict verdict, AiSituation situation, AiDifficulty difficulty) => true;

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude < 0.0001f ? 0f : Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(point, a + ab * t);
        }
    }
}
