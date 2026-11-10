using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Um padrão de camisa de botão ou de goleiro (item desbloqueável), com os próprios recortes: sombra, corpo, padrão,
    /// contorno (só botão), sombra interna e brilho, de qualquer folha e com qualquer nome. Desenhados em cinza e
    /// tingidos no jogo (<see cref="ShirtArt.Tint"/>): o corpo leva a cor principal e o padrão a segunda cor. As setas da
    /// Seleção de times seguem a <see cref="order"/>; a escolha do jogador é guardada pelo <see cref="id"/>. Os assets
    /// ficam em Resources/PadroesCamisa.
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
        [Tooltip("Posição nas setas da Seleção de times (menor primeiro; empate pelo id).")]
        public int order;
        [Tooltip("Preço em moedas.")]
        [Min(0)] public int price;
        [Tooltip("O jogador já tem desde o começo.")]
        public bool unlockedByDefault = true;

        [Header("Recortes (cinza; o jogo tinge)")]
        [Tooltip("Preta: o jogo deixa semitransparente.")]
        public Sprite shadow;
        [Tooltip("Cor principal.")]
        public Sprite body;
        [Tooltip("Segunda cor.")]
        public Sprite pattern;
        [Tooltip("Cor principal (o goleiro não tem).")]
        public Sprite outline;
        [Tooltip("Cor principal.")]
        public Sprite innerShade;
        [Tooltip("Cor principal.")]
        public Sprite shine;

        [Tooltip("Linha da folha antiga: só para converter saves e preencher os recortes uma vez. Não precisa mexer.")]
        [HideInInspector] public int row = -1;

        public bool Usable => body != null;

        /// <summary>Código na lista de comprados do save.</summary>
        public string ShopKey => "camisa:" + id;

        /// <summary>Vem liberado ou o jogador comprou.</summary>
        public bool Unlocked => unlockedByDefault || Shop.Owns(ShopKey);

        public Sprite Get(ShirtLayer layer) => layer switch
        {
            ShirtLayer.Shadow => shadow,
            ShirtLayer.Body => body,
            ShirtLayer.Pattern => pattern,
            ShirtLayer.Outline => outline,
            ShirtLayer.InnerShade => innerShade,
            _ => shine,
        };

        private static ShirtPattern[] cached;

        /// <summary>Todos os padrões de Resources/PadroesCamisa, na ordem das setas.</summary>
        public static ShirtPattern[] All()
        {
            if (cached != null) return cached;
            var list = new List<ShirtPattern>(Resources.LoadAll<ShirtPattern>(ResourceFolder));
            list.Sort((a, b) => a.order != b.order ? a.order.CompareTo(b.order) : string.CompareOrdinal(a.id, b.id));
            cached = list.ToArray();
            return cached;
        }

        /// <summary>Padrões do tipo com recortes, liberados (ou todos), na ordem das setas.</summary>
        public static List<ShirtPattern> Of(Kind kind, bool unlockedOnly = true)
        {
            var list = new List<ShirtPattern>();
            foreach (var item in All())
                if (item.kind == kind && item.Usable && (!unlockedOnly || item.Unlocked)) list.Add(item);
            // Nenhum liberado: todos (para nunca ficar sem camisa).
            if (list.Count == 0 && unlockedOnly) return Of(kind, false);
            return list;
        }

        public static bool Any(Kind kind) => Of(kind).Count > 0;

        /// <summary>O padrão do id, se for do tipo e tiver recortes (liberado ou não); senão nulo.</summary>
        public static ShirtPattern FindOf(Kind kind, string patternId)
        {
            var item = Find(patternId);
            return item != null && item.kind == kind && item.Usable ? item : null;
        }

        public static ShirtPattern Find(string patternId)
        {
            if (string.IsNullOrEmpty(patternId)) return null;
            foreach (var item in All())
                if (item.id == patternId) return item;
            return null;
        }

        /// <summary>O padrão do id, se for do tipo e estiver liberado; senão o primeiro liberado (nulo sem nenhum).</summary>
        public static ShirtPattern Resolve(Kind kind, string patternId)
        {
            var list = Of(kind);
            foreach (var item in list)
                if (item.id == patternId) return item;
            return list.Count > 0 ? list[0] : null;
        }

        /// <summary>O id do liberado <paramref name="step"/> casas depois de <paramref name="currentId"/> (dá a volta).</summary>
        public static string Step(Kind kind, string currentId, int step)
        {
            var list = Of(kind);
            if (list.Count == 0) return currentId;
            int index = list.FindIndex(item => item.id == currentId);
            if (index < 0) return list[0].id;
            int count = list.Count;
            return list[((index + step) % count + count) % count].id;
        }

        /// <summary>Um liberado qualquer (a IA sorteia o dela); nulo sem nenhum.</summary>
        public static string RandomId(Kind kind)
        {
            var list = Of(kind);
            return list.Count > 0 ? list[Random.Range(0, list.Count)].id : null;
        }

        /// <summary>Save antigo (posição na folha): o padrão que veio daquela linha, ou nulo.</summary>
        public static string FromLegacyRow(Kind kind, int legacyRow)
        {
            foreach (var item in All())
                if (item.kind == kind && item.row == legacyRow) return item.id;
            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => cached = null;

#if UNITY_EDITOR
        private void OnValidate() => cached = null;
#endif
    }

    /// <summary>Campo de texto com o id de um padrão de camisa: o Inspector mostra a lista dos padrões do tipo.</summary>
    public sealed class ShirtPatternIdAttribute : PropertyAttribute
    {
        public readonly ShirtPattern.Kind Kind;
        public ShirtPatternIdAttribute(ShirtPattern.Kind kind) => Kind = kind;
    }
}
