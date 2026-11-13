using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>
    /// Tela Loja (do Menu): o saldo de moedas em cima e abas Botão, Goleiro, Bola, Mesa e Feltro. Cada item é um cartão
    /// com a prévia (camisas pintadas com as cores do Meu time; mesa e feltro usam a miniatura do asset, se houver), o
    /// nome e o preço, ou "Já é seu". Comprar pergunta antes; sem saldo, o preço fica cinza. Esquerda/direita na linha das
    /// abas (ou LB/RB) troca a aba. Monta a UI sozinha (como as Estatísticas).
    /// </summary>
    public sealed class ShopScreen : MonoBehaviour
    {
        private static readonly string[] TabNames = { "Botão", "Goleiro", "Bola", "Mesa", "Feltro" };
        private const float TabY = 400f;
        private const float TabSpacing = 136f;
        private static readonly Vector2 TabSize = new(128f, 76f);
        private static readonly Vector2 ListCenter = new(0f, -40f);
        private static readonly Vector2 ListSize = new(680f, 760f);
        private const float ListPadding = 18f;
        private const int Columns = 2;
        private const float CardGap = 16f;
        private const float CardHeight = 290f;
        private const float PreviewY = 55f;
        private const float PreviewBox = 130f;
        private static readonly Color CardColor = new(1f, 1f, 1f, 0.08f);
        private static readonly Color CoinColor = new(1f, 0.85f, 0.25f);
        private static readonly Color OwnedColor = new(0.55f, 1f, 0.55f);
        private static readonly Color PanelColor = new(0f, 0f, 0f, 0.55f);
        private static readonly Color DimColor = new(0f, 0f, 0f, 0.7f);
        private static readonly Color ShadowColor = new(0f, 0f, 0f, 0.6f);
        private static readonly Color WoodColor = new(0.35f, 0.22f, 0.12f);
        private static readonly Color FeltColor = new(0.13f, 0.45f, 0.2f);

        /// <summary>Um item à venda, de qualquer tipo.</summary>
        private sealed class Item
        {
            public string Name;
            public string Key;
            public int Price;
            public bool Unlocked;
            public System.Action<RectTransform> Preview;
        }

        [SerializeField] private MenuScreen menu;

        private readonly Button[] tabs = new Button[TabNames.Length];
        private readonly Dictionary<MenuEntry, (float top, float bottom)> cardSpans = new();
        private Font font;
        private bool built;
        private int tab;
        private ScrollRect scroll;
        private RectTransform content;
        private MenuNavigator navigator;
        private MenuEntry tabEntry, backEntry;
        private Text coinsLabel;
        private int shownCoins = -1;
        private GameObject dialog;
        private Text question;
        private Item pending;

        public void Configure(MenuScreen menuScreen) => menu = menuScreen;

        /// <summary>Abre na primeira aba.</summary>
        public void Open()
        {
            Build();
            CloseDialog();
            Purchases.Retry();
            ShowTab(0);
        }

        private void Update()
        {
            if (!built) return;
            if (shownCoins != Shop.Coins) RefreshCoins();
            // A loja termina de conectar depois de abrir a tela: o preço e o botão aparecem quando ela estiver pronta.
            if (dialog.activeSelf) return;

            foreach (var gamepad in Gamepad.all)
            {
                if (gamepad.leftShoulder.wasPressedThisFrame) ShowTab(tab - 1);
                if (gamepad.rightShoulder.wasPressedThisFrame) ShowTab(tab + 1);
            }
            // Teclado/controle: a lista rola até o cartão escolhido.
            if (PadMode.Active && navigator.Current != null && cardSpans.TryGetValue(navigator.Current, out var span)) ScrollTo(span.top, span.bottom);
        }

        private void ScrollTo(float top, float bottom)
        {
            float view = scroll.viewport.rect.height;
            float overflow = Mathf.Max(0f, content.rect.height - view);
            float y = content.anchoredPosition.y;
            if (top < y) y = top;
            else if (bottom > y + view) y = bottom - view;
            content.anchoredPosition = new Vector2(0f, Mathf.Clamp(y, 0f, overflow));
        }

        /// <summary>Mostra o botão de remover anúncios (e afasta o Voltar) se dá para comprar. Devolve se está à mostra.</summary>
        private bool RefreshRemoveAds()
        {
            bool shown = Purchases.Supported && !Ads.Removed;
            removeAdsButton.gameObject.SetActive(shown);
            ((RectTransform)backButton.transform).anchoredPosition = new Vector2(shown ? -180f : 0f, -510f);
            if (!shown) return false;
            // O preço fica no pop-up (no botão não cabia numa linha).
            removeAdsButton.GetComponentInChildren<Text>().text = Txt.Get("Remover anúncios");
            // Fica clicável mesmo com a loja fora: o pop-up conta o motivo e deixa tentar de novo.
            removeAdsButton.interactable = !purchasing;
            return true;
        }

        /// <summary>"Remover anúncios": o pop-up explica e mostra o preço; Comprar abre a compra da loja.</summary>
        private void BuyRemoveAds()
        {
            if (!Purchases.Ready)
            {
                string reason = Txt.Get("A loja do Google Play ainda não respondeu. Tente de novo daqui a pouco.");
                if (!string.IsNullOrEmpty(Purchases.Status)) reason += "\n\n(" + Purchases.Status + ")";
                MessageDialog.Open(Txt.Get("Loja indisponível"), reason, Txt.Get("Tentar de novo"), Purchases.Retry, Txt.Get("Voltar"));
                return;
            }
            string price = Purchases.RemoveAdsPrice;
            string message = (string.IsNullOrEmpty(price) ? string.Empty : Txt.Format("Compra única de {0}.", price) + " ")
                             + Txt.Get("Tira para sempre os anúncios entre as partidas.") + "\n\n"
                             + Txt.Get("Os anúncios com prêmio (dobrar moedas, patrocínio) continuam, só quando você quiser.");
            MessageDialog.Open(Txt.Get("Remover anúncios"), message, Txt.Get("Comprar"), StartPurchase, Txt.Get("Agora não"));
        }

        private void StartPurchase()
        {
            purchasing = true;
            removeAdsButton.interactable = false;
            Purchases.BuyRemoveAds(_ =>
            {
                purchasing = false;
                if (this != null && built) FillList();
            });
        }

        private void RefreshCoins()
        {
            shownCoins = Shop.Coins;
            coinsLabel.text = Txt.Format("Moedas: {0}", shownCoins);
        }

        private void Back()
        {
            if (menu != null) menu.ShowMode();
        }

        // ---- Abas ----

        private void ShowTab(int index)
        {
            tab = ((index % TabNames.Length) + TabNames.Length) % TabNames.Length;
            for (int i = 0; i < tabs.Length; i++)
                ButtonSkin.Apply(tabs[i], i == tab ? ButtonStyle.Yellow : ButtonStyle.Green);
            FillList();
        }

        private List<Item> Items()
        {
            var items = new List<Item>();
            switch (tab)
            {
                case 0:
                case 1:
                    var kind = tab == 0 ? ShirtPattern.Kind.Botao : ShirtPattern.Kind.Goleiro;
                    foreach (var pattern in ShirtPattern.All())
                    {
                        if (pattern.kind != kind || !pattern.Usable) continue;
                        var shirt = pattern;
                        items.Add(new Item
                        {
                            Name = Txt.Get(shirt.displayName), Key = shirt.ShopKey, Price = shirt.price, Unlocked = shirt.Unlocked,
                            Preview = area => PreviewShirt(area, shirt)
                        });
                    }
                    break;
                case 2:
                    foreach (var theme in BallTheme.All())
                    {
                        if (!theme.HasFrames) continue;
                        var ball = theme;
                        items.Add(new Item
                        {
                            Name = Txt.Get(ball.displayName), Key = ball.ShopKey, Price = ball.price, Unlocked = ball.Unlocked,
                            Preview = area => PreviewBall(area, ball)
                        });
                    }
                    break;
                case 3:
                    foreach (var theme in TableTheme.All())
                    {
                        var table = theme;
                        items.Add(new Item
                        {
                            Name = Txt.Get(table.displayName), Key = table.ShopKey, Price = table.price, Unlocked = table.Unlocked,
                            Preview = area => PreviewTable(area, table)
                        });
                    }
                    break;
                default:
                    foreach (var theme in FeltTheme.All())
                    {
                        var felt = theme;
                        items.Add(new Item
                        {
                            Name = Txt.Get(felt.displayName), Key = felt.ShopKey, Price = felt.price, Unlocked = felt.Unlocked,
                            Preview = area => PreviewFelt(area, felt)
                        });
                    }
                    break;
            }
            return items;
        }

        private void FillList()
        {
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
            cardSpans.Clear();

            var items = Items();
            var entries = new List<MenuEntry> { tabEntry };
            float width = ListSize.x - 2f * ListPadding;
            float cardWidth = (width - CardGap * (Columns - 1)) / Columns;
            float height = 0f;

            if (items.Count == 0)
            {
                var empty = CreateText(content, "Vazio", Txt.Get("Nada à venda aqui ainda."), 30, FontStyle.Normal, Color.white, Vector2.zero, new Vector2(width, 200f));
                PlaceCard(empty.rectTransform, 0f, 0f, width, 200f);
                height = 200f;
            }

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                float x = (i % Columns) * (cardWidth + CardGap);
                float top = (i / Columns) * (CardHeight + CardGap);
                var card = CreateRect(content, item.Name, Vector2.zero, Vector2.zero);
                PlaceCard(card, x, top, cardWidth, CardHeight);
                var background = card.gameObject.AddComponent<Image>();
                background.color = CardColor;
                background.raycastTarget = false;

                var preview = CreateRect(card, "Prévia", new Vector2(0f, PreviewY), new Vector2(cardWidth - 20f, PreviewBox));
                item.Preview?.Invoke(preview);
                CreateText(card, "Nome", item.Name, 28, FontStyle.Bold, Color.white, new Vector2(0f, -40f), new Vector2(cardWidth - 16f, 40f));

                if (item.Unlocked)
                    CreateText(card, "Já é seu", Txt.Get("Já é seu"), 28, FontStyle.Bold, OwnedColor, new Vector2(0f, -105f), new Vector2(cardWidth - 16f, 60f));
                else
                {
                    bool affordable = Shop.Coins >= item.Price;
                    var buy = CreateButton(card, "Comprar", Txt.Format("{0} moedas", item.Price), new Vector2(0f, -105f), new Vector2(cardWidth - 50f, 64f),
                        () => AskBuy(item), affordable ? ButtonStyle.Yellow : ButtonStyle.Gray);
                    buy.interactable = affordable;
                    var entry = Entry(buy);
                    entries.Add(entry);
                    cardSpans[entry] = (top, top + CardHeight);
                }
                height = top + CardHeight;
            }

            entries.Add(backEntry);
            if (RefreshRemoveAds()) entries.Add(removeAdsEntry);
            navigator.Configure(entries.ToArray(), backButton);

            content.sizeDelta = new Vector2(0f, height + 10f);
            content.anchoredPosition = Vector2.zero;
            scroll.velocity = Vector2.zero;
        }

        // ---- Prévias ----

        private void PreviewShirt(RectTransform area, ShirtPattern pattern)
        {
            var profile = PlayerProfile.Data;
            bool keeper = pattern.kind == ShirtPattern.Kind.Goleiro;
            var piece = CreateRect(area, "Peça", Vector2.zero, Vector2.one * PreviewBox);
            for (int i = 0; i < ShirtArt.LayerCount; i++)
            {
                var layer = (ShirtLayer)i;
                var sprite = pattern.Get(layer);
                if (sprite == null) continue;
                var offset = keeper && layer == ShirtLayer.Shadow ? ShirtArt.KeeperShadowOffset * PreviewBox : Vector2.zero;
                CreateImage(piece, layer.ToString(), sprite, ShirtArt.Tint(layer, profile.primary, profile.secondary, keeper), offset, Vector2.one * PreviewBox);
            }
        }

        private void PreviewBall(RectTransform area, BallTheme ball)
        {
            var size = Vector2.one * PreviewBox;
            if (ball.shadow != null) CreateImage(area, "Sombra", ball.shadow, Color.white, Vector2.zero, size);
            CreateImage(area, "Bola", ball.frames[0], ball.color, Vector2.zero, size);
            if (ball.shine != null) CreateImage(area, "Brilho", ball.shine, Color.white, Vector2.zero, size);
        }

        private void PreviewTable(RectTransform area, TableTheme table)
        {
            if (table.thumbnail != null)
            {
                CreateImage(area, "Miniatura", table.thumbnail, Color.white, Vector2.zero, area.sizeDelta).preserveAspect = true;
                return;
            }
            if (table.HasBoards)
            {
                // Canto, placa e a placa da trave, como no alto da mesa, sobre o feltro.
                const float tile = 80f;
                CreateImage(area, "Feltro", null, FeltColor, Vector2.zero, new Vector2(tile * 3f, tile));
                var sprites = new[] { table.corner, table.center, table.bigGoalBoard };
                for (int i = 0; i < sprites.Length; i++)
                    CreateImage(area, sprites[i].name, sprites[i], Color.white, new Vector2((i - 1) * tile, 0f), Vector2.one * tile);
                return;
            }
            // Clássica sem miniatura: moldura de madeira com o feltro dentro.
            CreateImage(area, "Madeira", null, WoodColor, Vector2.zero, new Vector2(200f, 120f));
            CreateImage(area, "Feltro", null, FeltColor, Vector2.zero, new Vector2(176f, 96f));
        }

        private void PreviewFelt(RectTransform area, FeltTheme felt)
        {
            if (felt.thumbnail != null)
            {
                CreateImage(area, "Miniatura", felt.thumbnail, Color.white, Vector2.zero, area.sizeDelta).preserveAspect = true;
                return;
            }
            // Fileiras de baixo para cima, com a linha do meio na cor das marcações.
            const int rows = 7;
            const float rowHeight = 18f;
            const float width = 200f;
            for (int row = 0; row < rows; row++)
            {
                float y = (row - (rows - 1) * 0.5f) * rowHeight;
                CreateImage(area, $"Fileira {row + 1}", null, felt.RowColor(row, rows), new Vector2(0f, y), new Vector2(width, rowHeight));
            }
            if (felt.SplitsMiddleRow(rows))
                CreateImage(area, "Meia fileira", null, felt.RowColor(rows - 1, rows), new Vector2(0f, rowHeight * 0.25f), new Vector2(width, rowHeight * 0.5f));
            CreateImage(area, "Linha do meio", null, felt.marks, Vector2.zero, new Vector2(width, 3f));
        }

        // ---- Comprar ----

        private void AskBuy(Item item)
        {
            pending = item;
            question.text = Txt.Format("Comprar {0} por {1} moedas?", item.Name, item.Price);
            navigator.enabled = false;
            dialog.SetActive(true);
        }

        private void CloseDialog()
        {
            pending = null;
            if (dialog == null) return;
            dialog.SetActive(false);
            navigator.enabled = true;
        }

        private void ConfirmBuy()
        {
            var item = pending;
            CloseDialog();
            if (item != null && Shop.Buy(item.Key, item.Price, item.Name)) FillList();
        }

        // ---- UI ----

        private Button backButton;
        private Button removeAdsButton;
        private MenuEntry removeAdsEntry;
        private bool purchasing;

        private void Build()
        {
            if (built) return;
            built = true;
            font = GameFont.Get();
            var root = (RectTransform)transform;

            LocalizedText.Attach(CreateText(root, "Título", "Loja", 52, FontStyle.Bold, Color.white, new Vector2(0f, 545f), new Vector2(680f, 90f)));
            coinsLabel = CreateText(root, "Moedas", "Moedas: 0", 34, FontStyle.Bold, CoinColor, new Vector2(0f, 478f), new Vector2(680f, 60f));

            var tabRow = CreateRect(root, "Abas", new Vector2(0f, TabY), new Vector2(TabSpacing * TabNames.Length, TabSize.y));
            var tabAreas = new RectTransform[TabNames.Length];
            for (int i = 0; i < TabNames.Length; i++)
            {
                int index = i;
                float x = (i - (TabNames.Length - 1) * 0.5f) * TabSpacing;
                tabs[i] = CreateButton(tabRow, TabNames[i], TabNames[i], new Vector2(x, 0f), TabSize, () => ShowTab(index));
                var tabLabel = tabs[i].GetComponentInChildren<Text>();
                tabLabel.fontSize = 24;
                TextFit.Shrink(tabLabel);
                LocalizedText.Attach(tabs[i].GetComponentInChildren<Text>());
                tabAreas[i] = (RectTransform)tabs[i].transform;
            }
            var previousTab = CreateHiddenButton(tabRow, "Aba anterior", () => ShowTab(tab - 1));
            var nextTab = CreateHiddenButton(tabRow, "Próxima aba", () => ShowTab(tab + 1));
            tabEntry = tabRow.gameObject.AddComponent<MenuEntry>();
            tabEntry.Configure(null, previousTab, nextTab, tabAreas);

            var panel = CreateRect(root, "Lista", ListCenter, ListSize).gameObject.AddComponent<Image>();
            var panelSprite = ButtonArt.Find(ButtonSkin.HudPanelName);
            if (panelSprite != null)
            {
                panel.sprite = panelSprite;
                panel.type = Image.Type.Sliced;
            }
            else panel.color = PanelColor;

            var viewport = CreateRect(panel.transform, "Área", Vector2.zero, Vector2.zero);
            Stretch(viewport, ListPadding);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = Color.clear;

            content = CreateRect(viewport, "Conteúdo", Vector2.zero, Vector2.zero);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);

            scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;

            backButton = CreateButton(root, "Voltar", "Voltar", new Vector2(0f, -510f), new Vector2(260f, 80f), Back, ButtonStyle.Red);
            backEntry = Entry(backButton);
            // Remover anúncios (dinheiro de verdade): ao lado do Voltar, só onde há loja e enquanto não foi comprado.
            removeAdsButton = CreateButton(root, "Remover anúncios", string.Empty, new Vector2(150f, -510f), new Vector2(320f, 80f), BuyRemoveAds,
                ButtonStyle.Yellow);
            removeAdsEntry = Entry(removeAdsButton);
            LocalizedText.Attach(backButton.GetComponentInChildren<Text>());

            navigator = gameObject.AddComponent<MenuNavigator>();
            navigator.Configure(new[] { tabEntry, backEntry }, backButton);

            BuildDialog(root);
            RefreshCoins();
        }

        private void BuildDialog(RectTransform root)
        {
            var dim = CreateRect(root, "Confirmar compra", Vector2.zero, Vector2.zero);
            Stretch(dim, 0f);
            dim.gameObject.AddComponent<Image>().color = DimColor;

            var box = CreateRect(dim, "Caixa", Vector2.zero, new Vector2(620f, 380f)).gameObject.AddComponent<Image>();
            var boxSprite = ButtonArt.Find(ButtonSkin.HudPanelName);
            if (boxSprite != null)
            {
                box.sprite = boxSprite;
                box.type = Image.Type.Sliced;
            }
            else box.color = PanelColor;

            question = CreateText(box.transform, "Pergunta", string.Empty, 34, FontStyle.Bold, Color.white, new Vector2(0f, 70f), new Vector2(560f, 160f));
            var buy = CreateButton(box.transform, "Comprar", "Comprar", new Vector2(-140f, -100f), new Vector2(240f, 80f), ConfirmBuy, ButtonStyle.Yellow);
            var cancel = CreateButton(box.transform, "Cancelar", "Cancelar", new Vector2(140f, -100f), new Vector2(240f, 80f), CloseDialog, ButtonStyle.Red);

            LocalizedText.Attach(buy.GetComponentInChildren<Text>());
            LocalizedText.Attach(cancel.GetComponentInChildren<Text>());
            dialog = dim.gameObject;
            dialog.SetActive(false);
            var dialogNavigator = dialog.AddComponent<MenuNavigator>();
            dialogNavigator.Configure(new[] { Entry(buy), Entry(cancel) }, cancel, 1);
            dialogNavigator.BackAction = CloseDialog;
        }

        private static MenuEntry Entry(Button button)
        {
            var entry = button.gameObject.AddComponent<MenuEntry>();
            entry.Configure(button);
            return entry;
        }

        private static void PlaceCard(RectTransform rect, float x, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
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

        private static Image CreateImage(Transform parent, string objectName, Sprite sprite, Color color, Vector2 position, Vector2 size)
        {
            var image = CreateRect(parent, objectName, position, size).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Text CreateText(Transform parent, string objectName, string text, int size, FontStyle style, Color color,
            Vector2 position, Vector2 box)
        {
            var label = CreateRect(parent, objectName, position, box).gameObject.AddComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.gameObject.AddComponent<Shadow>().effectColor = ShadowColor;
            TextFit.Shrink(label);
            return label;
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
