using UnityEngine;
using UnityEngine.Tilemaps;

namespace FutebolDeBotao
{
    /// <summary>
    /// Desenho da mesa em pixel art: o feltro em faixas (lisas ou com gradiente, para comparar) e os gols grande e
    /// pequeno (sem goleiro). Montado por "Futebol de Botão/Aplicar artes"; a física da mesa não depende dele.
    /// </summary>
    public sealed class TableArt : MonoBehaviour
    {
        public enum FeltStyle { Lisa, Gradiente }

        [Tooltip("Faixas do feltro: tiles lisos ou com gradiente.")]
        [SerializeField] private FeltStyle feltStyle = FeltStyle.Lisa;
        [SerializeField] private Tilemap felt;
        [SerializeField] private TileBase bright;
        [SerializeField] private TileBase dark;
        [SerializeField] private TileBase brightGradient;
        [SerializeField] private TileBase darkGradient;
        [Tooltip("Primeira célula (canto de baixo à esquerda) e tamanho do feltro em tiles.")]
        [SerializeField] private Vector2Int feltOrigin = new(-4, -6);
        [SerializeField] private Vector2Int feltSize = new(7, 11);
        [SerializeField] private GameObject bigGoals;
        [SerializeField] private GameObject smallGoals;

        public void Configure(Tilemap feltTilemap, TileBase brightTile, TileBase darkTile, TileBase brightGradientTile,
            TileBase darkGradientTile, Vector2Int origin, Vector2Int size, GameObject big, GameObject small)
        {
            felt = feltTilemap;
            bright = brightTile;
            dark = darkTile;
            brightGradient = brightGradientTile;
            darkGradient = darkGradientTile;
            feltOrigin = origin;
            feltSize = size;
            bigGoals = big;
            smallGoals = small;
            PaintFelt();
            SetSmallGoals(false);
        }

        /// <summary>Sem goleiro o gol é menor: troca o desenho dos dois gols.</summary>
        public void SetSmallGoals(bool small)
        {
            if (bigGoals != null) bigGoals.SetActive(!small);
            if (smallGoals != null) smallGoals.SetActive(small);
        }

        /// <summary>Faixas horizontais: as linhas alternam clara e escura, começando clara embaixo (o meio fica claro).</summary>
        public void PaintFelt()
        {
            if (felt == null) return;
            bool gradient = feltStyle == FeltStyle.Gradiente && brightGradient != null && darkGradient != null;
            for (int row = 0; row < feltSize.y; row++)
            {
                bool light = row % 2 == 0;
                var tile = gradient ? (light ? brightGradient : darkGradient) : (light ? bright : dark);
                for (int column = 0; column < feltSize.x; column++)
                    felt.SetTile(new Vector3Int(feltOrigin.x + column, feltOrigin.y + row, 0), tile);
            }
        }

#if UNITY_EDITOR
        // Trocar a opção no Inspector redesenha o feltro na hora (também no Play). O Tilemap não aceita mudanças
        // dentro do OnValidate, então espera o editor terminar.
        private void OnValidate() => UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null) PaintFelt();
        };
#endif
    }
}
