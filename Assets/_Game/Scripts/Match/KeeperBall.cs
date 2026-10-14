using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Bola do goleiro: a jogada terminou com a bola parada na área em volta do goleiro (a caixa dele mais uma margem
    /// dos lados e na frente, e o espaço atrás dele até o fundo do gol, se a bola não entrou inteira).
    /// Vira tiro de meta para o time dele,
    /// não importa quem jogou.
    /// </summary>
    public static class KeeperBall
    {
        /// <summary>Margem para cada lado da caixa do goleiro.</summary>
        public const float SideMargin = 0.3f;
        /// <summary>Margem na frente da caixa do goleiro (para o meio do campo).</summary>
        public const float FrontMargin = 0.4f;

        /// <summary>Time do goleiro dono da bola parada em <paramref name="ballPosition"/>. Nulo: bola de ninguém.</summary>
        public static TeamSide? Owner(Vector2 ballPosition, float ballRadius, IReadOnlyList<Goalkeeper> keepers, bool hasGoalkeeper)
        {
            if (!hasGoalkeeper) return null;
            foreach (var side in new[] { TeamSide.Bottom, TeamSide.Top })
            {
                var keeper = Find(keepers, side);
                if (keeper != null && InZone(ballPosition, ballRadius, keeper)) return side;
            }
            return null;
        }

        /// <summary>A bola encosta na área em volta do goleiro.</summary>
        public static bool InZone(Vector2 ballPosition, float ballRadius, Goalkeeper keeper)
        {
            var box = keeper.GetComponent<Collider2D>();
            if (box == null) return false;
            var bounds = box.bounds;
            float goalLine = FieldLayout.GoalLineY(keeper.Side);
            bool bottom = keeper.Side == TeamSide.Bottom;

            float minX = bounds.min.x - SideMargin - ballRadius;
            float maxX = bounds.max.x + SideMargin + ballRadius;
            float front = (bottom ? bounds.max.y + FrontMargin : bounds.min.y - FrontMargin) + (bottom ? ballRadius : -ballRadius);
            // Atrás dele vai até o fundo do gol: a bola parada entre o goleiro e o gol, já na boca do gol mas sem entrar
            // inteira (não foi gol), também é dele. Fora da faixa do goleiro, a bola na boca do gol segue em jogo.
            float back = bottom ? goalLine - FieldLayout.GoalDepth : goalLine + FieldLayout.GoalDepth;
            float minY = bottom ? back : front;
            float maxY = bottom ? front : back;
            return ballPosition.x >= minX && ballPosition.x <= maxX && ballPosition.y >= minY && ballPosition.y <= maxY;
        }

        /// <summary>
        /// O goleiro consegue deixar a bola parada em <paramref name="ballPosition"/> na área em volta dele, andando para
        /// algum lugar onde pode ficar (no Vai chutar o defensor faria isso e a bola viraria dele).
        /// </summary>
        public static bool CanReach(Vector2 ballPosition, float ballRadius, Goalkeeper keeper)
        {
            if (keeper == null) return false;
            var box = keeper.GetComponent<Collider2D>();
            if (box == null) return false;
            var bounds = box.bounds;
            float half = bounds.extents.x + SideMargin + ballRadius;
            if (ballPosition.x < keeper.MinX - half || ballPosition.x > keeper.MaxX + half) return false;
            // A altura da área não muda quando ele anda: confere só a altura, com a bola no x onde ele está.
            return InZone(new Vector2(bounds.center.x, ballPosition.y), ballRadius, keeper);
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
