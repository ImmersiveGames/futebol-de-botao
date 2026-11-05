using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Um padrão de camisa (item desbloqueável): uma linha da folha de botões (Base_Botao_32-Sheet) ou de goleiros
    /// (Goal_Keeper_sheet), com nome, preço e se já vem liberado. O corpo leva a cor principal e o padrão a segunda cor
    /// (<see cref="ShirtArt"/>). Os assets ficam em Resources/PadroesCamisa; o "Aplicar artes" cria um por linha que
    /// ainda não tenha. Sem nenhum asset de um tipo, todas as linhas da folha valem.
    /// </summary>
    [CreateAssetMenu(menuName = "Futebol de Botão/Padrão de camisa", fileName = "Camisa")]
    public sealed class ShirtPattern : ScriptableObject
    {
        public enum Kind { Botao, Goleiro }

        public const string ResourceFolder = "PadroesCamisa";

        [Tooltip("Código guardado no save. Não mude depois de publicado, mesmo trocando o nome.")]
        public string id = "camisa";
        [Tooltip("Nome que o jogador vê.")]
        public string displayName = "Camisa";
        public Kind kind;
        [Tooltip("Linha da folha (0 = a de cima).")]
        [Min(0)] public int row;
        [Tooltip("Preço em moedas.")]
        [Min(0)] public int price;
        [Tooltip("O jogador já tem desde o começo.")]
        public bool unlockedByDefault = true;

        private static ShirtPattern[] cached;

        /// <summary>Todos os padrões de Resources/PadroesCamisa, na ordem do id.</summary>
        public static ShirtPattern[] All()
        {
            if (cached != null) return cached;
            var list = new List<ShirtPattern>(Resources.LoadAll<ShirtPattern>(ResourceFolder));
            list.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            cached = list.ToArray();
            return cached;
        }

        /// <summary>Linhas liberadas do tipo, em ordem (sem nenhum asset do tipo: todas as <paramref name="rowCount"/>).</summary>
        public static List<int> UnlockedRows(Kind kind, int rowCount)
        {
            var rows = new List<int>();
            bool anyOfKind = false;
            foreach (var pattern in All())
            {
                if (pattern.kind != kind) continue;
                anyOfKind = true;
                if (pattern.unlockedByDefault && pattern.row < rowCount && !rows.Contains(pattern.row)) rows.Add(pattern.row);
            }
            if (!anyOfKind)
                for (int i = 0; i < rowCount; i++) rows.Add(i);
            if (rows.Count == 0) rows.Add(0);
            rows.Sort();
            return rows;
        }

        /// <summary>O liberado <paramref name="step"/> casas depois de <paramref name="current"/> (dá a volta).</summary>
        public static int Step(Kind kind, int current, int step, int rowCount)
        {
            var rows = UnlockedRows(kind, rowCount);
            int index = rows.IndexOf(current);
            if (index < 0) return rows[0];
            int count = rows.Count;
            return rows[((index + step) % count + count) % count];
        }

        /// <summary><paramref name="current"/> se estiver liberado; senão o primeiro liberado.</summary>
        public static int Allowed(Kind kind, int current, int rowCount)
        {
            var rows = UnlockedRows(kind, rowCount);
            return rows.Contains(current) ? current : rows[0];
        }

        /// <summary>Um liberado qualquer (a IA sorteia o dela).</summary>
        public static int RandomRow(Kind kind, int rowCount)
        {
            var rows = UnlockedRows(kind, rowCount);
            return rows[Random.Range(0, rows.Count)];
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => cached = null;

#if UNITY_EDITOR
        private void OnValidate() => cached = null;
#endif
    }
}
