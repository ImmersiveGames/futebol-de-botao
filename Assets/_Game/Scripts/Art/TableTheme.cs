using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Uma mesa (item desbloqueável): a moldura com placas e as traves, com preço e se já vem liberada. A clássica é a
    /// da folha do campo (moldura e gols montados pelo "Aplicar artes"); as outras são montadas aqui com 6 tiles de
    /// placa desenhados para a parede de cima (as outras paredes giram e espelham) e as traves em várias cores, também
    /// do gol de cima (o de baixo gira 180°). Nas mesas de placas o feltro passa por baixo das placas e dos gols.
    /// Os assets ficam em Resources/Mesas; a da partida é sorteada entre as liberadas a cada partida nova.
    /// </summary>
    [CreateAssetMenu(menuName = "Futebol de Botão/Mesa", fileName = "Mesa")]
    public sealed class TableTheme : ScriptableObject
    {
        public const string ResourceFolder = "Mesas";

        /// <summary>Traves de uma cor: as peças dos gols grande e pequeno (o meio é o mesmo nos dois).</summary>
        [System.Serializable]
        public sealed class GoalFrames
        {
            public string name = "Branca";
            public Sprite bigLeft;
            public Sprite bigRight;
            public Sprite smallLeft;
            public Sprite smallRight;
            public Sprite center;

            public bool Complete => bigLeft != null && bigRight != null && smallLeft != null && smallRight != null && center != null;
        }

        /// <summary>Uma peça da moldura ou do gol: centro (em unidades, campo no 0,0), giro e espelho.</summary>
        public readonly struct Piece
        {
            public readonly Sprite Sprite;
            public readonly Vector2 Position;
            public readonly float Rotation;
            public readonly bool FlipX;

            public Piece(Sprite sprite, Vector2 position, float rotation, bool flipX)
            {
                Sprite = sprite;
                Position = position;
                Rotation = rotation;
                FlipX = flipX;
            }
        }

        [Tooltip("Código guardado no save. Não mude depois de publicado, mesmo trocando o nome.")]
        public string id = "mesa";
        [Tooltip("Nome que o jogador vê.")]
        public string displayName = "Mesa";
        [Tooltip("A mesa da folha do campo (Field_Sprites): moldura e gols já montados na cena; as placas daqui não são usadas.")]
        public bool classic;
        [Tooltip("A colisão das paredes fica estes pixels para dentro da linha do feltro (clássica: 3, a face inclinada).")]
        [Min(0f)] public float wallInsetPixels;

        [Header("Placas (desenhadas para a parede de cima)")]
        public Sprite corner;
        public Sprite afterCorner;
        public Sprite center;
        public Sprite beforeCorner;
        public Sprite bigGoalBoard;
        public Sprite smallGoalBoard;

        [Header("Traves (desenhadas para o gol de cima)")]
        public GoalFrames[] goalColors = { };

        [Tooltip("Preço em moedas.")]
        [Min(0)] public int price;
        [Tooltip("O jogador já tem desde o começo.")]
        public bool unlockedByDefault;
        [Tooltip("Miniatura na Loja. Vazio: a Loja monta com o canto, a placa e a trave.")]
        public Sprite thumbnail;

        /// <summary>Código na lista de comprados do save.</summary>
        public string ShopKey => "mesa:" + id;

        /// <summary>Vem liberada ou o jogador comprou.</summary>
        public bool Unlocked => unlockedByDefault || Shop.Owns(ShopKey);

        /// <summary>Mesa de placas com todos os recortes.</summary>
        public bool HasBoards => !classic && corner != null && afterCorner != null && center != null && beforeCorner != null
                                 && bigGoalBoard != null && smallGoalBoard != null;

        public float WallInset => wallInsetPixels / 32f;

        public GoalFrames Goal(int colorIndex)
        {
            if (goalColors == null || goalColors.Length == 0) return null;
            int count = goalColors.Length;
            var frames = goalColors[((colorIndex % count) + count) % count];
            return frames != null && frames.Complete ? frames : null;
        }

        /// <summary>
        /// Placas em volta do feltro (<see cref="FieldLayout.Width"/> x <see cref="FieldLayout.Height"/>, células de 1
        /// unidade). Paredes de cima e de baixo: canto, depois do canto, antes do canto e a placa até a trave, e o
        /// espelho do outro lado; a de baixo é a de cima girada 180°. Laterais: depois do canto, centros e antes do canto,
        /// giradas 90° (a da direita espelhada).
        /// </summary>
        public List<Piece> Boards(bool smallGoal)
        {
            var pieces = new List<Piece>();
            if (!HasBoards) return pieces;
            int halfColumns = Mathf.RoundToInt(FieldLayout.Width) / 2;
            int halfRows = Mathf.RoundToInt(FieldLayout.Height) / 2;
            int edgeX = halfColumns + 1;
            int edgeY = halfRows + 1;

            // Fileira de cima, metade da esquerda (de fora para dentro); o gol ocupa as 3 colunas do meio.
            var end = new List<(int x, Sprite sprite)> { (-edgeX, corner) };
            for (int x = -edgeX + 1; x <= -2; x++)
                end.Add((x, x == -edgeX + 1 ? afterCorner : x == -2 ? beforeCorner : center));
            end.Add((-1, smallGoal ? smallGoalBoard : bigGoalBoard));
            foreach (var (x, sprite) in end)
            {
                pieces.Add(new Piece(sprite, new Vector2(x, edgeY), 0f, false));
                pieces.Add(new Piece(sprite, new Vector2(-x, edgeY), 0f, true));
                pieces.Add(new Piece(sprite, new Vector2(-x, -edgeY), 180f, false));
                pieces.Add(new Piece(sprite, new Vector2(x, -edgeY), 180f, true));
            }

            // Laterais, de baixo para cima.
            for (int y = -halfRows; y <= halfRows; y++)
            {
                var sprite = y == -halfRows ? afterCorner : y == halfRows ? beforeCorner : center;
                pieces.Add(new Piece(sprite, new Vector2(-edgeX, y), 90f, false));
                pieces.Add(new Piece(sprite, new Vector2(edgeX, y), -90f, true));
            }
            return pieces;
        }

        /// <summary>As traves de uma cor nos dois gols: as do de cima como desenhadas, as do de baixo giradas 180°.</summary>
        public List<Piece> Goals(bool smallGoal, int colorIndex)
        {
            var pieces = new List<Piece>();
            var frames = Goal(colorIndex);
            if (frames == null) return pieces;
            float y = FieldLayout.HalfHeight + 0.5f;
            var row = new[] { smallGoal ? frames.smallLeft : frames.bigLeft, frames.center, smallGoal ? frames.smallRight : frames.bigRight };
            for (int i = 0; i < row.Length; i++)
            {
                pieces.Add(new Piece(row[i], new Vector2(i - 1, y), 0f, false));
                pieces.Add(new Piece(row[i], new Vector2(1 - i, -y), 180f, false));
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
            cached = list.ToArray();
            return cached;
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
        /// cor das traves junto), guardada para a Seleção de times e a Partida mostrarem a mesma e o Reiniciar manter.
        /// Nula se não houver mesas (fica a clássica da cena).
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
        /// Sorteia a cor das traves entre as que o feltro aceita (<see cref="FeltTheme.goalColors"/>, pelo nome); sem lista
        /// ou sem nenhuma dessas cores na mesa, entre todas.
        /// </summary>
        public int PickGoalColor(FeltTheme felt)
        {
            int count = goalColors?.Length ?? 0;
            if (count == 0) return 0;
            var allowed = new List<int>();
            if (felt != null && felt.goalColors != null && felt.goalColors.Length > 0)
                for (int i = 0; i < count; i++)
                    if (goalColors[i] != null && felt.AcceptsGoal(goalColors[i].name)) allowed.Add(i);
            return allowed.Count > 0 ? allowed[Random.Range(0, allowed.Count)] : Random.Range(0, count);
        }

        // Com o "Enter Play Mode" sem recarregar o domínio, a lista sobreviveria a mudanças nos assets.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => cached = null;

#if UNITY_EDITOR
        private void OnValidate() => cached = null;
#endif
    }
}
