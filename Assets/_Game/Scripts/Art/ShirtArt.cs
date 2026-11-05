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
    /// Camisas em pixel art desenhadas em cinza e tingidas no jogo: as cores de cada camada de um
    /// <see cref="ShirtPattern"/>. Corpo, contorno e sombra interna levam a cor principal (os tons de cinza da arte fazem
    /// o claro e o escuro); o padrão leva a segunda cor; a sombra é preta semitransparente.
    /// </summary>
    public static class ShirtArt
    {
        public const int LayerCount = 6;

        /// <summary>Brilho: 0 = cor principal (o cinza claro da arte faz o tom claro); 1 = branco puro.</summary>
        public const float ShineLightness = 0f;

        public static readonly Color ShadowColor = new(0f, 0f, 0f, 0.4f);

        /// <summary>
        /// A sombra do goleiro vem centrada no topo (8 px); no jogo ela desce os 4 px da frente (vai para o pé da caixa)
        /// e mais 2 px para baixo e 1 px para a direita, como a dos botões. Sem descer a frente, ela fica escondida embaixo dele.
        /// </summary>
        public static readonly Vector2 KeeperShadowOffset = new(1f / 32f, -6f / 32f);

        /// <summary>Transparência da luz (brilho) e da sombra interna do goleiro.</summary>
        public const float KeeperLightAlpha = 0.5f;

        /// <summary>Cor de cada camada com as cores do time (no goleiro, luz e sombra interna semitransparentes).</summary>
        public static Color Tint(ShirtLayer layer, Color primary, Color secondary, bool keeper = false)
        {
            var color = layer switch
            {
                ShirtLayer.Shadow => ShadowColor,
                ShirtLayer.Pattern => secondary,
                ShirtLayer.Shine => Color.Lerp(primary, Color.white, ShineLightness),
                _ => primary,
            };
            if (keeper && layer is ShirtLayer.Shine or ShirtLayer.InnerShade) color.a *= KeeperLightAlpha;
            return color;
        }

        /// <summary>Corpo, padrão e contorno (grupo "Gira" na cena); hoje nenhuma camada gira na tela.</summary>
        public static bool Spins(ShirtLayer layer) => layer is ShirtLayer.Body or ShirtLayer.Pattern or ShirtLayer.Outline;
    }
}
