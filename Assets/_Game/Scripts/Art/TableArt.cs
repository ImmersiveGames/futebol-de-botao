using UnityEngine;
using UnityEngine.Tilemaps;

namespace FutebolDeBotao
{
    /// <summary>
    /// Desenho da mesa em pixel art: o feltro e os gols grande e pequeno (sem goleiro). O feltro é um
    /// <see cref="FeltTheme"/> pintado com os tiles brancos da <see cref="FeltArt"/>: o da partida (sorteado entre os
    /// liberados a cada partida nova, o Reiniciar mantém) ou, para testar, o escolhido no Inspector. Sem os feltros,
    /// volta às faixas antigas (Field_Braight/Dark). Montado por "Futebol de Botão/Aplicar artes"; a física da mesa
    /// não depende dele.
    /// </summary>
    public sealed class TableArt : MonoBehaviour
    {
        private const string UpperHalfName = "Feltro (meia fileira)";

        [Tooltip("Para testar: este feltro em vez do da partida. Vazio: o da partida (o mesmo da Seleção de times).")]
        [SerializeField] private FeltTheme feltOverride;
        [SerializeField] private Tilemap felt;
        [Tooltip("Faixas antigas, usadas só se não houver feltros (Resources/Feltros) ou a arte deles.")]
        [SerializeField] private TileBase bright;
        [SerializeField] private TileBase dark;
        [Tooltip("Primeira célula (canto de baixo à esquerda) e tamanho do feltro em tiles.")]
        [SerializeField] private Vector2Int feltOrigin = new(-4, -6);
        [SerializeField] private Vector2Int feltSize = new(7, 11);
        [SerializeField] private GameObject bigGoals;
        [SerializeField] private GameObject smallGoals;

        private Tilemap upperHalf;

        public void Configure(Tilemap feltTilemap, TileBase brightTile, TileBase darkTile, Vector2Int origin, Vector2Int size,
            GameObject big, GameObject small)
        {
            felt = feltTilemap;
            bright = brightTile;
            dark = darkTile;
            feltOrigin = origin;
            feltSize = size;
            bigGoals = big;
            smallGoals = small;
            PaintFelt();
            SetSmallGoals(false);
        }

        private void Start() => PaintFelt();

        /// <summary>Sem goleiro o gol é menor: troca o desenho dos dois gols.</summary>
        public void SetSmallGoals(bool small)
        {
            if (bigGoals != null) bigGoals.SetActive(!small);
            if (smallGoals != null) smallGoals.SetActive(small);
        }

        /// <summary>Pinta cada fileira com a cor do feltro (de baixo para cima).</summary>
        public void PaintFelt()
        {
            if (felt == null) return;
            var theme = feltOverride != null ? feltOverride : Application.isPlaying ? FeltTheme.Current() : FirstUnlocked();
            var art = FeltArt.Load();
            ClearUpperHalf();
            if (theme == null || art == null || !art.Has(theme.tile))
            {
                PaintOldStripes();
                return;
            }

            var tile = art.GetTile(theme.tile);
            int rows = feltSize.y;
            for (int row = 0; row < rows; row++)
            {
                var color = theme.RowColor(row, rows);
                for (int column = 0; column < feltSize.x; column++)
                    PaintCell(felt, new Vector3Int(feltOrigin.x + column, feltOrigin.y + row, 0), tile, color);
            }

            if (!theme.SplitsMiddleRow(rows)) return;
            var half = art.UpperHalfTile(theme.tile);
            var layer = UpperHalfLayer();
            if (half == null || layer == null) return;
            for (int column = 0; column < feltSize.x; column++)
                PaintCell(layer, new Vector3Int(feltOrigin.x + column, feltOrigin.y + rows / 2, 0), half, theme.secondary);
        }

        private static void PaintCell(Tilemap map, Vector3Int cell, TileBase tile, Color color)
        {
            map.SetTile(cell, tile);
            map.SetTileFlags(cell, TileFlags.None);
            map.SetColor(cell, color);
        }

        /// <summary>Faixas de antes (sem os feltros): as linhas alternam clara e escura, começando clara embaixo.</summary>
        private void PaintOldStripes()
        {
            for (int row = 0; row < feltSize.y; row++)
            {
                var tile = row % 2 == 0 ? bright : dark;
                for (int column = 0; column < feltSize.x; column++)
                    PaintCell(felt, new Vector3Int(feltOrigin.x + column, feltOrigin.y + row, 0), tile, Color.white);
            }
        }

        /// <summary>Fora do Play (no editor): o primeiro liberado, sem sortear.</summary>
        private static FeltTheme FirstUnlocked()
        {
            var all = FeltTheme.All();
            foreach (var theme in all)
                if (theme.unlockedByDefault) return theme;
            return all.Length > 0 ? all[0] : null;
        }

        /// <summary>Camada da metade de cima da fileira do meio (Meio a meio), logo acima do feltro; criada quando precisa.</summary>
        private Tilemap UpperHalfLayer()
        {
            if (upperHalf == null)
            {
                var existing = felt.transform.parent.Find(UpperHalfName);
                if (existing != null) upperHalf = existing.GetComponent<Tilemap>();
            }
            if (upperHalf != null) return upperHalf;

            var go = new GameObject(UpperHalfName, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(felt.transform.parent, false);
            go.transform.localPosition = felt.transform.localPosition;
            // Criada na hora (o tile da metade não é um asset): não vai para a cena salva.
            go.hideFlags = HideFlags.DontSave;
            var renderer = go.GetComponent<TilemapRenderer>();
            var feltRenderer = felt.GetComponent<TilemapRenderer>();
            renderer.sortingLayerID = feltRenderer.sortingLayerID;
            renderer.sortingOrder = feltRenderer.sortingOrder + 1;
            renderer.sharedMaterial = feltRenderer.sharedMaterial;
            upperHalf = go.GetComponent<Tilemap>();
            return upperHalf;
        }

        private void ClearUpperHalf()
        {
            if (upperHalf == null && felt != null)
            {
                var existing = felt.transform.parent.Find(UpperHalfName);
                if (existing != null) upperHalf = existing.GetComponent<Tilemap>();
            }
            if (upperHalf != null) upperHalf.ClearAllTiles();
        }

#if UNITY_EDITOR
        // Trocar o feltro no Inspector redesenha na hora (também no Play). O Tilemap não aceita mudanças dentro do
        // OnValidate, então espera o editor terminar.
        private void OnValidate() => UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null) PaintFelt();
        };
#endif
    }
}
