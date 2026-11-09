using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Um feltro da mesa (item desbloqueável): padrão, tile da folha de feltros (pintado com as cores do item), cores,
    /// preço e se já vem liberado. Os assets ficam em Resources/Feltros; o "Aplicar artes" cria os de teste se a pasta
    /// estiver vazia. O feltro da partida é sorteado entre os liberados a cada partida nova (<see cref="Current"/>).
    /// </summary>
    [CreateAssetMenu(menuName = "Futebol de Botão/Feltro", fileName = "Feltro")]
    public sealed class FeltTheme : ScriptableObject
    {
        public enum Pattern { Liso, Listrado, MeioAMeio }
        public enum TileKind { Liso, Gradiente, Textura }

        public const string ResourceFolder = "Feltros";

        [Tooltip("Código guardado no save. Não mude depois de publicado, mesmo trocando o nome.")]
        public string id = "feltro";
        [Tooltip("Nome que o jogador vê.")]
        public string displayName = "Feltro";
        [Tooltip("Liso: só a cor primária. Listrado: faixas alternando primária e secundária (a do meio do campo é a " +
                 "primária). Meio a meio: primária embaixo, secundária em cima.")]
        public Pattern pattern = Pattern.Listrado;
        [Tooltip("Tile da folha de feltros pintado com as cores.")]
        public TileKind tile = TileKind.Liso;
        public Color primary = new(0.102f, 0.478f, 0.243f);
        public Color secondary = new(0.141f, 0.322f, 0.231f);
        [Tooltip("Cor das marcações do campo (linhas, círculo e áreas) com este feltro.")]
        public Color marks = Color.white;
        [Tooltip("Altura de cada faixa do listrado, em tiles.")]
        [Min(1)] public int stripeRows = 1;
        [Tooltip("Preço em moedas.")]
        [Min(0)] public int price;
        [Tooltip("O jogador já tem desde o começo.")]
        public bool unlockedByDefault;
        [Tooltip("Cores de trave que combinam com este feltro, pelo nome da cor na mesa (ex.: Branca). Vazio: qualquer uma.")]
        public string[] goalColors = { };
        [Tooltip("Miniatura na Loja. Vazio: a Loja mostra as faixas de cor.")]
        public Sprite thumbnail;

        /// <summary>Código na lista de comprados do save.</summary>
        public string ShopKey => "feltro:" + id;

        /// <summary>Vem liberado ou o jogador comprou.</summary>
        public bool Unlocked => unlockedByDefault || Shop.Owns(ShopKey);

        /// <summary>A cor de trave (nome na mesa) está na lista deste feltro (sem diferenciar maiúsculas e espaços).</summary>
        public bool AcceptsGoal(string colorName)
        {
            if (string.IsNullOrWhiteSpace(colorName) || goalColors == null) return false;
            foreach (var accepted in goalColors)
                if (!string.IsNullOrWhiteSpace(accepted) && string.Equals(accepted.Trim(), colorName.Trim(), System.StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        /// <summary>Cor da fileira <paramref name="row"/> (0 = de baixo) de um feltro com <paramref name="rows"/> fileiras.</summary>
        public Color RowColor(int row, int rows)
        {
            int middle = rows / 2;
            switch (pattern)
            {
                case Pattern.Listrado:
                    int width = Mathf.Max(1, stripeRows);
                    // A faixa do meio fica centrada na fileira do meio; as outras se repetem para cima e para baixo.
                    int band = (Mathf.Abs(row - middle) + width / 2) / width;
                    return band % 2 == 0 ? primary : secondary;
                case Pattern.MeioAMeio:
                    return row <= middle ? primary : secondary;
                default:
                    return primary;
            }
        }

        /// <summary>
        /// No Meio a meio com número ímpar de fileiras, a linha do meio passa no meio da fileira central: a metade de
        /// cima dela leva a cor secundária.
        /// </summary>
        public bool SplitsMiddleRow(int rows) => pattern == Pattern.MeioAMeio && rows % 2 == 1;

        private static FeltTheme[] cached;

        /// <summary>Todos os feltros de Resources/Feltros, na ordem do id.</summary>
        public static FeltTheme[] All()
        {
            if (cached != null) return cached;
            var list = new List<FeltTheme>(Resources.LoadAll<FeltTheme>(ResourceFolder));
            list.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            cached = list.ToArray();
            return cached;
        }

        public static FeltTheme Find(string themeId)
        {
            if (string.IsNullOrEmpty(themeId)) return null;
            foreach (var theme in All())
                if (theme.id == themeId) return theme;
            return null;
        }

        /// <summary>Um dos liberados (sem nenhum liberado, qualquer um); nulo se não houver feltros.</summary>
        public static FeltTheme RandomUnlocked()
        {
            var all = All();
            if (all.Length == 0) return null;
            var unlocked = new List<FeltTheme>();
            foreach (var theme in all)
                if (theme.Unlocked) unlocked.Add(theme);
            var pool = unlocked.Count > 0 ? unlocked : new List<FeltTheme>(all);
            return pool[Random.Range(0, pool.Count)];
        }

        /// <summary>
        /// Feltro da partida: o guardado na <see cref="MatchSession"/> ou, se nenhum, um sorteado entre os liberados (e
        /// guardado, para a Seleção de times e a Partida mostrarem o mesmo e o Reiniciar manter).
        /// </summary>
        public static FeltTheme Current()
        {
            var theme = Find(MatchSession.FeltId);
            if (theme != null) return theme;
            theme = RandomUnlocked();
            MatchSession.FeltId = theme != null ? theme.id : null;
            return theme;
        }

        // Com o "Enter Play Mode" sem recarregar o domínio, a lista sobreviveria a mudanças nos assets.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => cached = null;

#if UNITY_EDITOR
        private void OnValidate() => cached = null;
#endif
    }
}
