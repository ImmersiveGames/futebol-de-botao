using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Um padrão de bola (item desbloqueável da Loja): os quadros de rolagem (sem cor), a camada de cor de cada quadro
    /// (o branco recebe a cor), a luz, a sombra, as cores que aceita (da <see cref="ColorPalette"/>, pelo nome), preço e
    /// se já vem liberada. A cor não se compra: o jogador escolhe no Meu time. Sem a camada de cor, os próprios quadros
    /// recebem a cor (o branco vira a cor). Os assets ficam em Resources/Bolas.
    /// </summary>
    [CreateAssetMenu(menuName = "Futebol de Botão/Bola", fileName = "Bola")]
    public sealed class BallTheme : ScriptableObject
    {
        public const string ResourceFolder = "Bolas";

        [Tooltip("Código guardado no save. Não mude depois de publicado, mesmo trocando o nome.")]
        public string id = "bola";
        [Tooltip("Nome que o jogador vê.")]
        public string displayName = "Bola";
        [Tooltip("Quadros de rolagem, em ordem (Ball_Frame_1, 2...): a parte que não muda de cor.")]
        public Sprite[] frames = { };
        [Tooltip("Camada de cor de cada quadro, na mesma ordem (em branco: recebe a cor). Vazio: os quadros de cima recebem a cor.")]
        public Sprite[] colorFrames = { };
        [Tooltip("Luz por cima (fica parada). Vazio: sem luz.")]
        public Sprite shine;
        [Tooltip("Sombra embaixo (fica parada). Vazio: a da bola padrão.")]
        public Sprite shadow;
        [Tooltip("Cores da Color Palette que esta bola aceita, pelo nome. Vazio: todas.")]
        [GoalColorName] public string[] colors = { };
        [Tooltip("Preço em moedas.")]
        [Min(0)] public int price;
        [Tooltip("O jogador já tem desde o começo.")]
        public bool unlockedByDefault = true;

        /// <summary>Código na lista de comprados do save.</summary>
        public string ShopKey => "bola:" + id;

        /// <summary>Vem liberada ou o jogador comprou.</summary>
        public bool Unlocked => unlockedByDefault || Shop.Owns(ShopKey);

        public bool HasFrames => frames != null && frames.Length > 0 && frames[0] != null;

        /// <summary>Tem a camada de cor (um recorte para cada quadro).</summary>
        public bool HasColorLayer => colorFrames != null && colorFrames.Length >= frames.Length && colorFrames.Length > 0 && colorFrames[0] != null;

        /// <summary>A camada de cor do quadro <paramref name="index"/>; nula sem camada de cor.</summary>
        public Sprite ColorFrame(int index) => HasColorLayer && index >= 0 && index < colorFrames.Length ? colorFrames[index] : null;

        /// <summary>Cor do quadro de rolagem: branco (fica como desenhado) quando há camada de cor; senão a própria cor.</summary>
        public Color FrameTint(int colorIndex) => HasColorLayer ? Color.white : Tint(colorIndex);

        /// <summary>A cor da paleta na posição <paramref name="colorIndex"/>; sem paleta, branco.</summary>
        public static Color Tint(int colorIndex)
        {
            var palette = ColorPalette.Load();
            return palette != null && palette.Count > 0 ? palette[colorIndex]?.color ?? Color.white : Color.white;
        }

        /// <summary>Posições na paleta das cores que a bola aceita (sem lista ou nenhuma batendo, todas).</summary>
        public List<int> AllowedColors()
        {
            var all = new List<int>();
            var allowed = new List<int>();
            var palette = ColorPalette.Load();
            if (palette == null) return all;
            for (int i = 0; i < palette.Count; i++)
            {
                if (palette.colors[i] == null) continue;
                all.Add(i);
                if (Accepts(palette.colors[i].name)) allowed.Add(i);
            }
            return allowed.Count > 0 ? allowed : all;
        }

        private bool Accepts(string colorName)
        {
            if (colors == null || string.IsNullOrWhiteSpace(colorName)) return false;
            foreach (var accepted in colors)
                if (string.Equals(accepted?.Trim(), colorName.Trim(), System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>A cor com esse nome, se a bola aceitar; senão a primeira que ela aceita.</summary>
        public int ColorByName(string colorName)
        {
            var allowed = AllowedColors();
            if (allowed.Count == 0) return 0;
            var palette = ColorPalette.Load();
            foreach (int index in allowed)
                if (!string.IsNullOrEmpty(colorName) && string.Equals(palette.colors[index].name, colorName, System.StringComparison.OrdinalIgnoreCase))
                    return index;
            return allowed[0];
        }

        /// <summary>A cor <paramref name="step"/> casas depois da atual, só entre as que a bola aceita.</summary>
        public int StepColor(int current, int step)
        {
            var allowed = AllowedColors();
            if (allowed.Count == 0) return 0;
            int position = allowed.IndexOf(current);
            if (position < 0) return allowed[0];
            int count = allowed.Count;
            return allowed[(((position + step) % count) + count) % count];
        }

        /// <summary>Uma cor sorteada entre as que a bola aceita.</summary>
        public int PickColor()
        {
            var allowed = AllowedColors();
            return allowed.Count > 0 ? allowed[Random.Range(0, allowed.Count)] : 0;
        }

        /// <summary>A cor aceita mais próxima da atual (a atual, se a bola aceita; senão a primeira).</summary>
        public int KeepColor(int current)
        {
            var allowed = AllowedColors();
            if (allowed.Count == 0) return 0;
            return allowed.Contains(current) ? current : allowed[0];
        }

        private static BallTheme[] cached;

        /// <summary>Todas as bolas de Resources/Bolas, na ordem do id.</summary>
        public static BallTheme[] All()
        {
            if (cached != null) return cached;
            var list = new List<BallTheme>(Resources.LoadAll<BallTheme>(ResourceFolder));
            list.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            cached = list.ToArray();
            return cached;
        }

        public static BallTheme Find(string ballId)
        {
            if (string.IsNullOrEmpty(ballId)) return null;
            foreach (var ball in All())
                if (ball.id == ballId) return ball;
            return null;
        }

        /// <summary>
        /// Bola da partida: a guardada na <see cref="MatchSession"/> ou, se nenhuma, uma sorteada entre as liberadas, com
        /// uma cor sorteada (e guardadas, para a Seleção de times e a Partida mostrarem a mesma e o Reiniciar manter).
        /// Nula se não houver bolas.
        /// </summary>
        public static BallTheme Current()
        {
            var ball = Find(MatchSession.BallId);
            if (ball != null) return ball;
            var pool = new List<BallTheme>();
            foreach (var item in All())
                if (item.Unlocked && item.HasFrames) pool.Add(item);
            if (pool.Count == 0)
                foreach (var item in All())
                    if (item.HasFrames) pool.Add(item);
            if (pool.Count == 0) return null;
            ball = pool[Random.Range(0, pool.Count)];
            MatchSession.BallId = ball.id;
            MatchSession.BallColor = ball.PickColor();
            return ball;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => cached = null;

#if UNITY_EDITOR
        private void OnValidate() => cached = null;
#endif
    }
}
