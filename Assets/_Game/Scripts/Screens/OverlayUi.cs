using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>
    /// Peças das telas que abrem por cima de tudo, montadas por código (idade, anúncio de teste): Canvas próprio com o
    /// fundo escurecido, painel, textos e botões no estilo do jogo. Enquanto uma está aberta, os outros menus param
    /// (<see cref="PauseNavigators"/>), senão a mesma tecla mexe nos dois.
    /// </summary>
    internal static class OverlayUi
    {
        public static readonly Vector2 ReferenceResolution = new(720f, 1280f);
        public static readonly Color DimColor = new(0f, 0f, 0f, 0.6f);
        public static readonly Color PanelColor = new(0f, 0f, 0f, 0.85f);
        public static readonly Color ShadowColor = new(0f, 0f, 0f, 0.6f);
        public static readonly Color LabelColor = new(1f, 1f, 1f, 0.85f);
        public static readonly Color HighlightColor = new(1f, 0.85f, 0.25f);

        /// <summary>Canvas por cima de tudo (<paramref name="order"/>), com o fundo escurecido. Devolve a raiz.</summary>
        public static RectTransform CreateCanvas(GameObject go, int order)
        {
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            go.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)go.transform;
            var dim = CreateRect(root, "Fundo", Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            dim.rectTransform.anchorMin = Vector2.zero;
            dim.rectTransform.anchorMax = Vector2.one;
            dim.color = DimColor;
            return root;
        }

        /// <summary>Painel no meio da tela, com a arte do painel da HUD.</summary>
        public static RectTransform CreatePanel(RectTransform root, Vector2 size)
        {
            var panel = CreateRect(root, "Painel", Vector2.zero, size);
            var background = panel.gameObject.AddComponent<Image>();
            var sprite = ButtonArt.Find(ButtonSkin.HudPanelName);
            if (sprite != null)
            {
                background.sprite = sprite;
                background.type = Image.Type.Sliced;
            }
            else background.color = PanelColor;
            return panel;
        }

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

        public static Text CreateText(Transform parent, string objectName, string content, int size, FontStyle style, Color color,
            Vector2 position, Vector2 box)
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
            text.gameObject.AddComponent<Shadow>().effectColor = ShadowColor;
            TextFit.Shrink(text);
            return text;
        }

        public static Button CreateButton(Transform parent, string objectName, string label, Vector2 position, Vector2 size, UnityAction action,
            ButtonStyle style = ButtonStyle.Green)
        {
            var rect = CreateRect(parent, objectName, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            if (action != null) button.onClick.AddListener(action);
            var text = CreateText(rect, "Texto", label, 30, FontStyle.Bold, ButtonSkin.TextColor(style), Vector2.zero, size);
            UnityEngine.Object.Destroy(text.GetComponent<Shadow>());
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            ButtonSkin.Apply(button, style);
            return button;
        }

        public static MenuEntry Entry(Button button)
        {
            var entry = button.gameObject.AddComponent<MenuEntry>();
            entry.Configure(button);
            return entry;
        }

        /// <summary>Desliga os menus ligados (menos os de <paramref name="except"/>) e devolve quais, para religar depois.</summary>
        public static List<MenuNavigator> PauseNavigators(GameObject except)
        {
            var paused = new List<MenuNavigator>();
            foreach (var other in UnityEngine.Object.FindObjectsByType<MenuNavigator>())
            {
                if (!other.enabled || (except != null && other.transform.IsChildOf(except.transform))) continue;
                other.enabled = false;
                paused.Add(other);
            }
            return paused;
        }

        public static void ResumeNavigators(List<MenuNavigator> paused)
        {
            foreach (var navigator in paused)
                if (navigator != null) navigator.enabled = true;
            paused.Clear();
        }

        /// <summary>Texto do botão (o que <see cref="CreateButton"/> criou).</summary>
        public static Text Label(Button button) => button != null ? button.GetComponentInChildren<Text>(true) : null;

        /// <summary>Chama <paramref name="action"/> sem deixar o erro dela quebrar quem chamou.</summary>
        public static void SafeInvoke(Action action)
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
