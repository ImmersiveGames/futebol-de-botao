using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>
    /// Tela Configurações (Menu e pausa): Idioma (<see cref="Txt"/>) e Volume geral, Música e Efeitos de 0% a 100%
    /// (<see cref="GameVolume"/>). Monta a UI
    /// sozinha num Canvas próprio por cima de tudo, com o fundo escurecido; enquanto aberta, os outros menus não respondem ao
    /// teclado/controle. Mudar um volume vale na hora (o clique das setas já sai no volume novo); Voltar grava e fecha.
    /// </summary>
    public sealed class SettingsScreen : MonoBehaviour
    {
        private static readonly Vector2 ReferenceResolution = new(720f, 1280f);
        private static readonly Color DimColor = new(0f, 0f, 0f, 0.6f);
        private static readonly Color PanelColor = new(0f, 0f, 0f, 0.85f);
        private static readonly Color ShadowColor = new(0f, 0f, 0f, 0.6f);
        private static readonly Color LabelColor = new(1f, 1f, 1f, 0.85f);
        private const float LabelX = -150f;
        private const float LabelWidth = 240f;
        private const float ValueX = 150f;
        private const float ValueWidth = 300f;
        private const float ArrowSize = 70f;

        private static SettingsScreen open;

        private readonly List<MenuNavigator> pausedNavigators = new();
        private Action onClose;
        private bool fromPause;
        private Font font;
        private Text languageLabel, masterLabel, musicLabel, effectsLabel;

        /// <summary>Abre a tela (se já estiver aberta, nada muda). <paramref name="followPauser"/>: só quem pausou navega.</summary>
        public static void Open(bool followPauser = false, Action closed = null)
        {
            if (open != null) return;
            var go = new GameObject("Configurações", typeof(RectTransform));
            go.SetActive(false);
            open = go.AddComponent<SettingsScreen>();
            open.onClose = closed;
            open.fromPause = followPauser;
            open.Build(followPauser);
            go.SetActive(true);
        }

        private void Close()
        {
            GameSave.WriteOnly("configurações");
            foreach (var navigator in pausedNavigators)
                if (navigator != null) navigator.enabled = true;
            pausedNavigators.Clear();
            if (open == this) open = null;
            var closed = onClose;
            Destroy(gameObject);
            closed?.Invoke();
        }

        // Aberta na pausa: se a partida voltar (pausa tirada por outro botão), fecha junto.
        private void Update()
        {
            if (fromPause && Time.timeScale > 0f) Close();
        }

        private void OnDestroy()
        {
            if (open == this) open = null;
        }

        /// <summary>Automático, depois os idiomas com tradução (dá a volta).</summary>
        private void ChangeLanguage(int step)
        {
            var options = new List<string> { string.Empty };
            options.AddRange(Txt.Available());
            int index = Mathf.Max(0, options.IndexOf(Txt.Choice));
            Txt.Choice = options[((index + step) % options.Count + options.Count) % options.Count];
            Refresh();
        }

        private void ChangeMaster(int step)
        {
            GameVolume.Master = Mathf.Clamp(GameVolume.Master + step, 0, GameVolume.MaxLevel);
            Refresh();
        }

        private void ChangeMusic(int step)
        {
            GameVolume.Music = Mathf.Clamp(GameVolume.Music + step, 0, GameVolume.MaxLevel);
            Refresh();
        }

        private void ChangeEffects(int step)
        {
            GameVolume.Effects = Mathf.Clamp(GameVolume.Effects + step, 0, GameVolume.MaxLevel);
            Refresh();
        }

        private void Refresh()
        {
            languageLabel.text = string.IsNullOrEmpty(Txt.Choice) ? Txt.Get("Automático") : Txt.NativeName(Txt.Choice);
            masterLabel.text = GameVolume.Percent(GameVolume.Master);
            musicLabel.text = GameVolume.Percent(GameVolume.Music);
            effectsLabel.text = GameVolume.Percent(GameVolume.Effects);
        }

        // ---- UI ----

        private void Build(bool followPauser)
        {
            font = GameFont.Get();
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            gameObject.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)transform;

            var dim = CreateRect(root, "Fundo", Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            dim.rectTransform.anchorMin = Vector2.zero;
            dim.rectTransform.anchorMax = Vector2.one;
            dim.color = DimColor;

            var panel = CreateRect(root, "Painel", Vector2.zero, new Vector2(660f, 650f));
            var background = panel.gameObject.AddComponent<Image>();
            var panelSprite = ButtonArt.Find(ButtonSkin.HudPanelName);
            if (panelSprite != null)
            {
                background.sprite = panelSprite;
                background.type = Image.Type.Sliced;
            }
            else background.color = PanelColor;

            LocalizedText.Attach(CreateText(panel, "Título", "Configurações", 48, FontStyle.Bold, Color.white, new Vector2(0f, 245f), new Vector2(600f, 80f)));
            var language = CreateArrowRow(panel, "Idioma", 130f, ChangeLanguage, out languageLabel);
            var master = CreateArrowRow(panel, "Volume geral", 40f, ChangeMaster, out masterLabel);
            var music = CreateArrowRow(panel, "Música", -50f, ChangeMusic, out musicLabel);
            var effects = CreateArrowRow(panel, "Efeitos", -140f, ChangeEffects, out effectsLabel);
            var back = CreateButton(panel, "Voltar", "Voltar", new Vector2(0f, -250f), new Vector2(300f, 80f), Close, ButtonStyle.Red);
            LocalizedText.Attach(back.GetComponentInChildren<Text>());
            var backEntry = back.gameObject.AddComponent<MenuEntry>();
            backEntry.Configure(back);

            // Os outros menus param enquanto esta tela está aberta (senão a mesma tecla mexe nos dois).
            foreach (var other in FindObjectsByType<MenuNavigator>(FindObjectsSortMode.None))
            {
                if (!other.enabled) continue;
                other.enabled = false;
                pausedNavigators.Add(other);
            }
            var navigator = gameObject.AddComponent<MenuNavigator>();
            if (followPauser) navigator.FollowPauser();
            navigator.Configure(new[] { language, master, music, effects, backEntry }, back);
            Refresh();
        }

        /// <summary>Linha com setas "&lt;" e "&gt;" e o volume no meio; esquerda/direita mudam o valor.</summary>
        private MenuEntry CreateArrowRow(Transform parent, string label, float y, UnityAction<int> change, out Text value)
        {
            var line = CreateRect(parent, label, Vector2.zero, Vector2.zero);
            var caption = CreateText(line, "Rótulo", label, 30, FontStyle.Bold, LabelColor, new Vector2(LabelX, y), new Vector2(LabelWidth, 70f));
            caption.alignment = TextAnchor.MiddleLeft;
            LocalizedText.Attach(caption);
            float leftX = ValueX - ValueWidth * 0.5f + ArrowSize * 0.5f;
            float rightX = ValueX + ValueWidth * 0.5f - ArrowSize * 0.5f;
            var previous = CreateButton(line, "Anterior", "<", new Vector2(leftX, y), Vector2.one * ArrowSize, () => change(-1));
            var next = CreateButton(line, "Próxima", ">", new Vector2(rightX, y), Vector2.one * ArrowSize, () => change(1));
            value = CreateText(line, "Valor", string.Empty, 30, FontStyle.Bold, Color.white, new Vector2(ValueX, y),
                new Vector2(ValueWidth - 2f * ArrowSize - 20f, 60f));
            var entry = line.gameObject.AddComponent<MenuEntry>();
            entry.Configure(null, previous, next, (RectTransform)previous.transform, (RectTransform)next.transform);
            return entry;
        }

        private static RectTransform CreateRect(Transform parent, string objectName, Vector2 position, Vector2 size)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private Text CreateText(Transform parent, string objectName, string content, int size, FontStyle style, Color color,
            Vector2 position, Vector2 box)
        {
            var text = CreateRect(parent, objectName, position, box).gameObject.AddComponent<Text>();
            text.font = font;
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

        private Button CreateButton(Transform parent, string objectName, string label, Vector2 position, Vector2 size, UnityAction action,
            ButtonStyle style = ButtonStyle.Green)
        {
            var rect = CreateRect(parent, objectName, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(action);
            var text = CreateText(rect, "Texto", label, 30, FontStyle.Bold, ButtonSkin.TextColor(style), Vector2.zero, size);
            Destroy(text.GetComponent<Shadow>());
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            ButtonSkin.Apply(button, style);
            return button;
        }
    }
}
