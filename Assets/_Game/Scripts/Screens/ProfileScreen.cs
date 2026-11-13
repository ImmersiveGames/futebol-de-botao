using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>
    /// Tela Meu time (do Menu), em três abas. Time: nome do time e sigla de 3 letras em cima e, embaixo, as abas Uniforme 1
    /// e Uniforme 2, cada uma com o botão e o goleiro pintados e as opções dela: cor principal e segunda cor (da lista Cores
    /// do time; podem ser iguais), camisa do botão e do goleiro (só as liberadas). Financeiro: saldo, ganhos e gastos e o extrato (<see cref="Finance"/>). Mesa: a mesa de casa (mesa, feltro, cor das traves que o feltro aceita e bola, só os liberados), com a mesa
    /// montada em cima. "Salvar" grava Time e Mesa e volta ao Menu; "Voltar" volta sem gravar. Tocar no nome ou na sigla
    /// abre o teclado (no celular, o do aparelho); no teclado/controle, Confirmar começa a digitar. Monta a UI sozinha.
    /// </summary>
    public sealed class ProfileScreen : MonoBehaviour
    {
        private const float NameY = 320f;
        private const float UniformTabY = 130f;
        private static readonly Vector2 UniformTabSize = new(240f, 64f);
        private const float RowTop = -110f;
        private const float TabY = 445f;
        private static readonly Vector2 TabSize = new(200f, 70f);
        private const float TabGap = 20f;
        private const int TeamTab = 0, TableTab = 1, FinanceTab = 2;
        private static readonly string[] TabNames = { "Time", "Mesa", "Financeiro" };
        private const float FinanceListTop = -10f;
        private const float FinanceListBottom = -455f;
        private const float FinanceRowHeight = 46f;
        private static readonly Color EarnColor = new(0.45f, 0.9f, 0.45f);
        private static readonly Color SpendColor = new(1f, 0.45f, 0.4f);
        private static readonly Color StripeColor = new(1f, 1f, 1f, 0.06f);
        private const float TableRowTop = -120f;
        private const float TableRowSpacing = 90f;
        private const float TablePreviewY = 150f;
        private const float TablePreviewTile = 32f;
        private const float RowSpacing = 80f;
        private const float LabelX = -200f;
        private const float LabelWidth = 280f;
        private const float ValueX = 150f;
        private const float ValueWidth = 360f;
        private const float ArrowSize = 70f;
        private const float PreviewY = 25f;
        private const float PreviewSize = 150f;
        private static readonly Color PanelColor = new(0f, 0f, 0f, 0.55f);
        private static readonly Color ShadowColor = new(0f, 0f, 0f, 0.6f);
        private static readonly Color CoinColor = new(1f, 0.85f, 0.25f);
        private static readonly Color LabelColor = new(1f, 1f, 1f, 0.85f);

        [SerializeField] private MenuScreen menu;
        [Tooltip("Recortes da mesa e da bola (Field_Sprites, Field_Marks e Ball_SpriteSheet), para a prévia da aba Mesa.")]
        [SerializeField] private Sprite[] tableSprites;

        private Font font;
        private bool built;
        private InputField nameField, abbreviationField;
        private Image primarySwatch, secondarySwatch;
        private Text primaryLabel, secondaryLabel, discLabel, keeperLabel;
        private GameObject discRow, keeperRow;
        private Image[] discLayers, keeperLayers;
        private GameObject[] tabPages;
        private Button[] tabButtons;
        private Text balanceLabel, earnedLabel, spentLabel, matchesLabel, tournamentsLabel, tutorialLabel, sponsorsLabel;
        private Button sponsorButton;
        private MenuEntry sponsorEntry;
        private RectTransform statementContent;
        private ScrollRect statementScroll;
        private Button[] uniformTabButtons;
        private MenuEntry[] teamEntries, tableEntries;
        private MenuEntry tabsEntry, backEntry, saveEntry;
        private MenuNavigator navigator;
        private TablePreview tablePreview;
        private Text tableLabel, feltLabel, goalLabel, ballLabel;
        private GameObject goalRow;
        private int tab;
        private VenueSetup home;

        // Escolhas ainda não salvas: os dois uniformes e qual aba está aberta.
        private readonly TeamUniform[] uniforms = new TeamUniform[TeamData.UniformCount];
        private int uniformTab;

        private TeamUniform Current => uniforms[uniformTab];

        public void Configure(MenuScreen menuScreen, Sprite[] tableArt)
        {
            menu = menuScreen;
            tableSprites = tableArt;
        }

        /// <summary>Abre com o que está salvo.</summary>
        public void Open()
        {
            Build();
            var data = PlayerProfile.Data;
            nameField.text = data.teamName;
            abbreviationField.text = data.abbreviation;
            for (int i = 0; i < uniforms.Length; i++)
            {
                var saved = PlayerProfile.Uniform(i);
                // Camisa que não está mais liberada: mostra (e salva) a primeira liberada.
                uniforms[i] = new TeamUniform(saved.primary, saved.secondary,
                    ShirtPattern.Resolve(ShirtPattern.Kind.Botao, saved.discPatternId)?.id ?? saved.discPatternId,
                    ShirtPattern.Resolve(ShirtPattern.Kind.Goleiro, saved.keeperPatternId)?.id ?? saved.keeperPatternId);
            }
            uniformTab = 0;
            home = Venue.Home();
            ShowTab(TeamTab);
            Refresh();
        }

        private void Save()
        {
            PlayerProfile.Save(nameField.text, abbreviationField.text, uniforms[0], uniforms[1]);
            PlayerProfile.SaveHome(home);
            Back();
        }

        /// <summary>Aba Time, Mesa ou Financeiro: o botão da aba fica apertado e o cursor passa pelas linhas dela.</summary>
        private void ShowTab(int index)
        {
            tab = (index % TabNames.Length + TabNames.Length) % TabNames.Length;
            for (int i = 0; i < tabPages.Length; i++)
            {
                tabPages[i].SetActive(i == tab);
                ButtonSkin.SetHeld(tabButtons[i], ButtonStyle.Green, i == tab);
            }
            var items = new System.Collections.Generic.List<MenuEntry> { tabsEntry };
            if (tab == TeamTab) items.AddRange(teamEntries);
            else if (tab == TableTab) items.AddRange(tableEntries);
            else if (tab == FinanceTab && sponsorButton.gameObject.activeSelf) items.Add(sponsorEntry);
            items.Add(backEntry);
            items.Add(saveEntry);
            navigator.Configure(items.ToArray(), (Button)backEntry.GetComponent<Button>(), 0);
            if (tab == TableTab) RefreshTable();
            if (tab == FinanceTab) RefreshFinance();
        }

        /// <summary>Saldo, totais, ganho por origem e o extrato (o mais novo em cima).</summary>
        private void RefreshFinance()
        {
            var data = Finance.Data;
            balanceLabel.text = Txt.Format("{0} moedas", PlayerProfile.Coins);
            earnedLabel.text = $"+{data.earned}";
            spentLabel.text = $"-{data.spent}";
            matchesLabel.text = $"+{data.fromMatches}";
            tournamentsLabel.text = $"+{data.fromTournaments}";
            tutorialLabel.text = $"+{data.fromTutorial}";
            sponsorsLabel.text = $"+{data.fromSponsors}";
            RefreshSponsor();

            for (int i = statementContent.childCount - 1; i >= 0; i--) Destroy(statementContent.GetChild(i).gameObject);
            var entries = data.entries;
            float width = StatementWidth;
            int count = entries != null ? entries.Count : 0;
            if (count == 0)
            {
                var empty = CreateText(statementContent, "Vazio", Txt.Get("Nenhuma movimentação ainda. Jogue partidas, torneios ou o tutorial para ganhar moedas."),
                    26, FontStyle.Normal, LabelColor, new Vector2(0f, -60f), new Vector2(width - 40f, 120f));
                empty.rectTransform.anchorMin = empty.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            }
            for (int i = 0; i < count; i++)
            {
                var entry = entries[count - 1 - i];
                var row = CreateRect(statementContent, "Movimentação", Vector2.zero, new Vector2(width, FinanceRowHeight));
                row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
                row.anchoredPosition = new Vector2(0f, -FinanceRowHeight * (i + 0.5f));
                if (i % 2 == 1)
                {
                    var band = row.gameObject.AddComponent<Image>();
                    band.color = StripeColor;
                    band.raycastTarget = false;
                }
                // Data (80) | descrição | valor (110), com 10 de margem nas pontas.
                var date = CreateText(row, "Data", Finance.ShortDate(entry.date), 22, FontStyle.Normal, LabelColor,
                    new Vector2(-width * 0.5f + 50f, 0f), new Vector2(80f, FinanceRowHeight));
                date.alignment = TextAnchor.MiddleLeft;
                float textWidth = width - 220f;
                var text = CreateText(row, "Descrição", entry.text, 24, FontStyle.Normal, Color.white,
                    new Vector2(-width * 0.5f + 95f + textWidth * 0.5f, 0f), new Vector2(textWidth, FinanceRowHeight));
                text.alignment = TextAnchor.MiddleLeft;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.resizeTextMaxSize = 24;
                text.resizeTextMinSize = 16;
                text.resizeTextForBestFit = true;
                var amount = CreateText(row, "Valor", entry.amount > 0 ? $"+{entry.amount}" : entry.amount.ToString(), 24, FontStyle.Bold,
                    entry.amount > 0 ? EarnColor : SpendColor, new Vector2(width * 0.5f - 65f, 0f), new Vector2(110f, FinanceRowHeight));
                amount.alignment = TextAnchor.MiddleRight;
            }
            statementContent.sizeDelta = new Vector2(0f, Mathf.Max(count, 1) * FinanceRowHeight + 10f);
            statementContent.anchoredPosition = Vector2.zero;
            statementScroll.velocity = Vector2.zero;
        }

        private void ChangeTable(int step)
        {
            home.TableId = Venue.Step(Venue.Tables(), home.TableId, step, table => table.id);
            home.GoalColor = Venue.GoalByName(home.Table, home.Felt, null);
            RefreshTable();
        }

        private void ChangeFelt(int step)
        {
            string goal = home.Table != null ? home.Table.Goal(home.GoalColor)?.name : null;
            home.FeltId = Venue.Step(Venue.Felts(), home.FeltId, step, felt => felt.id);
            // Fica com a mesma cor de trave se o feltro novo aceitar; senão, a primeira que ele aceita.
            home.GoalColor = Venue.GoalByName(home.Table, home.Felt, goal);
            RefreshTable();
        }

        private void ChangeGoal(int step)
        {
            home.GoalColor = Venue.StepGoal(home.Table, home.Felt, home.GoalColor, step);
            RefreshTable();
        }

        private void ChangeBall(int step)
        {
            home.BallId = Venue.Step(Venue.Balls(), home.BallId, step, ball => ball.id);
            RefreshTable();
        }

        private void RefreshTable()
        {
            var table = home.Table;
            var felt = home.Felt;
            var ball = home.Ball;
            tableLabel.text = table != null ? Txt.Get(table.displayName) : "-";
            feltLabel.text = felt != null ? Txt.Get(felt.displayName) : "-";
            ballLabel.text = ball != null ? Txt.Get(ball.displayName) : "-";
            // Cor da trave só nas mesas de placas (a clássica tem os gols da folha do campo).
            bool goals = table != null && table.HasBoards && table.AllowedGoalColors(felt).Count > 0;
            goalRow.SetActive(goals);
            goalLabel.text = goals ? Txt.Get(table.Goal(home.GoalColor)?.name ?? "-") : string.Empty;
            tablePreview.Paint(home);
        }

        private void Back()
        {
            if (menu != null) menu.ShowMode();
        }

        private void ShowUniform(int index)
        {
            uniformTab = Mathf.Clamp(index, 0, uniforms.Length - 1);
            Refresh();
        }

        private void ChangePrimary(int step)
        {
            Current.primary = TeamColorPalette.Load().Step(Current.primary, step);
            Refresh();
        }

        private void ChangeSecondary(int step)
        {
            Current.secondary = TeamColorPalette.Load().Step(Current.secondary, step);
            Refresh();
        }

        private void ChangeShirt(bool keeper, int step)
        {
            if (keeper) Current.keeperPatternId = ShirtPattern.Step(ShirtPattern.Kind.Goleiro, Current.keeperPatternId, step);
            else Current.discPatternId = ShirtPattern.Step(ShirtPattern.Kind.Botao, Current.discPatternId, step);
            Refresh();
        }

        private void Refresh()
        {
            for (int i = 0; i < uniformTabButtons.Length; i++)
                ButtonSkin.SetHeld(uniformTabButtons[i], ButtonStyle.Green, i == uniformTab);

            var uniform = Current;
            var palette = TeamColorPalette.Load();
            PaintSwatch(primarySwatch, primaryLabel, uniform.primary, palette.NameOf(uniform.primary));
            PaintSwatch(secondarySwatch, secondaryLabel, uniform.secondary, palette.NameOf(uniform.secondary));

            var disc = ShirtPattern.Resolve(ShirtPattern.Kind.Botao, uniform.discPatternId);
            var keeper = ShirtPattern.Resolve(ShirtPattern.Kind.Goleiro, uniform.keeperPatternId);
            discRow.SetActive(disc != null);
            keeperRow.SetActive(keeper != null);
            discLabel.text = disc != null ? Txt.Get(disc.displayName) : string.Empty;
            keeperLabel.text = keeper != null ? Txt.Get(keeper.displayName) : string.Empty;
            PaintPiece(discLayers, disc, false, uniform.primary, uniform.secondary);
            PaintPiece(keeperLayers, keeper, true, uniform.primary, uniform.secondary);
        }

        private static void PaintSwatch(Image swatch, Text label, Color color, string colorName)
        {
            swatch.color = color;
            label.text = Txt.Get(colorName);
            // Nome escuro em cor clara, claro em cor escura.
            float light = 0.299f * color.r + 0.587f * color.g + 0.114f * color.b;
            label.color = light > 0.6f ? new Color(0.1f, 0.1f, 0.1f) : Color.white;
        }

        private static void PaintPiece(Image[] layers, ShirtPattern pattern, bool keeper, Color primary, Color secondary)
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

            LocalizedText.Attach(CreateText(root, "Título", "Meu time", 52, FontStyle.Bold, Color.white, new Vector2(0f, 545f), new Vector2(680f, 90f)));

            // Abas: Time | Mesa (a escolhida fica apertada).
            // Abas: Time | Mesa | Financeiro (a escolhida fica apertada; no teclado/controle, esquerda/direita trocam).
            tabButtons = new Button[TabNames.Length];
            var tabAreas = new RectTransform[TabNames.Length];
            for (int i = 0; i < TabNames.Length; i++)
            {
                int index = i;
                float x = (i - (TabNames.Length - 1) * 0.5f) * (TabSize.x + TabGap);
                tabButtons[i] = CreateButton(root, $"Aba {TabNames[i]}", TabNames[i], new Vector2(x, TabY), TabSize, () => ShowTab(index));
                tabAreas[i] = (RectTransform)tabButtons[i].transform;
            }
            var tabsItem = CreateRect(root, "Item das abas", Vector2.zero, Vector2.zero);
            tabsEntry = tabsItem.gameObject.AddComponent<MenuEntry>();
            tabsEntry.Configure(null, CreateHiddenButton(tabsItem, "Aba anterior", () => ShowTab(tab - 1)),
                CreateHiddenButton(tabsItem, "Próxima aba", () => ShowTab(tab + 1)), tabAreas);

            // Aba Time: nome e sigla em cima; embaixo as abas Uniforme 1 | Uniforme 2, a prévia e as opções do uniforme.
            var teamPage = CreateRect(root, "Aba Time", Vector2.zero, Vector2.zero);
            tabPages = new GameObject[TabNames.Length];
            tabPages[TeamTab] = teamPage.gameObject;
            var nameEntry = CreateField(teamPage, "Nome", "Nome do time", NameY, PlayerProfile.NameLength, false, out nameField);
            var abbreviationEntry = CreateField(teamPage, "Sigla", "Sigla (3 letras)", NameY - RowSpacing, PlayerProfile.AbbreviationLength, true,
                out abbreviationField);

            uniformTabButtons = new Button[TeamData.UniformCount];
            for (int i = 0; i < uniformTabButtons.Length; i++)
            {
                int index = i;
                float x = (i - (uniformTabButtons.Length - 1) * 0.5f) * (UniformTabSize.x + 20f);
                uniformTabButtons[i] = CreateButton(teamPage, $"Aba Uniforme {i + 1}", $"Uniforme {i + 1}", new Vector2(x, UniformTabY),
                    UniformTabSize, () => ShowUniform(index));
            }
            var uniformTabsEntry = CreateRect(teamPage, "Item dos uniformes", Vector2.zero, Vector2.zero).gameObject.AddComponent<MenuEntry>();
            uniformTabsEntry.Configure(null, uniformTabButtons[0], uniformTabButtons[1],
                (RectTransform)uniformTabButtons[0].transform, (RectTransform)uniformTabButtons[1].transform);

            // Prévia: o botão e o goleiro do uniforme aberto, direto sobre o fundo da tela.
            var preview = CreateRect(teamPage, "Prévia", new Vector2(0f, PreviewY), new Vector2(460f, 200f));
            discLayers = CreatePiece(preview, "Botão", new Vector2(-110f, 0f), false);
            keeperLayers = CreatePiece(preview, "Goleiro", new Vector2(110f, 0f), true);

            var primaryEntry = CreateArrowRow(teamPage, "Cor principal", RowY(0), ChangePrimary, true, out primaryLabel, out primarySwatch, out _);
            var secondaryEntry = CreateArrowRow(teamPage, "Segunda cor", RowY(1), ChangeSecondary, true, out secondaryLabel, out secondarySwatch, out _);
            var discEntry = CreateArrowRow(teamPage, "Camisa do botão", RowY(2), step => ChangeShirt(false, step), false, out discLabel, out _, out discRow);
            var keeperEntry = CreateArrowRow(teamPage, "Camisa do goleiro", RowY(3), step => ChangeShirt(true, step), false, out keeperLabel, out _, out keeperRow);
            teamEntries = new[] { nameEntry, abbreviationEntry, uniformTabsEntry, primaryEntry, secondaryEntry, discEntry, keeperEntry };

            // Aba Mesa: a mesa de casa montada e as setas de cada parte.
            var tablePage = CreateRect(root, "Aba Mesa", Vector2.zero, Vector2.zero);
            tabPages[TableTab] = tablePage.gameObject;
            tablePreview = new TablePreview(tablePage, new Vector2(0f, TablePreviewY), TablePreviewTile, tableSprites);
            var tableEntry = CreateArrowRow(tablePage, "Mesa", TableRowY(0), ChangeTable, false, out tableLabel, out _, out _);
            var feltEntry = CreateArrowRow(tablePage, "Feltro", TableRowY(1), ChangeFelt, false, out feltLabel, out _, out _);
            var goalEntry = CreateArrowRow(tablePage, "Cor da trave", TableRowY(2), ChangeGoal, false, out goalLabel, out _, out goalRow);
            var ballEntry = CreateArrowRow(tablePage, "Bola", TableRowY(3), ChangeBall, false, out ballLabel, out _, out _);
            tableEntries = new[] { tableEntry, feltEntry, goalEntry, ballEntry };

            BuildFinance(root);

            var back = CreateButton(root, "Voltar", "Voltar", new Vector2(-180f, -510f), new Vector2(260f, 80f), Back, ButtonStyle.Red);
            var save = CreateButton(root, "Salvar", "Salvar", new Vector2(150f, -510f), new Vector2(300f, 80f), Save, ButtonStyle.Yellow);
            backEntry = Entry(back);
            saveEntry = Entry(save);

            navigator = gameObject.AddComponent<MenuNavigator>();
        }

        /// <summary>Largura do extrato: a mesma das linhas da aba Time (do rótulo até a seta da direita).</summary>
        private const float StatementWidth = ValueX + ValueWidth * 0.5f - (LabelX - LabelWidth * 0.5f) + 20f;

        /// <summary>Aba Financeiro: saldo grande, resumo (ganhos por origem e gastos) e o extrato com rolagem.</summary>
        private void BuildFinance(RectTransform root)
        {
            var page = CreateRect(root, "Aba Financeiro", Vector2.zero, Vector2.zero);
            tabPages[FinanceTab] = page.gameObject;

            LocalizedText.Attach(CreateText(page, "Rótulo do saldo", "Saldo", 28, FontStyle.Bold, LabelColor, new Vector2(0f, 345f), new Vector2(600f, 40f)));
            balanceLabel = CreateText(page, "Saldo", string.Empty, 56, FontStyle.Bold, CoinColor, new Vector2(0f, 290f), new Vector2(680f, 70f));

            earnedLabel = SummaryRow(page, "Ganho total", 228f, false, EarnColor);
            matchesLabel = SummaryRow(page, "Partidas", 193f, true, Color.white);
            tournamentsLabel = SummaryRow(page, "Torneios", 160f, true, Color.white);
            tutorialLabel = SummaryRow(page, "Tutorial", 127f, true, Color.white);
            sponsorsLabel = SummaryRow(page, "Patrocínios", 94f, true, Color.white);
            spentLabel = SummaryRow(page, "Gasto total (loja)", 52f, false, SpendColor);

            var header = CreateText(page, "Extrato", "Extrato", 32, FontStyle.Bold, CoinColor, new Vector2(LabelX, 0f), new Vector2(LabelWidth, 44f));
            header.alignment = TextAnchor.MiddleLeft;
            LocalizedText.Attach(header);

            // Patrocínio: na linha do "Extrato", à direita (só onde há anúncios).
            float rightEdge = ValueX + ValueWidth * 0.5f;
            sponsorButton = CreateButton(page, "Vender patrocínio", string.Empty, new Vector2(rightEdge - 170f, 2f), new Vector2(340f, 56f),
                SellSponsor, ButtonStyle.Yellow);
            sponsorEntry = Entry(sponsorButton);

            // Lista com rolagem (arrastar ou roda do mouse).
            float centerX = (LabelX - LabelWidth * 0.5f + ValueX + ValueWidth * 0.5f) * 0.5f;
            float height = FinanceListTop - FinanceListBottom - 25f;
            var viewport = CreateRect(page, "Lista", new Vector2(centerX, FinanceListTop - 25f - height * 0.5f),
                new Vector2(StatementWidth, height));
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0.25f);
            viewport.gameObject.AddComponent<RectMask2D>();
            statementContent = CreateRect(viewport, "Conteúdo", Vector2.zero, Vector2.zero);
            statementContent.anchorMin = new Vector2(0f, 1f);
            statementContent.anchorMax = new Vector2(1f, 1f);
            statementContent.pivot = new Vector2(0.5f, 1f);
            statementScroll = viewport.gameObject.AddComponent<ScrollRect>();
            statementScroll.content = statementContent;
            statementScroll.viewport = viewport;
            statementScroll.horizontal = false;
            statementScroll.movementType = ScrollRect.MovementType.Clamped;
            statementScroll.scrollSensitivity = 30f;
        }

        /// <summary>Botão do patrocínio: quantos faltam hoje; esgotado fica cinza até amanhã.</summary>
        private void RefreshSponsor()
        {
            bool shown = Ads.RewardedAvailable;
            if (sponsorButton.gameObject.activeSelf != shown)
            {
                sponsorButton.gameObject.SetActive(shown);
                if (tab == FinanceTab) ShowTab(FinanceTab);
            }
            int left = Sponsorship.RemainingToday;
            sponsorButton.interactable = left > 0;
            sponsorButton.GetComponentInChildren<Text>().text = left > 0
                ? Txt.Format("Vender patrocínio (+{0}) {1}/{2}", Sponsorship.Reward, left, Sponsorship.PerDay)
                : Txt.Get("Patrocínios de hoje vendidos");
        }

        private void SellSponsor()
        {
            sponsorButton.interactable = false;
            Sponsorship.Sell(_ =>
            {
                if (this != null && isActiveAndEnabled) RefreshFinance();
            });
        }

        /// <summary>Linha do resumo: rótulo à esquerda (recuado nas origens) e valor à direita.</summary>
        private Text SummaryRow(Transform parent, string label, float y, bool indent, Color color)
        {
            var caption = CreateText(parent, "Rótulo", label, indent ? 24 : 28, indent ? FontStyle.Normal : FontStyle.Bold, LabelColor,
                new Vector2(LabelX + (indent ? 40f : 0f), y), new Vector2(LabelWidth, 40f));
            caption.alignment = TextAnchor.MiddleLeft;
            LocalizedText.Attach(caption);
            var value = CreateText(parent, "Valor", string.Empty, indent ? 24 : 28, FontStyle.Bold, color,
                new Vector2(ValueX + ValueWidth * 0.5f - 100f, y), new Vector2(200f, 40f));
            value.alignment = TextAnchor.MiddleRight;
            return value;
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

        private static float TableRowY(int row) => TableRowTop - row * TableRowSpacing;

        private Text CreateRowLabel(Transform parent, string label, float y)
        {
            var text = CreateText(parent, "Rótulo", label, 28, FontStyle.Bold, LabelColor, new Vector2(LabelX, y), new Vector2(LabelWidth, 70f));
            text.alignment = TextAnchor.MiddleLeft;
            LocalizedText.Attach(text);
            return text;
        }

        /// <summary>Linha com um campo de texto; Confirmar (teclado/controle) começa a digitar.</summary>
        private MenuEntry CreateField(Transform parent, string label, string placeholder, float y, int limit, bool upper, out InputField field)
        {
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
            LocalizedText.Attach(hint);

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
        private MenuEntry CreateArrowRow(Transform parent, string label, float y, UnityAction<int> change, bool swatch,
            out Text value, out Image swatchImage, out GameObject rowObject)
        {
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
            LocalizedText.Attach(text);
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
