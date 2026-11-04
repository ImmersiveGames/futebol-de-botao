using UnityEngine;
using UnityEngine.Tilemaps;

namespace FutebolDeBotao
{
    /// <summary>
    /// Desenho da mesa em pixel art: a mesa (<see cref="TableTheme"/>), o feltro e os gols grande e pequeno (sem
    /// goleiro). A mesa clássica usa a moldura e os gols montados por "Futebol de Botão/Aplicar artes"; nas mesas de
    /// placas as placas e as traves são montadas aqui (sem ir para a cena salva), o feltro passa por baixo delas e a
    /// colisão das paredes vai para a borda de dentro das placas. O feltro é um <see cref="FeltTheme"/> pintado com os
    /// tiles brancos da <see cref="FeltArt"/>. Mesa e feltro são os da partida (sorteados entre os liberados a cada
    /// partida nova, o Reiniciar mantém) ou, para testar, os escolhidos no Inspector. Sem os feltros, volta às faixas
    /// antigas (Field_Braight/Dark).
    /// </summary>
    public sealed class TableArt : MonoBehaviour
    {
        private const string UpperHalfName = "Feltro (meia fileira)";
        private const string BuiltName = "Mesa montada";
        private const int BoardOrder = -12;
        private const int GoalOrder = 13;

        [Tooltip("Para testar: esta mesa em vez da da partida. Vazio: a da partida (a mesma da Seleção de times).")]
        [SerializeField] private TableTheme tableOverride;
        [Tooltip("Para testar: a cor das traves (0 = a primeira cor da mesa). -1: a da partida.")]
        [SerializeField] private int goalColorOverride = -1;
        [Tooltip("Para testar: este feltro em vez do da partida. Vazio: o da partida (o mesmo da Seleção de times).")]
        [SerializeField] private FeltTheme feltOverride;
        [SerializeField] private Tilemap felt;
        [Tooltip("Faixas antigas, usadas só se não houver feltros (Resources/Feltros) ou a arte deles.")]
        [SerializeField] private TileBase bright;
        [SerializeField] private TileBase dark;
        [Tooltip("Primeira célula (canto de baixo à esquerda) e tamanho do feltro em tiles.")]
        [SerializeField] private Vector2Int feltOrigin = new(-4, -6);
        [SerializeField] private Vector2Int feltSize = new(7, 11);
        [Tooltip("Moldura da mesa clássica (escondida nas mesas de placas).")]
        [SerializeField] private GameObject classicFrame;
        [Tooltip("Gols da mesa clássica.")]
        [SerializeField] private GameObject bigGoals;
        [SerializeField] private GameObject smallGoals;

        private Tilemap upperHalf;
        private bool small;
        private bool boards;
        private GameObject boardsBig, boardsSmall, goalsBig, goalsSmall;

        public void Configure(Tilemap feltTilemap, TileBase brightTile, TileBase darkTile, Vector2Int origin, Vector2Int size,
            GameObject frame, GameObject big, GameObject smallGoal)
        {
            felt = feltTilemap;
            bright = brightTile;
            dark = darkTile;
            feltOrigin = origin;
            feltSize = size;
            classicFrame = frame;
            bigGoals = big;
            smallGoals = smallGoal;
            Refresh();
            SetSmallGoals(false);
        }

        private void Awake()
        {
            Refresh();
            ApplyWalls();
        }

        /// <summary>Sem goleiro o gol é menor: troca o desenho dos dois gols (e das placas que encostam nele).</summary>
        public void SetSmallGoals(bool smallGoal)
        {
            small = smallGoal;
            if (bigGoals != null) bigGoals.SetActive(!boards && !small);
            if (smallGoals != null) smallGoals.SetActive(!boards && small);
            if (boardsBig != null) boardsBig.SetActive(!small);
            if (goalsBig != null) goalsBig.SetActive(!small);
            if (boardsSmall != null) boardsSmall.SetActive(small);
            if (goalsSmall != null) goalsSmall.SetActive(small);
        }

        /// <summary>Monta a mesa e pinta o feltro (de novo, se a mesa ou o feltro mudarem).</summary>
        public void Refresh()
        {
            var table = CurrentTable();
            boards = table != null && table.HasBoards;
            // Cena montada antes das mesas: a moldura é o filho "Moldura".
            if (classicFrame == null && transform.Find("Moldura") is { } frame) classicFrame = frame.gameObject;
            if (classicFrame != null) classicFrame.SetActive(!boards);
            BuildBoards(boards ? table : null);
            SetSmallGoals(small);
            PaintFelt();
        }

        private TableTheme CurrentTable()
        {
            if (tableOverride != null) return tableOverride;
            if (Application.isPlaying) return TableTheme.Current();
            var all = TableTheme.All();
            foreach (var table in all)
                if (table.unlockedByDefault) return table;
            return all.Length > 0 ? all[0] : null;
        }

        private int GoalColorIndex => goalColorOverride >= 0 ? goalColorOverride : Application.isPlaying ? MatchSession.GoalColor : 0;

        // ---- Placas e traves ----

        private void BuildBoards(TableTheme table)
        {
            var old = transform.Find(BuiltName);
            if (old != null)
            {
                if (Application.isPlaying) Destroy(old.gameObject);
                else DestroyImmediate(old.gameObject);
            }
            boardsBig = boardsSmall = goalsBig = goalsSmall = null;
            if (table == null) return;

            var root = new GameObject(BuiltName).transform;
            root.SetParent(transform, false);
            // Montada na hora a partir da mesa: não vai para a cena salva.
            root.gameObject.hideFlags = HideFlags.DontSave;
            int color = GoalColorIndex;
            boardsBig = CreatePieces(root, "Placas (gol grande)", table.Boards(false), BoardOrder);
            boardsSmall = CreatePieces(root, "Placas (gol pequeno)", table.Boards(true), BoardOrder);
            goalsBig = CreatePieces(root, "Traves grandes", table.Goals(false, color), GoalOrder);
            goalsSmall = CreatePieces(root, "Traves pequenas", table.Goals(true, color), GoalOrder);
        }

        private static GameObject CreatePieces(Transform parent, string groupName, System.Collections.Generic.List<TableTheme.Piece> pieces, int order)
        {
            var group = new GameObject(groupName).transform;
            group.SetParent(parent, false);
            group.gameObject.hideFlags = HideFlags.DontSave;
            foreach (var piece in pieces)
            {
                var go = new GameObject(piece.Sprite.name) { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(group, false);
                go.transform.SetPositionAndRotation(piece.Position, Quaternion.Euler(0f, 0f, piece.Rotation));
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = piece.Sprite;
                renderer.flipX = piece.FlipX;
                renderer.sortingOrder = order;
            }
            return group.gameObject;
        }

        /// <summary>
        /// Colisão das paredes na borda de dentro da mesa da partida: as laterais e as do fundo (ao lado do gol) ficam
        /// <see cref="TableTheme.WallInset"/> para fora da linha do feltro. Só no começo da cena (antes das traves sem
        /// goleiro, que copiam as paredes do fundo).
        /// </summary>
        private void ApplyWalls()
        {
            if (!Application.isPlaying) return;
            var table = CurrentTable();
            float inset = table != null && (table.classic || table.HasBoards) ? table.WallInset : FieldLayout.ClassicWallInset;
            FieldLayout.WallInset = inset;
            float sideSegment = (FieldLayout.Width - FieldLayout.GoalWidth) * 0.5f + inset;
            foreach (var wall in FindObjectsByType<Wall>())
            {
                if (!wall.PushesBack) continue;
                var t = wall.transform;
                var position = t.position;
                var scale = t.localScale;
                if (Mathf.Abs(position.x) > FieldLayout.HalfWidth)
                {
                    position.x = Mathf.Sign(position.x) * (FieldLayout.HalfWidth + inset + scale.x * 0.5f);
                    scale.y = FieldLayout.Height + 2f * (scale.x + inset);
                }
                else if (Mathf.Abs(position.y) > FieldLayout.HalfHeight)
                {
                    position.y = Mathf.Sign(position.y) * (FieldLayout.HalfHeight + inset + scale.y * 0.5f);
                    position.x = Mathf.Sign(position.x) * (FieldLayout.GoalWidth * 0.5f + sideSegment * 0.5f);
                    scale.x = sideSegment;
                }
                else continue;
                t.position = position;
                t.localScale = scale;
            }
        }

        // ---- Feltro ----

        /// <summary>
        /// Pinta cada fileira com a cor do feltro (de baixo para cima). Nas mesas de placas o feltro vai uma célula além,
        /// por baixo das placas e dos gols, com a cor da fileira vizinha.
        /// </summary>
        public void PaintFelt()
        {
            if (felt == null) return;
            var theme = feltOverride != null ? feltOverride : Application.isPlaying ? FeltTheme.Current() : FirstUnlocked();
            var art = FeltArt.Load();
            felt.ClearAllTiles();
            ClearUpperHalf();
            int rows = feltSize.y;
            int margin = boards ? 1 : 0;
            bool themed = theme != null && art != null && art.Has(theme.tile);
            var tile = themed ? art.GetTile(theme.tile) : null;
            for (int row = -margin; row < rows + margin; row++)
            {
                int inside = Mathf.Clamp(row, 0, rows - 1);
                var color = themed ? theme.RowColor(inside, rows) : Color.white;
                var rowTile = themed ? tile : inside % 2 == 0 ? bright : dark;
                for (int column = -margin; column < feltSize.x + margin; column++)
                    PaintCell(felt, new Vector3Int(feltOrigin.x + column, feltOrigin.y + row, 0), rowTile, color);
            }

            if (!themed || !theme.SplitsMiddleRow(rows)) return;
            var half = art.UpperHalfTile(theme.tile);
            var layer = UpperHalfLayer();
            if (half == null || layer == null) return;
            for (int column = -margin; column < feltSize.x + margin; column++)
                PaintCell(layer, new Vector3Int(feltOrigin.x + column, feltOrigin.y + rows / 2, 0), half, theme.secondary);
        }

        private static void PaintCell(Tilemap map, Vector3Int cell, TileBase tile, Color color)
        {
            map.SetTile(cell, tile);
            map.SetTileFlags(cell, TileFlags.None);
            map.SetColor(cell, color);
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
        // Trocar a mesa ou o feltro no Inspector redesenha na hora (também no Play; a colisão das paredes não muda depois
        // do começo). O Tilemap não aceita mudanças dentro do OnValidate, então espera o editor terminar.
        private void OnValidate() => UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null) Refresh();
        };
#endif
    }
}
