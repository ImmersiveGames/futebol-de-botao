using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Bola do goleiro: a jogada terminou com a bola parada na faixa do goleiro, colada nele, ou dentro da área
    /// depois de tocar nele. Vira tiro de meta para o time dele, não importa quem jogou.
    /// </summary>
    public static class KeeperBall
    {
        /// <summary>Distância máxima entre a borda da bola e o goleiro para contar como colada nele.</summary>
        public const float NearGap = 0.25f;

        /// <summary>Time do goleiro dono da bola parada em <paramref name="ballPosition"/>. Nulo: bola de ninguém.</summary>
        public static TeamSide? Owner(Vector2 ballPosition, float ballRadius, TeamSide? touchedKeeper,
            IReadOnlyList<Goalkeeper> keepers, bool hasGoalkeeper)
        {
            if (!hasGoalkeeper) return null;
            foreach (var side in new[] { TeamSide.Bottom, TeamSide.Top })
            {
                if (FieldLayout.InKeeperZone(ballPosition, side)) return side;

                var keeper = Find(keepers, side);
                if (keeper == null) continue;
                if (Gap(ballPosition, ballRadius, keeper) <= NearGap) return side;
                if (touchedKeeper == side && FieldLayout.InArea(ballPosition, side)) return side;
            }
            return null;
        }

        /// <summary>A bola está entre o goleiro e a linha do gol (atrás dele).</summary>
        public static bool BehindKeeper(Vector2 ballPosition, Goalkeeper keeper)
        {
            float goalLine = FieldLayout.GoalLineY(keeper.Side);
            return Mathf.Abs(ballPosition.y - goalLine) < Mathf.Abs(keeper.transform.position.y - goalLine);
        }

        /// <summary>Distância entre a borda da bola e a caixa do goleiro (0 ou menos = encostada).</summary>
        public static float Gap(Vector2 ballPosition, float ballRadius, Goalkeeper keeper)
        {
            var box = keeper.GetComponent<Collider2D>();
            if (box == null) return float.MaxValue;
            var bounds = box.bounds;
            Vector2 closest = new(
                Mathf.Clamp(ballPosition.x, bounds.min.x, bounds.max.x),
                Mathf.Clamp(ballPosition.y, bounds.min.y, bounds.max.y));
            return Vector2.Distance(ballPosition, closest) - ballRadius;
        }

        public static Goalkeeper Find(IReadOnlyList<Goalkeeper> keepers, TeamSide side)
        {
            if (keepers == null) return null;
            foreach (var keeper in keepers)
                if (keeper != null && keeper.isActiveAndEnabled && keeper.Side == side) return keeper;
            return null;
        }
    }
}
