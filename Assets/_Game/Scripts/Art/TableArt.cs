using UnityEngine;
using UnityEngine.Tilemaps;

namespace FutebolDeBotao
{
    /// <summary>
    /// Desenho da mesa em pixel art: a mesa (<see cref="TableTheme"/>), o feltro e os gols grande e pequeno (sem
    /// goleiro). As placas e as traves são montadas aqui (sem ir para a cena salva), o feltro passa por baixo delas e a
    /// colisão das paredes vai para a borda de dentro das placas. Mesa sem as 6 placas: usa a primeira completa. O
    /// feltro é um <see cref="FeltTheme"/> pintado com os tiles brancos da <see cref="FeltArt"/> (sem feltros, o tile
    /// liso em verde listrado). Mesa e feltro são os da partida (sorteados entre os liberados a cada partida nova, o
    /// Reiniciar mantém) ou, para testar, os escolhidos no Inspector.
    /// </summary>
    public sealed class TableArt : MonoBehaviour
    {
        private const string UpperHalfName = "Feltro (meia fileira)";
        private const string BuiltName = "Mesa montada";
        private const int FillOrder = -18;
        // Rede por cima da bola (7) e dos botões, embaixo da trave: a bola dentro do gol fica atrás da rede.
        private const int NetOrder = 12;
        private const int BoardOrder = -12;
        private const int BoardColorOrder = -11;
        private const int GoalOrder = 13;
        // Células de Fill além das placas: cobre o que a câmera mostra em volta da mesa (celular comprido inclusive).
        private const int FillRing = 4;

        [Tooltip("Para testar: esta mesa em vez da da partida. Vazio: a da partida (a mesma da Seleção de times).")]
        [SerializeField] private TableTheme tableOverride;
        [Tooltip("Para testar: a cor da mesa (traves e placas; 0 = a primeira). -1: a da partida.")]
        [SerializeField] private int goalColorOverride = -1;
        [Tooltip("Para testar: este feltro em vez do da partida. Vazio: o da partida (o mesmo da Seleção de times).")]
        [SerializeField] private FeltTheme feltOverride;
        [SerializeField] private Tilemap felt;
        [Tooltip("Primeira célula (canto de baixo à esquerda) e tamanho do feltro em tiles.")]
        [SerializeField] private Vector2Int feltOrigin = new(-4, -6);
        [SerializeField] private Vector2Int feltSize = new(7, 11);

        private Tilemap upperHalf;
        private bool small;
        private GameObject boardsBig, boardsSmall, goalsBig, goalsSmall;

        // Feltro de reserva (sem nenhum FeltTheme): o tile liso em verde listrado.
        private static readonly Color FallbackBright = new Color32(0x1A, 0x7A, 0x3E, 0xFF);
        private static readonly Color FallbackDark = new Color32(0x24, 0x52, 0x3B, 0xFF);

        public void Configure(Tilemap feltTilemap, Vector2Int origin, Vector2Int size)
        {
            felt = feltTilemap;
            feltOrigin = origin;
            feltSize = size;
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
            if (boardsBig != null) boardsBig.SetActive(!small);
            if (goalsBig != null) goalsBig.SetActive(!small);
            if (boardsSmall != null) boardsSmall.SetActive(small);
            if (goalsSmall != null) goalsSmall.SetActive(small);
        }

        /// <summary>Monta a mesa e pinta o feltro (de novo, se a mesa ou o feltro mudarem).</summary>
        public void Refresh()
        {
            RemoveOldTable();
            BuildBoards(CurrentTable());
            SetSmallGoals(small);
            PaintFelt();
        }

        /// <summary>Cena montada antes da limpeza da mesa antiga: tira a madeira, a moldura e os gols antigos.</summary>
        private void RemoveOldTable()
        {
            foreach (var oldName in new[] { "Madeira", "Moldura", "Gols grandes", "Gols pequenos" })
            {
                var old = transform.Find(oldName);
                if (old == null) continue;
                if (Application.isPlaying) Destroy(old.gameObject);
                else old.gameObject.SetActive(false);
            }
        }

        private TableTheme CurrentTable() => TableTheme.Complete(ChosenTable());

        private TableTheme ChosenTable()
        {
            if (tableOverride != null) return tableOverride;
            if (Application.isPlaying) return TableTheme.Current();
            foreach (var table in TableTheme.All())
                if (table.unlockedByDefault) return table;
            return null;
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
            boardsBig = CreatePieces(root, "Placas (gol grande)", table.Boards(false, color, FillRing));
            boardsSmall = CreatePieces(root, "Placas (gol pequeno)", table.Boards(true, color, FillRing));
            goalsBig = CreatePieces(root, "Traves grandes", table.Goals(false, color));
            goalsSmall = CreatePieces(root, "Traves pequenas", table.Goals(true, color));
        }

        private static int Order(TableTheme.PieceKind kind) => kind switch
        {
            TableTheme.PieceKind.Fill => FillOrder,
            TableTheme.PieceKind.BoardColor => BoardColorOrder,
            TableTheme.PieceKind.Net => NetOrder,
            TableTheme.PieceKind.Frame => GoalOrder,
            _ => BoardOrder,
        };

        private static GameObject CreatePieces(Transform parent, string groupName, System.Collections.Generic.List<TableTheme.Piece> pieces)
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
                renderer.color = piece.Tint;
                renderer.sortingOrder = Order(piece.Kind);
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
            float inset = table != null ? table.WallInset : FieldLayout.ClassicWallInset;
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
        /// Pinta cada fileira com a cor do feltro (de baixo para cima). O feltro vai uma célula além, por baixo das placas
        /// e dos gols, com a cor da fileira da ponta (o resto em volta é o Fill da mesa).
        /// </summary>
        public void PaintFelt()
        {
            if (felt == null) return;
            var theme = feltOverride != null ? feltOverride : Application.isPlaying ? FeltTheme.Current() : FirstUnlocked();
            var art = FeltArt.Load();
            felt.ClearAllTiles();
            ClearUpperHalf();
            int rows = feltSize.y;
            int left = -1, right = feltSize.x + 1, bottom = -1, top = rows + 1;
            bool themed = theme != null && art != null && art.Has(theme.tile);
            var tile = art == null ? null : themed ? art.GetTile(theme.tile) : art.GetTile(FeltTheme.TileKind.Liso);
            if (tile == null) return;
            // Fileira do meio clara, como no verde listrado.
            int middleRow = rows / 2;
            for (int row = bottom; row < top; row++)
            {
                int inside = Mathf.Clamp(row, 0, rows - 1);
                var color = themed ? theme.RowColor(inside, rows) : (inside - middleRow) % 2 == 0 ? FallbackBright : FallbackDark;
                for (int column = left; column < right; column++)
                    PaintCell(felt, new Vector3Int(feltOrigin.x + column, feltOrigin.y + row, 0), tile, color);
            }

            PaintMarks(themed ? theme.marks : Color.white);
            if (!themed || !theme.SplitsMiddleRow(rows)) return;
            var half = art.UpperHalfTile(theme.tile);
            var layer = UpperHalfLayer();
            if (half == null || layer == null) return;
            for (int column = left; column < right; column++)
                PaintCell(layer, new Vector3Int(feltOrigin.x + column, feltOrigin.y + rows / 2, 0), half, theme.secondary);
        }

        /// <summary>Marcações (o sprite branco "Marcações" da mesa) na cor do feltro.</summary>
        private void PaintMarks(Color color)
        {
            var marks = transform.Find("Marcações");
            if (marks != null && marks.TryGetComponent(out SpriteRenderer renderer)) renderer.color = color;
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
