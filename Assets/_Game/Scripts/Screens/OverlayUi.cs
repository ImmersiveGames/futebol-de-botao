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

        public static RectTransform CreateRect(Transform parent, string objectName, Vector2 position, Vector2 size) =>
            UiKit.CreateRect(parent, objectName, position, size);

        public static Text CreateText(Transform parent, string objectName, string content, int size, FontStyle style, Color color,
            Vector2 position, Vector2 box) =>
            UiKit.CreateText(parent, objectName, content, size, style, color, position, box);

        public static Button CreateButton(Transform parent, string objectName, string label, Vector2 position, Vector2 size, UnityAction action,
            ButtonStyle style = ButtonStyle.Green) =>
            UiKit.CreateButton(parent, objectName, label, position, size, action, style);

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
