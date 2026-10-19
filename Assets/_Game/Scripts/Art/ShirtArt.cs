using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>Camadas da camisa, na ordem de desenho (de baixo para cima).</summary>
    public enum ShirtLayer
    {
        Shadow,
        Body,
        Pattern,
        Outline,
        InnerShade,
        Shine,
    }

    /// <summary>
    /// Camisas em pixel art desenhadas em cinza e tingidas no jogo: botões (Base_Botao_32-Sheet) e goleiros
    /// (Goal_Keeper_sheet), uma linha por padrão e uma coluna por camada. Corpo, contorno e sombra interna levam a cor
    /// principal (os tons de cinza da arte fazem o claro e o escuro); o padrão leva a segunda cor; a sombra é preta semitransparente.
    /// Montado por "Futebol de Botão/Aplicar artes" em Resources/Camisas.
    /// </summary>
    public sealed class ShirtArt : ScriptableObject
    {
        public const string ResourcePath = "Camisas";
        public const int LayerCount = 6;

        /// <summary>Brilho: 0 = cor principal (o cinza claro da arte faz o tom claro); 1 = branco puro.</summary>
        public const float ShineLightness = 0f;

        public static readonly Color ShadowColor = new(0f, 0f, 0f, 0.4f);

        /// <summary>
        /// A sombra do goleiro vem centrada no topo (8 px); no jogo ela desce os 4 px da frente (vai para o pé da caixa)
        /// e mais 2 px para baixo e 1 px para a direita, como a dos botões. Sem descer a frente, ela fica escondida embaixo dele.
        /// </summary>
        public static readonly Vector2 KeeperShadowOffset = new(1f / 32f, -6f / 32f);

        [System.Serializable]
        public sealed class Sheet
        {
            public Sprite[] shadow = new Sprite[0];
            public Sprite[] body = new Sprite[0];
            public Sprite[] pattern = new Sprite[0];
            public Sprite[] outline = new Sprite[0];
            public Sprite[] innerShade = new Sprite[0];
            public Sprite[] shine = new Sprite[0];

            public int PatternCount => pattern != null ? pattern.Length : 0;

            public Sprite[] Layer(ShirtLayer layer) => layer switch
            {
                ShirtLayer.Shadow => shadow,
                ShirtLayer.Body => body,
                ShirtLayer.Pattern => pattern,
                ShirtLayer.Outline => outline,
                ShirtLayer.InnerShade => innerShade,
                _ => shine,
            };

            /// <summary>Recorte da camada no padrão <paramref name="index"/> (dá a volta se passar do fim).</summary>
            public Sprite Get(ShirtLayer layer, int index)
            {
                var sprites = Layer(layer);
                if (sprites == null || sprites.Length == 0) return null;
                int count = sprites.Length;
                return sprites[((index % count) + count) % count];
            }
        }

        public Sheet disc = new();
        public Sheet keeper = new();

        private static ShirtArt cached;

        /// <summary>A arte das camisas, ou nulo se o "Aplicar artes" ainda não montou.</summary>
        public static ShirtArt Load()
        {
            if (cached == null) cached = Resources.Load<ShirtArt>(ResourcePath);
            return cached;
        }

        /// <summary>Cor de cada camada com as cores do time.</summary>
        public static Color Tint(ShirtLayer layer, Color primary, Color secondary) => layer switch
        {
            ShirtLayer.Shadow => ShadowColor,
            ShirtLayer.Pattern => secondary,
            ShirtLayer.Shine => Color.Lerp(primary, Color.white, ShineLightness),
            _ => primary,
        };

        /// <summary>Corpo, padrão e contorno (grupo "Gira" na cena); hoje nenhuma camada gira na tela.</summary>
        public static bool Spins(ShirtLayer layer) => layer is ShirtLayer.Body or ShirtLayer.Pattern or ShirtLayer.Outline;
    }
}
