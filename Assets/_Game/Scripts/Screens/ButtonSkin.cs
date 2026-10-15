using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>Cores da folha de botões (uma linha da folha para cada).</summary>
    public enum ButtonStyle
    {
        /// <summary>Botões comuns: setas, cartões, Pausa.</summary>
        Green,
        /// <summary>Voltar, Sair, Reiniciar.</summary>
        Red,
        /// <summary>Desligado (não dá para tocar).</summary>
        Brown,
        /// <summary>Ação principal: Jogar, Avançar, Pronto, Pro Gol!.</summary>
        Yellow,
    }

    /// <summary>
    /// Veste um <see cref="Button"/> com a moldura 9-slice de Resources/Botoes/Buttons_Sprites (32x32, borda de 10 px):
    /// troca para a versão apertada no toque, usa a marrom quando o botão está desligado e acerta a cor do texto.
    /// Sem a folha (ou sem os recortes), não mexe em nada e devolve falso: quem chamou mantém a cor lisa de antes.
    /// Os recortes são feitos pelo menu "Criar telas".
    /// </summary>
    public static class ButtonSkin
    {
        public const string SheetResource = "Botoes/Buttons_Sprites";
        /// <summary>Nome do recorte de cada linha da folha, de cima para baixo (mesma ordem de <see cref="ButtonStyle"/>).</summary>
        public static readonly string[] RowNames = { "Verde", "Vermelho", "Marrom", "Amarelo" };
        public const string PressedSuffix = "_Apertado";
        public const int TileSize = 32;
        public const int BorderPixels = 10;

        // Canvas.referencePixelsPerUnit padrão: a borda de 10 px a 32 PPU vira 31,25 unidades da UI.
        private const float ReferencePixelsPerUnit = 100f;
        // Em botão baixo (setas, faixa da HUD) a borda encolhe para ocupar no máximo 40% de cada lado.
        private const float MaxBorderShare = 0.4f;

        private static readonly Color LightText = Color.white;
        private static readonly Color DarkText = new(0.18f, 0.12f, 0.05f);

        private static Dictionary<string, Sprite> sprites;

        public static Color TextColor(ButtonStyle style) => style == ButtonStyle.Yellow ? DarkText : LightText;

        /// <summary>O botão já foi vestido com a folha.</summary>
        public static bool IsSkinned(Button button) => button != null && button.TryGetComponent<ButtonSkinText>(out _);

        public static bool Apply(Button button, ButtonStyle style)
        {
            if (button == null || button.targetGraphic is not Image image) return false;
            var normal = Find(style, false);
            var pressed = Find(style, true);
            var disabled = Find(ButtonStyle.Brown, false);
            if (normal == null || pressed == null || disabled == null) return false;

            image.sprite = normal;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.color = Color.white;
            var size = image.rectTransform.rect.size;
            float minSide = Mathf.Min(size.x, size.y);
            float border = BorderPixels * ReferencePixelsPerUnit / normal.pixelsPerUnit;
            image.pixelsPerUnitMultiplier = minSide > 0f ? Mathf.Max(1f, border / (MaxBorderShare * minSide)) : 1f;

            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState { pressedSprite = pressed, disabledSprite = disabled };

            var label = button.GetComponentInChildren<Text>(true);
            if (!button.TryGetComponent<ButtonSkinText>(out var text)) text = button.gameObject.AddComponent<ButtonSkinText>();
            text.Configure(button, label, TextColor(style));
            return true;
        }

        private static Sprite Find(ButtonStyle style, bool pressed)
        {
            string name = RowNames[(int)style] + (pressed ? PressedSuffix : string.Empty);
            if (sprites != null && sprites.TryGetValue(name, out var cached) && cached != null) return cached;

            // Primeira vez, ou o editor descarregou os sprites ao trocar de cena: carrega de novo.
            sprites = new Dictionary<string, Sprite>();
            foreach (var sprite in Resources.LoadAll<Sprite>(SheetResource)) sprites[sprite.name] = sprite;
            return sprites.TryGetValue(name, out var found) ? found : null;
        }
    }
}
