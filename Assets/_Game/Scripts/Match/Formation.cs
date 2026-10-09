using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Esquema tático: posições iniciais dos botões de um time, em unidades do mundo, para o time de baixo.
    /// O time de cima usa as mesmas posições espelhadas no eixo Y.
    /// </summary>
    [CreateAssetMenu(menuName = "Futebol de Botão/Formação", fileName = "Formacao")]
    public sealed class Formation : ScriptableObject
    {
        [Tooltip("Nome do esquema, contado de trás para a frente (ex.: 2-2-1).")]
        [SerializeField] private string displayName = "2-2-1";
        [Tooltip("Estilo mostrado embaixo do nome (ex.: Defensivo).")]
        [SerializeField] private string style = "Equilibrado";
        [SerializeField] private Vector2[] positions = Default5;

        // Os esquemas deixam livres as faixas laterais (|x| > 1,75) da Seleção de times, onde ficam escudo e setas.
        public static readonly Vector2[] Default5 =
        {
            new(0f, -0.9f),
            new(-1.6f, -1.8f),
            new(1.6f, -1.8f),
            new(-1.0f, -3.4f),
            new(1.0f, -3.4f)
        };

        public static readonly Vector2[] Defensive5 =
        {
            new(-0.7f, -1.1f),
            new(0.7f, -1.1f),
            new(-1.5f, -3.2f),
            new(0f, -3.5f),
            new(1.5f, -3.2f)
        };

        public static readonly Vector2[] Offensive5 =
        {
            new(0f, -0.9f),
            new(-1.6f, -1.2f),
            new(1.6f, -1.2f),
            new(-1.0f, -3.2f),
            new(1.0f, -3.2f)
        };

        public static readonly Vector2[] Diamond5 =
        {
            new(-1.4f, -1.0f),
            new(1.4f, -1.0f),
            new(0f, -2.3f),
            new(-1.2f, -3.4f),
            new(1.2f, -3.4f)
        };

        public static readonly Vector2[] Default3 =
        {
            new(0f, -1.2f),
            new(-1.5f, -2.8f),
            new(1.5f, -2.8f)
        };

        public static readonly Vector2[] Offensive3 =
        {
            new(-1.2f, -1.1f),
            new(1.2f, -1.1f),
            new(0f, -3.0f)
        };

        public string DisplayName => displayName;
        public string Style => style;
        public int Count => positions != null ? positions.Length : 0;

        public void SetPositions(Vector2[] bottomSidePositions) => positions = (Vector2[])bottomSidePositions.Clone();

        public void Set(string newName, string newStyle, Vector2[] bottomSidePositions)
        {
            displayName = newName;
            style = newStyle;
            SetPositions(bottomSidePositions);
        }

        public static Formation Create(string newName, string newStyle, Vector2[] bottomSidePositions)
        {
            var formation = CreateInstance<Formation>();
            formation.name = newName;
            formation.Set(newName, newStyle, bottomSidePositions);
            return formation;
        }

        public Vector2 PositionFor(int index, TeamSide side) => Mirror(positions[index], side);

        public static Vector2 Mirror(Vector2 bottomSidePosition, TeamSide side) =>
            side == TeamSide.Bottom ? bottomSidePosition : new Vector2(bottomSidePosition.x, -bottomSidePosition.y);
    }
}
