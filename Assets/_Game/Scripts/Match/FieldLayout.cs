using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>Medidas do campo em unidades do mundo (campo vertical, centro em 0,0). Usadas pelas regras e pelo builder.</summary>
    public static class FieldLayout
    {
        public const float Width = 7f;
        public const float Height = 11f;
        public const float HalfWidth = Width * 0.5f;
        public const float HalfHeight = Height * 0.5f;
        public const float GoalWidth = 2.4f;
        /// <summary>Fundo do gol (da linha de fundo até a rede de trás).</summary>
        public const float GoalDepth = 0.8f;

        /// <summary>
        /// A colisão das paredes fica um pouco para dentro da face inclinada desenhada (3 px a 32 px por unidade): bola e
        /// botões encostam na parede em vez de parar na linha do feltro. As regras continuam usando a linha do feltro.
        /// </summary>
        public const float WallInset = 3f / 32f;

        /// <summary>Área do goleiro: retângulo na frente de cada gol.</summary>
        public const float AreaWidth = 4f;
        /// <summary>Fundo da área: 48 px no desenho das marcações (1,5 unidade).</summary>
        public const float AreaDepth = 1.5f;

        /// <summary>Marca do pênalti: no meio do campo do defensor.</summary>
        public const float PenaltyDistanceFromGoal = HalfHeight * 0.5f;

        /// <summary>Sentido para onde o time ataca.</summary>
        public static Vector2 AttackDirection(TeamSide side) => side == TeamSide.Bottom ? Vector2.up : Vector2.down;

        /// <summary>Y da linha de fundo de quem defende <paramref name="defending"/>.</summary>
        public static float GoalLineY(TeamSide defending) => defending == TeamSide.Bottom ? -HalfHeight : HalfHeight;

        /// <summary>A posição está dentro da área do gol defendido por <paramref name="defending"/>.</summary>
        public static bool InArea(Vector2 position, TeamSide defending)
        {
            if (Mathf.Abs(position.x) > AreaWidth * 0.5f) return false;
            float fromGoalLine = Mathf.Abs(position.y - GoalLineY(defending));
            bool sameSide = defending == TeamSide.Bottom ? position.y < 0f : position.y > 0f;
            return sameSide && fromGoalLine <= AreaDepth;
        }

        /// <summary>
        /// Na boca do gol a linha desenhada tem 3 px: 2 px no feltro (marcações) e 1 px no fundo do gol, além da beira
        /// do feltro. A bola só passou inteira quando passa também desse pixel.
        /// </summary>
        public const float GoalLineOutside = 1f / 32f;

        /// <summary>A bola passou inteira da linha de fundo (o centro está a mais de um raio além da linha desenhada).</summary>
        public static bool BallFullyInGoal(Vector2 ballPosition, float ballRadius) =>
            Mathf.Abs(ballPosition.y) >= HalfHeight + GoalLineOutside + ballRadius;

        /// <summary>Marca do pênalti contra quem defende <paramref name="defending"/>.</summary>
        public static Vector2 PenaltySpot(TeamSide defending) =>
            new(0f, GoalLineY(defending) + (defending == TeamSide.Bottom ? PenaltyDistanceFromGoal : -PenaltyDistanceFromGoal));
    }
}
