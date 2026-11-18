using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>
    /// Fábrica das peças de interface montadas por código (telas e HUD): retângulo, texto, botão, botão escondido e
    /// imagem, com a fonte do jogo (<see cref="GameFont"/>), o encolher do texto (<see cref="TextFit"/>), a tradução
    /// (<see cref="LocalizedText"/>) e a arte dos botões (<see cref="ButtonSkin"/>). Mudou o estilo? Muda aqui e vale
    /// para todas as telas. O "Criar telas" (editor) tem a sua própria, porque grava os objetos na cena.
    /// </summary>
    internal static class UiKit
    {
        /// <summary>Sombra preta dos textos.</summary>
        public static readonly Color ShadowColor = new(0f, 0f, 0f, 0.6f);
        /// <summary>Tamanho da fonte dos botões.</summary>
        public const int ButtonFontSize = 30;

        /// <summary>Retângulo com âncora e pivô no meio do pai.</summary>
        public static RectTransform CreateRect(Transform parent, string objectName, Vector2 position, Vector2 size)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        /// <summary>Estica o retângulo para ocupar o pai inteiro.</summary>
        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Texto centralizado que quebra linha, sem receber toque. <paramref name="bestFitMin"/> 0: a fonte diminui
        /// pelo <see cref="TextFit"/> se não couber; maior que 0: o "Best Fit" do Unity, até esse tamanho.
        /// </summary>
        public static Text CreateText(Transform parent, string objectName, string content, int size, FontStyle style, Color color,
            Vector2 position, Vector2 box, bool shadow = true, int bestFitMin = 0)
        {
            var text = CreateRect(parent, objectName, position, box).gameObject.AddComponent<Text>();
            text.font = GameFont.Get();
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            if (shadow) text.gameObject.AddComponent<Shadow>().effectColor = ShadowColor;
            if (bestFitMin > 0)
            {
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = bestFitMin;
                text.resizeTextMaxSize = size;
            }
            else TextFit.Shrink(text);
            return text;
        }

        /// <summary>
        /// Botão com a arte do estilo e o texto esticado por dentro. <paramref name="localize"/>: o texto troca junto
        /// com o idioma. Sem navegação automática (os menus usam o <see cref="MenuNavigator"/>).
        /// </summary>
        public static Button CreateButton(Transform parent, string objectName, string label, Vector2 position, Vector2 size, UnityAction action,
            ButtonStyle style = ButtonStyle.Green, bool localize = false, int bestFitMin = 0)
        {
            var rect = CreateRect(parent, objectName, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            if (action != null) button.onClick.AddListener(action);
            var text = CreateText(rect, "Texto", label, ButtonFontSize, FontStyle.Bold, ButtonSkin.TextColor(style), Vector2.zero, size,
                shadow: false, bestFitMin: bestFitMin);
            if (localize) LocalizedText.Attach(text);
            Stretch(text.rectTransform);
            ButtonSkin.Apply(button, style);
            return button;
        }

        /// <summary>Botão sem imagem nem clique: só para o teclado/controle apertar (MenuEntry).</summary>
        public static Button CreateHiddenButton(Transform parent, string objectName, UnityAction action)
        {
            var button = CreateRect(parent, objectName, Vector2.zero, Vector2.zero).gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(action);
            return button;
        }

        /// <summary>Imagem que não recebe toque.</summary>
        public static Image CreateImage(Transform parent, string objectName, Sprite sprite, Color color, Vector2 position, Vector2 size)
        {
            var image = CreateRect(parent, objectName, position, size).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }
}
