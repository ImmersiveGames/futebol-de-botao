using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Uma bola (item desbloqueável): os quadros de rolagem, a luz e a sombra de uma folha como a Ball_SpriteSheet, uma
    /// cor que tinge os quadros (o branco da base vira a cor; os gomos escuros quase não mudam), preço e se já vem
    /// liberada. Os assets ficam em Resources/Bolas; a da partida é sorteada entre as liberadas a cada partida nova.
    /// </summary>
    [CreateAssetMenu(menuName = "Futebol de Botão/Bola", fileName = "Bola")]
    public sealed class BallTheme : ScriptableObject
    {
        public const string ResourceFolder = "Bolas";

        [Tooltip("Código guardado no save. Não mude depois de publicado, mesmo trocando o nome.")]
        public string id = "bola";
        [Tooltip("Nome que o jogador vê.")]
        public string displayName = "Bola";
        [Tooltip("Quadros de rolagem, em ordem (Ball_Frame_1, 2...).")]
        public Sprite[] frames = { };
        [Tooltip("Luz por cima (fica parada). Vazio: sem luz.")]
        public Sprite shine;
        [Tooltip("Sombra embaixo (fica parada). Vazio: a da bola padrão.")]
        public Sprite shadow;
        [Tooltip("Tinge os quadros de rolagem: o branco da arte vira esta cor.")]
        public Color color = Color.white;
        [Tooltip("Preço em moedas.")]
        [Min(0)] public int price;
        [Tooltip("O jogador já tem desde o começo.")]
        public bool unlockedByDefault = true;

        /// <summary>Código na lista de comprados do save.</summary>
        public string ShopKey => "bola:" + id;

        /// <summary>Vem liberada ou o jogador comprou.</summary>
        public bool Unlocked => unlockedByDefault || Shop.Owns(ShopKey);

        public bool HasFrames => frames != null && frames.Length > 0 && frames[0] != null;

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
        /// Bola da partida: a guardada na <see cref="MatchSession"/> ou, se nenhuma, uma sorteada entre as liberadas (e
        /// guardada, para a Seleção de times e a Partida mostrarem a mesma e o Reiniciar manter). Nula se não houver bolas.
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
            return ball;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => cached = null;

#if UNITY_EDITOR
        private void OnValidate() => cached = null;
#endif
    }
}
