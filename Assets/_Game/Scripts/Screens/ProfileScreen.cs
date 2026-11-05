using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>
    /// Tela Meu time (do Menu): nome do time, sigla de 3 letras, cor principal e segunda cor (da lista Cores do time; podem
    /// ser iguais), camisa do botão e do goleiro (só as liberadas) e o saldo de moedas. Em cima, o botão e o goleiro já
    /// pintados. "Salvar" grava no save e volta ao Menu; "Voltar" volta sem gravar. Tocar no nome ou na sigla abre o
    /// teclado (no celular, o do aparelho); no teclado/controle, Confirmar começa a digitar. Monta a UI sozinha.
    /// </summary>
    public sealed class ProfileScreen : MonoBehaviour
    {
        private const float RowTop = 150f;
        private const float RowSpacing = 100f;
        private const float LabelX = -200f;
        private const float LabelWidth = 280f;
        private const float ValueX = 150f;
        private const float ValueWidth = 360f;
        private const float ArrowSize = 70f;
        private const float PreviewY = 330f;
        private const float PreviewSize = 150f;
        private static readonly Color PanelColor = new(0f, 0f, 0f, 0.55f);
        private static readonly Color ShadowColor = new(0f, 0f, 0f, 0.6f);
        private static readonly Color CoinColor = new(1f, 0.85f, 0.25f);
        private static readonly Color LabelColor = new(1f, 1f, 1f, 0.85f);

        [SerializeField] private MenuScreen menu;

        private Font font;
        private bool built;
        private InputField nameField, abbreviationField;
        private Image primarySwatch, secondarySwatch;
        private Text primaryLabel, secondaryLabel, discLabel, keeperLabel, coinsLabel;
        private GameObject discRow, keeperRow;
        private Image[] discLayers, keeperLayers;

        // Escolhas ainda não salvas.
        private Color primary, secondary;
        private string discId, keeperId;

        public void Configure(MenuScreen menuScreen) => menu = menuScreen;

        /// <summary>Abre com o que está salvo.</summary>
        public void Open()
        {
            Build();
            var data = PlayerProfile.Data;
            nameField.text = data.teamName;
            abbreviationField.text = data.abbreviation;
            primary = data.primary;
            secondary = data.secondary;
            discId = PlayerProfile.DiscPattern?.id ?? data.discPatternId;
            keeperId = PlayerProfile.KeeperPattern?.id ?? data.keeperPatternId;
            coinsLabel.text = $"Moedas: {PlayerProfile.Coins}";
            Refresh();
        }

        private void Save()
        {
            PlayerProfile.Save(nameField.text, abbreviationField.text, primary, secondary, discId, keeperId);
            Back();
        }

        private void Back()
        {
            if (menu != null) menu.ShowMode();
        }

        private void ChangePrimary(int step)
        {
            primary = TeamColorPalette.Load().Step(primary, step);
            Refresh();
        }

        private void ChangeSecondary(int step)
        {
            secondary = TeamColorPalette.Load().Step(secondary, step);
            Refresh();
        }

        private void ChangeShirt(bool keeper, int step)
        {
            if (keeper) keeperId = ShirtPattern.Step(ShirtPattern.Kind.Goleiro, keeperId, step);
            else discId = ShirtPattern.Step(ShirtPattern.Kind.Botao, discId, step);
            Refresh();
        }

        private void Refresh()
        {
            var palette = TeamColorPalette.Load();
            PaintSwatch(primarySwatch, primaryLabel, primary, palette.NameOf(primary));
            PaintSwatch(secondarySwatch, secondaryLabel, secondary, palette.NameOf(secondary));

            var disc = ShirtPattern.Resolve(ShirtPattern.Kind.Botao, discId);
            var keeper = ShirtPattern.Resolve(ShirtPattern.Kind.Goleiro, keeperId);
            discRow.SetActive(disc != null);
            keeperRow.SetActive(keeper != null);
            discLabel.text = disc != null ? disc.displayName : string.Empty;
            keeperLabel.text = keeper != null ? keeper.displayName : string.Empty;
            PaintPiece(discLayers, disc, false);
            PaintPiece(keeperLayers, keeper, true);
        }

        private static void PaintSwatch(Image swatch, Text label, Color color, string colorName)
        {
            swatch.color = color;
            label.text = colorName;
            // Nome escuro em cor clara, claro em cor escura.
            float light = 0.299f * color.r + 0.587f * color.g + 0.114f * color.b;
            label.color = light > 0.6f ? new Color(0.1f, 0.1f, 0.1f) : Color.white;
        }

        private void PaintPiece(Image[] layers, ShirtPattern pattern, bool keeper)
        {
            for (int i = 0; i < layers.Length; i++)
            {
                var layer = (ShirtLayer)i;
                var image = layers[i];
                image.sprite = pattern != null ? pattern.Get(layer) : null;
                image.color = ShirtArt.Tint(layer, primary, secondary, keeper);
                image.enabled = image.sprite != null;
            }
        }

        // ---- UI ----

        private void Build()
        {
            if (built) return;
            built = true;
            font = GameFont.Get();
            var root = (RectTransform)transform;

            CreateText(root, "Título", "Meu time", 52, FontStyle.Bold, Color.white, new Vector2(0f, 545f), new Vector2(680f, 90f));
            coinsLabel = CreateText(root, "Moedas", "Moedas: 0", 34, FontStyle.Bold, CoinColor, new Vector2(0f, 475f), new Vector2(680f, 60f));

            // Prévia: o botão e o goleiro pintados, direto sobre o fundo da tela.
            var preview = CreateRect(root, "Prévia", new Vector2(0f, PreviewY), new Vector2(460f, 200f));
            discLayers = CreatePiece(preview, "Botão", new Vector2(-110f, 0f), false);
            keeperLayers = CreatePiece(preview, "Goleiro", new Vector2(110f, 0f), true);

            var nameEntry = CreateField(root, "Nome", "Nome do time", 0, PlayerProfile.NameLength, false, out nameField);
            var abbreviationEntry = CreateField(root, "Sigla", "Sigla (3 letras)", 1, PlayerProfile.AbbreviationLength, true, out abbreviationField);
            var primaryEntry = CreateArrowRow(root, "Cor principal", 2, ChangePrimary, true, out primaryLabel, out primarySwatch, out _);
            var secondaryEntry = CreateArrowRow(root, "Segunda cor", 3, ChangeSecondary, true, out secondaryLabel, out secondarySwatch, out _);
            var discEntry = CreateArrowRow(root, "Camisa do botão", 4, step => ChangeShirt(false, step), false, out discLabel, out _, out discRow);
            var keeperEntry = CreateArrowRow(root, "Camisa do goleiro", 5, step => ChangeShirt(true, step), false, out keeperLabel, out _, out keeperRow);

            var back = CreateButton(root, "Voltar", "Voltar", new Vector2(-180f, -510f), new Vector2(260f, 80f), Back, ButtonStyle.Red);
            var save = CreateButton(root, "Salvar", "Salvar", new Vector2(150f, -510f), new Vector2(300f, 80f), Save, ButtonStyle.Yellow);

            var navigator = gameObject.AddComponent<MenuNavigator>();
            navigator.Configure(new[] { nameEntry, abbreviationEntry, primaryEntry, secondaryEntry, discEntry, keeperEntry, Entry(back), Entry(save) }, back);
        }

        private Image[] CreatePiece(Transform parent, string objectName, Vector2 position, bool keeper)
        {
            var piece = CreateRect(parent, objectName, position, Vector2.one * PreviewSize);
            var layers = new Image[ShirtArt.LayerCount];
            for (int i = 0; i < layers.Length; i++)
            {
                var layer = (ShirtLayer)i;
                var offset = keeper && layer == ShirtLayer.Shadow ? ShirtArt.KeeperShadowOffset * PreviewSize : Vector2.zero;
                var image = CreateRect(piece, layer.ToString(), offset, Vector2.one * PreviewSize).gameObject.AddComponent<Image>();
                image.raycastTarget = false;
                layers[i] = image;
            }
            return layers;
        }

        private static float RowY(int row) => RowTop - row * RowSpacing;

        private Text CreateRowLabel(Transform parent, string label, float y)
        {
            var text = CreateText(parent, "Rótulo", label, 28, FontStyle.Bold, LabelColor, new Vector2(LabelX, y), new Vector2(LabelWidth, 70f));
            text.alignment = TextAnchor.MiddleLeft;
            return text;
        }

        /// <summary>Linha com um campo de texto; Confirmar (teclado/controle) começa a digitar.</summary>
        private MenuEntry CreateField(Transform parent, string label, string placeholder, int row, int limit, bool upper, out InputField field)
        {
            float y = RowY(row);
            var line = CreateRect(parent, label, Vector2.zero, Vector2.zero);
            CreateRowLabel(line, label, y);

            var box = CreateRect(line, "Campo", new Vector2(ValueX, y), new Vector2(ValueWidth, 76f));
            var background = box.gameObject.AddComponent<Image>();
            var panelSprite = ButtonArt.Find(ButtonSkin.HudPanelName);
            if (panelSprite != null)
            {
                background.sprite = panelSprite;
                background.type = Image.Type.Sliced;
            }
            else background.color = PanelColor;

            var text = CreateText(box, "Texto", string.Empty, 32, FontStyle.Bold, Color.white, Vector2.zero, Vector2.zero);
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            Stretch(text.rectTransform, 16f);
            var hint = CreateText(box, "Dica", placeholder, 26, FontStyle.Italic, new Color(1f, 1f, 1f, 0.4f), Vector2.zero, Vector2.zero);
            Stretch(hint.rectTransform, 16f);

            field = box.gameObject.AddComponent<InputField>();
            field.targetGraphic = background;
            field.textComponent = text;
            field.placeholder = hint;
            field.characterLimit = limit;
            field.lineType = InputField.LineType.SingleLine;
            field.navigation = new Navigation { mode = Navigation.Mode.None };
            field.customCaretColor = true;
            field.caretColor = Color.white;
            if (upper)
            {
                field.keyboardType = TouchScreenKeyboardType.ASCIICapable;
                field.onValidateInput = (_, _, c) => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '\0';
            }
            var target = field;
            field.onEndEdit.AddListener(value => target.text = upper ? PlayerProfile.CleanAbbreviation(value) : PlayerProfile.CleanName(value));

            var start = CreateHiddenButton(line, "Digitar", () =>
            {
                target.Select();
                target.ActivateInputField();
            });
            var entry = line.gameObject.AddComponent<MenuEntry>();
            entry.Configure(start, null, null, box);
            return entry;
        }

        /// <summary>Linha com setas "&lt;" e "&gt;" e o valor no meio (numa amostra da cor, nas linhas de cor).</summary>
        private MenuEntry CreateArrowRow(Transform parent, string label, int row, UnityAction<int> change, bool swatch,
            out Text value, out Image swatchImage, out GameObject rowObject)
        {
            float y = RowY(row);
            var line = CreateRect(parent, label, Vector2.zero, Vector2.zero);
            rowObject = line.gameObject;
            CreateRowLabel(line, label, y);

            float leftX = ValueX - ValueWidth * 0.5f + ArrowSize * 0.5f;
            float rightX = ValueX + ValueWidth * 0.5f - ArrowSize * 0.5f;
            var previous = CreateButton(line, "Anterior", "<", new Vector2(leftX, y), Vector2.one * ArrowSize, () => change(-1));
            var next = CreateButton(line, "Próxima", ">", new Vector2(rightX, y), Vector2.one * ArrowSize, () => change(1));

            var middle = new Vector2(ValueWidth - 2f * ArrowSize - 20f, 60f);
            swatchImage = null;
            if (swatch)
            {
                swatchImage = CreateRect(line, "Amostra", new Vector2(ValueX, y), middle).gameObject.AddComponent<Image>();
                swatchImage.raycastTarget = false;
                value = CreateText(swatchImage.transform, "Valor", string.Empty, 24, FontStyle.Bold, Color.white, Vector2.zero, middle);
                Destroy(value.GetComponent<Shadow>());
            }
            else value = CreateText(line, "Valor", string.Empty, 26, FontStyle.Bold, Color.white, new Vector2(ValueX, y), middle);

            var entry = line.gameObject.AddComponent<MenuEntry>();
            entry.Configure(null, previous, next, (RectTransform)previous.transform, (RectTransform)next.transform);
            return entry;
        }

        private static MenuEntry Entry(Button button)
        {
            var entry = button.gameObject.AddComponent<MenuEntry>();
            entry.Configure(button);
            return entry;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, 0f);
            rect.offsetMax = new Vector2(-inset, 0f);
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

        /// <summary>Botão sem imagem nem clique: só para o teclado/controle apertar (MenuEntry).</summary>
        private static Button CreateHiddenButton(Transform parent, string objectName, UnityAction action)
        {
            var button = CreateRect(parent, objectName, Vector2.zero, Vector2.zero).gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(action);
            return button;
        }
    }
}
