using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Fechar o gol: é o último toque e o adversário teria chute fácil daqui; põe um botão entre a bola e o gol em vez
    /// de mexer na bola. Só quando a IA olhou o perigo (depende do nível).
    /// </summary>
    public sealed class CloseGoalGoal : AiGoal
    {
        private const float MinDanger = 0.5f;
        private const float CoverWidth = 0.35f;
        private static readonly AiAction[] ActionList = { AiAction.PlaceDisc };

        public override string Name => "Fechar o gol";
        public override IReadOnlyList<AiAction> Actions => ActionList;

        public override float Priority(AiSituation situation)
        {
            if (!AiAction.PlaceDisc.CanRun(situation) || !situation.IsLastTouch) return 0f;
            if (!situation.ReadDanger || situation.Danger < MinDanger) return 0f;
            return 45f + 40f * situation.Danger;
        }

        public override float? Score(in SimResult result, in ShotCandidate candidate, AiAction action, AiSituation situation,
            in AiScoreContext context)
        {
            float? score = context.Planner.ScorePosition(result, candidate);
            if (score == null) return null;
            // Só vale o ponto na linha entre a bola e o gol.
            var ownGoal = new Vector2(0f, FieldLayout.GoalLineY(situation.Side));
            bool covers = DistanceToSegment(candidate.Target, situation.Ball, ownGoal) < CoverWidth;
            return score.Value + (covers ? 10f : -10f);
        }

        public override bool Accepts(in AiVerdict verdict, AiSituation situation, AiDifficulty difficulty) => verdict.Score > 0f;

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude < 0.0001f ? 0f : Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(point, a + ab * t);
        }
    }
}
