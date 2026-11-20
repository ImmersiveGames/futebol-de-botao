using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Uma mesa (item desbloqueável): placas em volta do feltro, a trave (<see cref="GoalFrameSet"/>), preço e se já vem
    /// liberada. As cores que o jogador escolhe vêm da <see cref="ColorPalette"/> (a mesma do time), filtradas pelo feltro. As 6 peças de placa são desenhadas para a parede de cima (as
    /// outras paredes giram e espelham): canto, parede, a parede ao lado do gol (grande e pequeno) e o canto do fundo do
    /// gol, uma fileira acima, com a parede normal entre os dois cantos. Cada peça pode ter uma camada de cor (desenho
    /// branco que o jogo pinta com a cor escolhida, por cima da peça); a trave inteira também é pintada. O Fill, se
    /// houver, cobre o lado de fora da mesa em vez do feltro. Os assets ficam em Resources/Mesas.
    /// </summary>
    [CreateAssetMenu(menuName = "Futebol de Botão/Mesa", fileName = "Mesa")]
    public sealed class TableTheme : ScriptableObject
    {
        public const string ResourceFolder = "Mesas";

        /// <summary>Uma peça de placa e a camada dela que recebe a cor (opcional).</summary>
        [System.Serializable]
        public sealed class BoardPiece
        {
            public Sprite sprite;
            [Tooltip("Desenho branco por cima da peça, pintado com a cor escolhida. Vazio: a peça não muda de cor.")]
            public Sprite color;
        }

        /// <summary>O que a peça é: decide a ordem de desenho (Fill embaixo, rede por cima da bola, trave na frente).</summary>
        public enum PieceKind
        {
            Fill,
            Board,
            BoardColor,
            Net,
            Frame,
        }

        /// <summary>Uma peça da mesa ou do gol: centro (em unidades, campo no 0,0), giro, espelho e cor.</summary>
        public readonly struct Piece
        {
            public readonly Sprite Sprite;
            public readonly Vector2 Position;
            public readonly float Rotation;
            public readonly bool FlipX;
            public readonly Color Tint;
            public readonly PieceKind Kind;

            public Piece(Sprite sprite, Vector2 position, float rotation, bool flipX, Color tint, PieceKind kind)
            {
                Sprite = sprite;
                Position = position;
                Rotation = rotation;
                FlipX = flipX;
                Tint = tint;
                Kind = kind;
            }
        }

        [Tooltip("Código guardado no save. Não mude depois de publicado, mesmo trocando o nome.")]
        public string id = "mesa";
        [Tooltip("Nome que o jogador vê.")]
        public string displayName = "Mesa";
        [Tooltip("A colisão das paredes fica estes pixels para fora da linha do feltro (a face de dentro da parede).")]
        [Min(0f)] public float wallInsetPixels;

        [Header("Placas (desenhadas para a parede de cima)")]
        [Tooltip("Os cantos da mesa.")]
        public BoardPiece corner = new();
        [Tooltip("A parede: fundo e laterais (gira e espelha).")]
        public BoardPiece wall = new();
        [Tooltip("A parede ao lado do gol grande, que faz o encaixe dele.")]
        public BoardPiece bigGoal = new();
        [Tooltip("O canto do fundo do gol grande, uma fileira acima da parede do gol.")]
        public BoardPiece bigGoalCorner = new();
        [Tooltip("A parede ao lado do gol pequeno (sem goleiro).")]
        public BoardPiece smallGoal = new();
        [Tooltip("O canto do fundo do gol pequeno.")]
        public BoardPiece smallGoalCorner = new();
        [Tooltip("Opcional: preenche o lado de fora da mesa no lugar do feltro.")]
        public Sprite fill;

        [Header("Trave")]
        [Tooltip("A trave e a rede dos dois gols (o asset pode ser o mesmo em várias mesas). As cores vêm da Color Palette.")]
        public GoalFrameSet goal;

        [Header("Loja")]
        [Tooltip("Preço em moedas.")]
        [Min(0)] public int price;
        [Tooltip("O jogador já tem desde o começo.")]
        public bool unlockedByDefault;
        [Tooltip("Miniatura na Loja. Vazio: a Loja monta com o canto, a parede e a parede do gol.")]
        public Sprite thumbnail;

        /// <summary>Código na lista de comprados do save.</summary>
        public string ShopKey => "mesa:" + id;

        /// <summary>Vem liberada ou o jogador comprou.</summary>
        public bool Unlocked => unlockedByDefault || Shop.Owns(ShopKey);

        /// <summary>Mesa com todas as peças de placa (sem elas, fica a moldura antiga da cena).</summary>
        public bool HasBoards => Has(corner) && Has(wall) && Has(bigGoal) && Has(bigGoalCorner) && Has(smallGoal) && Has(smallGoalCorner);

        public float WallInset => wallInsetPixels / 32f;

        private static bool Has(BoardPiece piece) => piece != null && piece.sprite != null;

        /// <summary>A cor da <see cref="ColorPalette"/> na posição <paramref name="colorIndex"/> (dá a volta); nula se a paleta estiver vazia.</summary>
        public ColorPalette.Entry Goal(int colorIndex)
        {
            var palette = ColorPalette.Load();
            return palette.Count > 0 ? palette[colorIndex] : null;
        }

        /// <summary>A cor para pintar: a escolhida ou, sem paleta, branco (o desenho como está).</summary>
        public Color Tint(int colorIndex) => Goal(colorIndex)?.color ?? Color.white;

        /// <summary>
        /// Placas em volta do feltro (<see cref="FieldLayout.Width"/> x <see cref="FieldLayout.Height"/>, células de 1
        /// unidade). Fundo de cima, da esquerda para o meio: canto, paredes e a parede do gol; uma fileira acima, o canto
        /// do fundo do gol e a parede entre os dois cantos; o outro lado espelhado e o fundo de baixo girado 180°.
        /// Laterais: paredes giradas 90° (a da direita espelhada). Cada peça vem seguida da camada de cor; o Fill vem
        /// antes, nas células de fora até <paramref name="fillRing"/> células além das placas.
        /// </summary>
        public List<Piece> Boards(bool smallGoal, int colorIndex, int fillRing = 1)
        {
            var pieces = new List<Piece>();
            if (!HasBoards) return pieces;
            int halfColumns = Mathf.RoundToInt(FieldLayout.Width) / 2;
            int halfRows = Mathf.RoundToInt(FieldLayout.Height) / 2;
            int edgeX = halfColumns + 1;
            int edgeY = halfRows + 1;
            var goalWall = smallGoal ? this.smallGoal : bigGoal;
            var goalCorner = smallGoal ? smallGoalCorner : bigGoalCorner;

            var placed = new List<(BoardPiece piece, Vector2Int cell, float rotation, bool flip)>();
            void End(BoardPiece piece, int x, int y)
            {
                placed.Add((piece, new Vector2Int(x, y), 0f, false));
                placed.Add((piece, new Vector2Int(-x, y), 0f, true));
                placed.Add((piece, new Vector2Int(-x, -y), 180f, false));
                placed.Add((piece, new Vector2Int(x, -y), 180f, true));
            }

            // Fundos: da ponta até a parede do gol; o gol ocupa a coluna do meio e entra nas duas do lado.
            End(corner, -edgeX, edgeY);
            for (int x = -edgeX + 1; x <= -2; x++) End(wall, x, edgeY);
            End(goalWall, -1, edgeY);
            // Atrás do gol: os dois cantos e a parede entre eles (a do meio entra uma vez só de cada lado).
            End(goalCorner, -1, edgeY + 1);
            placed.Add((wall, new Vector2Int(0, edgeY + 1), 0f, false));
            placed.Add((wall, new Vector2Int(0, -edgeY - 1), 180f, false));

            // Laterais, de baixo para cima.
            for (int y = -halfRows; y <= halfRows; y++)
            {
                placed.Add((wall, new Vector2Int(-edgeX, y), 90f, false));
                placed.Add((wall, new Vector2Int(edgeX, y), -90f, true));
            }

            if (fill != null && fillRing > 0)
            {
                var taken = new HashSet<Vector2Int>();
                foreach (var item in placed) taken.Add(item.cell);
                for (int x = -edgeX - fillRing; x <= edgeX + fillRing; x++)
                    for (int y = -edgeY - 1 - fillRing; y <= edgeY + 1 + fillRing; y++)
                    {
                        var cell = new Vector2Int(x, y);
                        bool field = Mathf.Abs(x) <= halfColumns && Mathf.Abs(y) <= halfRows;
                        bool goalMouth = x == 0 && Mathf.Abs(y) == edgeY;
                        if (field || goalMouth || taken.Contains(cell)) continue;
                        pieces.Add(new Piece(fill, cell, 0f, false, Color.white, PieceKind.Fill));
                    }
            }

            var tint = Tint(colorIndex);
            foreach (var (piece, cell, rotation, flip) in placed)
            {
                pieces.Add(new Piece(piece.sprite, cell, rotation, flip, Color.white, PieceKind.Board));
                if (piece.color != null) pieces.Add(new Piece(piece.color, cell, rotation, flip, tint, PieceKind.BoardColor));
            }
            return pieces;
        }

        /// <summary>A rede e a trave (pintada com a cor) nos dois gols: as do de cima como desenhadas, as do de baixo giradas 180°.</summary>
        public List<Piece> Goals(bool smallGoal, int colorIndex)
        {
            var pieces = new List<Piece>();
            if (goal == null || !goal.Complete) return pieces;
            float y = FieldLayout.HalfHeight + 0.5f;
            var tint = Tint(colorIndex);
            var nets = new[] { smallGoal ? goal.smallNetLeft : goal.bigNetLeft, goal.centerNet, smallGoal ? goal.smallNetRight : goal.bigNetRight };
            var frames = new[] { smallGoal ? goal.smallLeft : goal.bigLeft, goal.center, smallGoal ? goal.smallRight : goal.bigRight };
            foreach (var (row, kind, color) in new[] { (nets, PieceKind.Net, Color.white), (frames, PieceKind.Frame, tint) })
                for (int i = 0; i < row.Length; i++)
                {
                    if (row[i] == null) continue;
                    pieces.Add(new Piece(row[i], new Vector2(i - 1, y), 0f, false, color, kind));
                    pieces.Add(new Piece(row[i], new Vector2(1 - i, -y), 180f, false, color, kind));
                }
            return pieces;
        }

        private static TableTheme[] cached;

        /// <summary>Todas as mesas de Resources/Mesas, na ordem do id.</summary>
        public static TableTheme[] All()
        {
            if (cached != null) return cached;
            var list = new List<TableTheme>(Resources.LoadAll<TableTheme>(ResourceFolder));
            list.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            // Id repetido: a loja e a escolha da mesa confundem as duas (comprar uma libera a outra).
            for (int i = 1; i < list.Count; i++)
                if (list[i].id == list[i - 1].id)
                    Debug.LogWarning($"[Mesas] '{list[i - 1].name}' e '{list[i].name}' têm o mesmo id '{list[i].id}'. Cada mesa precisa de um id próprio.");
            cached = list.ToArray();
            return cached;
        }

        /// <summary>
        /// A própria mesa, se tem as 6 placas; senão a primeira completa (mesa montada pela metade não aparece sem
        /// placas). Nula se nenhuma estiver completa.
        /// </summary>
        public static TableTheme Complete(TableTheme table)
        {
            if (table != null && table.HasBoards) return table;
            foreach (var item in All())
                if (item.HasBoards) return item;
            return null;
        }

        public static TableTheme Find(string tableId)
        {
            if (string.IsNullOrEmpty(tableId)) return null;
            foreach (var table in All())
                if (table.id == tableId) return table;
            return null;
        }

        /// <summary>
        /// Mesa da partida: a guardada na <see cref="MatchSession"/> ou, se nenhuma, uma sorteada entre as liberadas (e a
        /// cor junto), guardada para a Seleção de times e a Partida mostrarem a mesma e o Reiniciar manter.
        /// Nula se não houver mesas.
        /// </summary>
        public static TableTheme Current()
        {
            var table = Find(MatchSession.TableId);
            if (table != null) return table;
            var all = All();
            if (all.Length == 0) return null;
            var unlocked = new List<TableTheme>();
            foreach (var item in all)
                if (item.Unlocked) unlocked.Add(item);
            var pool = unlocked.Count > 0 ? unlocked : new List<TableTheme>(all);
            table = pool[Random.Range(0, pool.Count)];
            MatchSession.TableId = table.id;
            MatchSession.GoalColor = table.PickGoalColor(FeltTheme.Current());
            return table;
        }

        /// <summary>
        /// Sorteia a cor (traves e placas) entre as que o feltro aceita (<see cref="FeltTheme.goalColors"/>, pelo nome); sem lista
        /// ou sem nenhuma dessas cores na mesa, entre todas.
        /// </summary>
        public int PickGoalColor(FeltTheme felt)
        {
            var allowed = AllowedGoalColors(felt);
            return allowed.Count > 0 ? allowed[Random.Range(0, allowed.Count)] : 0;
        }

        /// <summary>
        /// Posições das cores que o feltro aceita (<see cref="FeltTheme.goalColors"/>, pelo nome); se o feltro não tiver
        /// lista ou nenhuma bater, todas.
        /// </summary>
        public List<int> AllowedGoalColors(FeltTheme felt)
        {
            var all = new List<int>();
            var allowed = new List<int>();
            var palette = ColorPalette.Load();
            for (int i = 0; i < palette.Count; i++)
            {
                if (palette.colors[i] == null) continue;
                all.Add(i);
                if (felt != null && felt.AcceptsGoal(palette.colors[i].name)) allowed.Add(i);
            }
            return allowed.Count > 0 ? allowed : all;
        }

        // Com o "Enter Play Mode" sem recarregar o domínio, a lista sobreviveria a mudanças nos assets.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => cached = null;

#if UNITY_EDITOR
        private void OnValidate() => cached = null;
#endif
    }
}
