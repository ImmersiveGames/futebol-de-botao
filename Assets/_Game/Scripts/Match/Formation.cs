using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Posições iniciais dos botões de um time, em unidades do mundo, para o time de baixo.
    /// O time de cima usa as mesmas posições espelhadas no eixo Y.
    /// </summary>
    [CreateAssetMenu(menuName = "Futebol de Botão/Formação", fileName = "Formacao")]
    public sealed class Formation : ScriptableObject
    {
        [SerializeField] private Vector2[] positions = Default5;

        public static readonly Vector2[] Default5 =
        {
            new(0f, -0.9f),
            new(-1.6f, -1.8f),
            new(1.6f, -1.8f),
            new(-1.0f, -3.4f),
            new(1.0f, -3.4f)
        };

        public static readonly Vector2[] Default3 =
        {
            new(0f, -1.2f),
            new(-1.5f, -2.8f),
            new(1.5f, -2.8f)
        };

        public int Count => positions != null ? positions.Length : 0;

        public void SetPositions(Vector2[] bottomSidePositions) => positions = (Vector2[])bottomSidePositions.Clone();

        public Vector2 PositionFor(int index, TeamSide side) => Mirror(positions[index], side);

        public static Vector2 Mirror(Vector2 bottomSidePosition, TeamSide side) =>
            side == TeamSide.Bottom ? bottomSidePosition : new Vector2(bottomSidePosition.x, -bottomSidePosition.y);
    }
}
