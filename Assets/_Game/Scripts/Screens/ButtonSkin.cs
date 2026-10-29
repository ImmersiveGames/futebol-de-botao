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
        /// <summary>Marrom (sobrou da folha antiga, hoje sem uso fixo).</summary>
        Brown,
        /// <summary>Ação principal: Jogar, Avançar, Pronto, Pro Gol!.</summary>
        Yellow,
        /// <summary>Desligado (não dá para tocar).</summary>
        Gray,
    }

    /// <summary>
    /// Veste um <see cref="Button"/> com a moldura 9-slice da folha Art/UI/Botões_UI-Sheet (32x32, borda de 10 px, achada pelo
    /// <see cref="ButtonArt"/> em Resources): troca para a versão apertada no toque, usa a cinza quando o botão está
    /// desligado e acerta a cor do texto.
    /// Também põe o som de clique (<see cref="ButtonClickSound"/>).
    /// Sem a folha (ou sem os recortes), não mexe em nada e devolve falso: quem chamou mantém a cor lisa de antes.
    /// Os recortes são feitos pelo menu "Criar telas".
    /// </summary>
    public static class ButtonSkin
    {
        /// <summary>Nome do recorte de cada estilo na folha (mesma ordem de <see cref="ButtonStyle"/>).</summary>
        public static readonly string[] StyleNames = { "Green_Button", "Red_Button", "Brown_Button", "Yellow_Button", "Gray_Button" };
        public const string PressedSuffix = "_Pressed";
        /// <summary>Fundo 9-slice das faixas da HUD, na mesma folha.</summary>
        public const string HudPanelName = "Hud_BG_Tile";
        /// <summary>Ícones da folha Art/UI/Icons-Sheet (32x32, desenho branco no transparente).</summary>
        public const string KeyboardIcon = "Icons_Keyboard";
        public const string MouseIcon = "Icons_Mouse";
        public const string TouchIcon = "Icons_touch";
        public const string GamepadIcon = "Icons_Joypad";
        public const string PauseIcon = "Icons_Pause";
        private const string IconObjectName = "Ícone";
        /// <summary>Vão entre as imagens de um ícone de várias partes (teclado e mouse), em pixels da arte.</summary>
        private const float IconGapPixels = 3f;
        private const float IconArtPixels = 32f;
        public const int TileSize = 32;
        public const int BorderPixels = 10;
        /// <summary>
        /// No botão normal a face tem 2 px de borda em cima e 6 px (a faixa escura) embaixo: o texto sobe 2 px para ficar
        /// no meio da face. No apertado as bordas são iguais (3 e 3) e o texto volta ao centro, como se afundasse junto.
        /// </summary>
        public const float FaceRaisePixels = 2f;
        /// <summary>
        /// Pixels da arte fora do miolo da face, na altura: 2 de borda em cima, 6 da faixa escura embaixo e 1 do filete
        /// claro de cada lado. O ícone não passa da altura que sobra, para não entrar na parte de perspectiva.
        /// </summary>
        private const float FaceVerticalInsetPixels = 10f;
        /// <summary>Na largura: 3 pixels de cada lado (contorno, borda escura e filete claro).</summary>
        private const float FaceHorizontalInsetPixels = 6f;

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
            var disabled = Find(ButtonStyle.Gray, false);
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

            // Com ícone, ele faz o papel do texto (sobe com a face, escurece no toque, cor do texto do estilo).
            RectTransform label = FindIcon(button);
            if (label == null)
            {
                var caption = button.GetComponentInChildren<Text>(true);
                if (caption != null) label = caption.rectTransform;
            }
            if (!button.TryGetComponent<ButtonSkinText>(out var text)) text = button.gameObject.AddComponent<ButtonSkinText>();
            // Um pixel da arte em unidades da UI, já com a borda encolhida dos botões baixos.
            float artPixel = ReferencePixelsPerUnit / normal.pixelsPerUnit / image.pixelsPerUnitMultiplier;
            if (label != null && label.name == IconObjectName) FitIcon(label, size, artPixel);
            text.Configure(button, label, TextColor(style), FaceRaisePixels * artPixel);
            if (!button.TryGetComponent<ButtonClickSound>(out _)) button.gameObject.AddComponent<ButtonClickSound>();
            return true;
        }

        /// <summary>
        /// Troca o texto do botão por um ícone da folha de ícones. <paramref name="cellSize"/> é o lado, em unidades da UI,
        /// de uma célula de 32 px da arte. Com mais de um nome (teclado e mouse), as imagens ficam lado a lado na mesma
        /// escala, com um vão de 3 px. Sem a arte, não mexe em nada e devolve falso (fica o texto). Chamar antes do
        /// <see cref="Apply"/>, que encolhe o ícone se ele não couber na face.
        /// </summary>
        public static bool SetIcon(Button button, float cellSize, params string[] iconNames)
        {
            if (button == null || iconNames == null || iconNames.Length == 0) return false;
            var sprites = new Sprite[iconNames.Length];
            for (int i = 0; i < iconNames.Length; i++)
                if ((sprites[i] = ButtonArt.Find(iconNames[i])) == null) return false;

            var old = FindIcon(button);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var group = (RectTransform)new GameObject(IconObjectName, typeof(RectTransform)).transform;
            group.SetParent(button.transform, false);
            group.anchorMin = group.anchorMax = group.pivot = new Vector2(0.5f, 0.5f);
            group.anchoredPosition = Vector2.zero;

            float scale = cellSize / IconArtPixels;
            float gap = IconGapPixels * scale;
            float width = -gap, height = 0f;
            foreach (var sprite in sprites)
            {
                width += sprite.rect.width * scale + gap;
                height = Mathf.Max(height, sprite.rect.height * scale);
            }
            group.sizeDelta = new Vector2(width, height);

            float x = -width * 0.5f;
            foreach (var sprite in sprites)
            {
                var part = new GameObject(sprite.name, typeof(RectTransform)).AddComponent<Image>();
                part.transform.SetParent(group, false);
                part.sprite = sprite;
                part.raycastTarget = false;
                var rect = part.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(sprite.rect.width, sprite.rect.height) * scale;
                rect.anchoredPosition = new Vector2(x + rect.sizeDelta.x * 0.5f, 0f);
                x += rect.sizeDelta.x + gap;
            }

            foreach (var text in button.GetComponentsInChildren<Text>(true))
                if (text.transform.parent == button.transform) text.enabled = false;
            return true;
        }

        /// <summary>Encolhe o ícone (sem mudar a proporção) para caber no miolo da face do botão; nunca aumenta.</summary>
        private static void FitIcon(RectTransform icon, Vector2 buttonSize, float artPixel)
        {
            float faceHeight = buttonSize.y - FaceVerticalInsetPixels * artPixel;
            float faceWidth = buttonSize.x - FaceHorizontalInsetPixels * artPixel;
            var natural = icon.sizeDelta;
            if (natural.x <= 0f || natural.y <= 0f) return;
            float fit = Mathf.Min(1f, faceWidth / natural.x, faceHeight / natural.y);
            icon.localScale = new Vector3(fit, fit, 1f);
        }

        private static RectTransform FindIcon(Button button) => button.transform.Find(IconObjectName) as RectTransform;

        private static Sprite Find(ButtonStyle style, bool pressed)
        {
            string name = StyleNames[(int)style] + (pressed ? PressedSuffix : string.Empty);
            if (sprites != null && sprites.TryGetValue(name, out var cached) && cached != null) return cached;

            // Primeira vez, ou o editor descarregou os sprites ao trocar de cena: carrega de novo.
            sprites = new Dictionary<string, Sprite>();
            var art = Resources.Load<ButtonArt>(ButtonArt.ResourcePath);
            if (art != null)
                foreach (var sprite in art.sprites)
                    if (sprite != null) sprites[sprite.name] = sprite;
            return sprites.TryGetValue(name, out var found) ? found : null;
        }
    }
}
