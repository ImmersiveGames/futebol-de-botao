using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>
    /// Seleção de times, no Menu, depois das Opções da partida: o campo desenhado com os dois times nos esquemas escolhidos.
    /// Em cada metade, à esquerda, escudo, nome e abreviação com as setas do time; à direita, as setas do esquema.
    /// A metade de cima fica girada 180° para o Jogador 2 (base da versão de celular, 2 jogadores na mesma tela).
    /// Contra a IA, ela sorteia time e esquema ao abrir a tela. A UI é montada no Awake.
    /// </summary>
    public sealed class TeamSelectionScreen : MonoBehaviour
    {
        // 1 unidade do campo = 100 pixels da UI (referência 720 x 1280); a tela encolhe o campo para caber.
        private const float Unit = 100f;
        private const float Wall = 0.5f;
        private const float DiscSize = 0.7f;
        private const float StripX = 2.8f;
        private const float BlockY = -FieldLayout.HalfHeight * 0.5f;

        private static readonly Color Background = new(0.05f, 0.08f, 0.06f);
        private static readonly Color FeltColor = new(0.13f, 0.45f, 0.2f);
        private static readonly Color LineColor = new(1f, 1f, 1f, 0.5f);
        private static readonly Color WallColor = new(0.35f, 0.22f, 0.12f);
        private static readonly Color GoalColor = new(0.12f, 0.12f, 0.12f);
        private static readonly Color ButtonColor = new(0.95f, 0.95f, 0.9f);
        private static readonly Color ButtonTextColor = new(0.1f, 0.1f, 0.1f);
        private static readonly Color SoftText = new(1f, 1f, 1f, 0.75f);

        [SerializeField] private MenuScreen menu;
        [SerializeField] private MatchOptions defaults;
        [Tooltip("Círculo branco usado nos botões, na bola e no escudo provisório.")]
        [SerializeField] private Sprite circle;

        private sealed class HalfView
        {
            public readonly List<(Image Body, Image Ring, Image Center)> Discs = new();
            public Image Keeper;
            public Image Crest;
            public Image CrestRing;
            public Image CrestCenter;
            public Text Name;
            public Text Abbreviation;
            public Text Scheme;
            public Text Style;
        }

        private readonly Dictionary<TeamSide, HalfView> halves = new();
        private RectTransform field;
        private Font font;
        private bool built;

        public void Configure(MenuScreen menuScreen, MatchOptions defaultOptions, Sprite circleSprite)
        {
            menu = menuScreen;
            defaults = defaultOptions;
            circle = circleSprite;
        }

        private void Awake() => Build();

        private void OnEnable() => Refresh();

        /// <summary>Abre a tela: contra a IA, sorteia o time (diferente do seu) e o esquema dela.</summary>
        public void Open()
        {
            Build();
            if (MatchSession.VsAi == true)
            {
                var catalog = TeamCatalog.Load();
                int discs = MatchSession.Options(defaults).discsPerTeam;
                int mine = MatchSession.TeamIndex(TeamSide.Bottom);
                int pick = Random.Range(0, catalog.TeamCount - 1);
                if (pick >= Wrap(mine, catalog.TeamCount)) pick++;
                MatchSession.SetTeamIndex(TeamSide.Top, pick);
                MatchSession.SetSchemeIndex(TeamSide.Top, discs, Random.Range(0, catalog.SchemeCount(discs)));
            }
            EnsureDifferentTeams();
            Refresh();
        }

        public void ChangeTeam(TeamSide side, int step)
        {
            var catalog = TeamCatalog.Load();
            int other = Wrap(MatchSession.TeamIndex(Other(side)), catalog.TeamCount);
            int index = Wrap(MatchSession.TeamIndex(side) + step, catalog.TeamCount);
            if (index == other) index = Wrap(index + step, catalog.TeamCount);
            MatchSession.SetTeamIndex(side, index);
            Refresh();
        }

        public void ChangeScheme(TeamSide side, int step)
        {
            var catalog = TeamCatalog.Load();
            int discs = MatchSession.Options(defaults).discsPerTeam;
            int count = catalog.SchemeCount(discs);
            MatchSession.SetSchemeIndex(side, discs, Wrap(MatchSession.SchemeIndex(side, discs) + step, count));
            Refresh();

            // Diagnóstico da troca de esquema (tirar depois).
            var scheme = MatchSession.Scheme(side, discs);
            var view = halves[side];
            var shown = new System.Text.StringBuilder();
            foreach (var (body, _, _) in view.Discs)
                if (body.gameObject.activeSelf) shown.Append(body.rectTransform.anchoredPosition / Unit).Append(' ');
            Debug.Log($"[Seleção] {side}: {discs} botões, esquema {MatchSession.SchemeIndex(side, discs) + 1}/{count} " +
                      $"'{(scheme != null ? scheme.name : "nulo")}' ({(scheme != null ? scheme.DisplayName : "-")}, {(scheme != null ? scheme.Count : 0)} posições). " +
                      $"Catálogo '{catalog.name}' (5: {catalog.SchemeCount(5)}, 3: {catalog.SchemeCount(3)}). Botões na tela: {shown}");
        }

        public void Back()
        {
            if (menu != null) menu.ShowOptions();
        }

        public void Play()
        {
            if (menu != null) menu.Play();
        }

        private void EnsureDifferentTeams()
        {
            var catalog = TeamCatalog.Load();
            int bottom = Wrap(MatchSession.TeamIndex(TeamSide.Bottom), catalog.TeamCount);
            int top = Wrap(MatchSession.TeamIndex(TeamSide.Top), catalog.TeamCount);
            MatchSession.SetTeamIndex(TeamSide.Bottom, bottom);
            MatchSession.SetTeamIndex(TeamSide.Top, top == bottom ? Wrap(top + 1, catalog.TeamCount) : top);
        }

        private void Update()
        {
            if (field == null || field.parent is not RectTransform parent) return;
            // O campo inteiro (com paredes e gols) cabe na tela, em pé.
            var size = parent.rect.size;
            float scale = Mathf.Min(size.x / field.sizeDelta.x, size.y / field.sizeDelta.y);
            field.localScale = Vector3.one * Mathf.Max(scale, 0.01f);
        }

        // ---- Conteúdo ----

        private void Refresh()
        {
            if (!built) return;
            var options = MatchSession.Options(defaults);
            int discs = options.discsPerTeam;

            foreach (var pair in halves)
            {
                var side = pair.Key;
                var view = pair.Value;
                var team = MatchSession.Team(side);
                var scheme = MatchSession.Scheme(side, discs);

                for (int i = 0; i < view.Discs.Count; i++)
                {
                    var (body, ring, center) = view.Discs[i];
                    bool shown = i < discs;
                    body.gameObject.SetActive(shown);
                    if (!shown) continue;
                    Vector2 position = scheme != null && i < scheme.Count
                        ? scheme.PositionFor(i, TeamSide.Bottom)
                        : Formation.Mirror((discs == 3 ? Formation.Default3 : Formation.Default5)[Mathf.Min(i, discs - 1)], TeamSide.Bottom);
                    body.rectTransform.anchoredPosition = position * Unit;
                    body.color = team.PrimaryColor;
                    ring.color = team.SecondaryColor;
                    center.color = team.PrimaryColor;
                }

                view.Keeper.gameObject.SetActive(options.hasGoalkeeper);
                view.Keeper.color = Color.Lerp(team.PrimaryColor, Color.black, 0.25f);

                // Sem escudo, um círculo nas cores do time.
                bool hasCrest = team.Crest != null;
                view.Crest.gameObject.SetActive(hasCrest);
                view.CrestRing.gameObject.SetActive(!hasCrest);
                if (hasCrest) view.Crest.sprite = team.Crest;
                view.CrestRing.color = team.SecondaryColor;
                view.CrestCenter.color = team.PrimaryColor;

                view.Name.text = team.TeamName;
                view.Abbreviation.text = team.Abbreviation;
                view.Scheme.text = scheme != null ? scheme.DisplayName : "-";
                view.Style.text = scheme != null ? scheme.Style : string.Empty;
            }
        }

        // ---- Montagem ----

        private void Build()
        {
            if (built) return;
            built = true;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var root = (RectTransform)transform;
            Stretch(CreateImage(root, "Fundo", Background, null).rectTransform);

            float width = FieldLayout.Width + 2f * Wall;
            float height = FieldLayout.Height + 2f * FieldLayout.GoalDepth;
            field = CreateRect(root, "Campo", Vector2.zero, new Vector2(width, height) * Unit);

            // Mesa: paredes, gols, feltro e linhas (as mesmas medidas da Partida).
            CreateImage(field, "Paredes", WallColor, null, Vector2.zero, new Vector2(width, FieldLayout.Height + 2f * Wall));
            foreach (float sign in new[] { -1f, 1f })
                CreateImage(field, sign < 0f ? "Gol de baixo" : "Gol de cima", GoalColor, null,
                    new Vector2(0f, sign * (FieldLayout.HalfHeight + FieldLayout.GoalDepth * 0.5f)),
                    new Vector2(FieldLayout.GoalWidth, FieldLayout.GoalDepth));
            CreateImage(field, "Feltro", FeltColor, null, Vector2.zero, new Vector2(FieldLayout.Width, FieldLayout.Height));
            CreateImage(field, "Linha do meio", LineColor, null, Vector2.zero, new Vector2(FieldLayout.Width, 0.05f));
            CreateImage(field, "Círculo central", LineColor, circle, Vector2.zero, Vector2.one * 1.8f);
            CreateImage(field, "Feltro do círculo", FeltColor, circle, Vector2.zero, Vector2.one * 1.7f);
            foreach (var defending in new[] { TeamSide.Bottom, TeamSide.Top })
            {
                float goalLine = FieldLayout.GoalLineY(defending);
                float inward = defending == TeamSide.Bottom ? 1f : -1f;
                float half = FieldLayout.AreaWidth * 0.5f;
                CreateImage(field, "Área frente", LineColor, null, new Vector2(0f, goalLine + inward * FieldLayout.AreaDepth),
                    new Vector2(FieldLayout.AreaWidth + 0.05f, 0.05f));
                foreach (float x in new[] { -half, half })
                    CreateImage(field, "Área lado", LineColor, null, new Vector2(x, goalLine + inward * FieldLayout.AreaDepth * 0.5f),
                        new Vector2(0.05f, FieldLayout.AreaDepth));
                CreateImage(field, "Marca do pênalti", LineColor, circle, FieldLayout.PenaltySpot(defending), Vector2.one * 0.15f);
            }
            CreateImage(field, "Bola", Color.white, circle, Vector2.zero, Vector2.one * 0.4f);

            // Metades: a de cima é a de baixo girada 180°.
            halves[TeamSide.Bottom] = BuildHalf(TeamSide.Bottom);
            halves[TeamSide.Top] = BuildHalf(TeamSide.Top);

            // Faixa do meio-campo.
            CreateButton(field, "Voltar", "Voltar", new Vector2(-2.45f, 0f), new Vector2(1.5f, 0.56f), Back);
            CreateButton(field, "Jogar", "Jogar", new Vector2(2.45f, 0f), new Vector2(1.5f, 0.56f), Play);

            Update();
        }

        private HalfView BuildHalf(TeamSide side)
        {
            var view = new HalfView();
            var half = CreateRect(field, side == TeamSide.Bottom ? "Time de baixo" : "Time de cima", Vector2.zero, Vector2.zero);
            if (side == TeamSide.Top) half.localRotation = Quaternion.Euler(0f, 0f, 180f);

            view.Keeper = CreateImage(half, "Goleiro", Color.white, null, new Vector2(0f, -(FieldLayout.HalfHeight - 0.35f)), new Vector2(1f, 0.25f));
            for (int i = 0; i < 5; i++)
            {
                var body = CreateImage(half, $"Botão {i + 1}", Color.white, circle, Vector2.zero, Vector2.one * DiscSize);
                var ring = CreateImage(body.rectTransform, "Anel", Color.white, circle, Vector2.zero, Vector2.one * 0.55f);
                var center = CreateImage(body.rectTransform, "Centro", Color.white, circle, Vector2.zero, Vector2.one * 0.42f);
                view.Discs.Add((body, ring, center));
            }

            // Esquerda (de quem joga deste lado): escudo, nome, abreviação e as setas do time.
            float x = -StripX;
            view.CrestRing = CreateImage(half, "Escudo provisório", Color.white, circle, new Vector2(x, BlockY + 0.8f), Vector2.one * 0.8f);
            view.CrestCenter = CreateImage(view.CrestRing.rectTransform, "Centro", Color.white, circle, Vector2.zero, Vector2.one * 0.62f);
            view.Crest = CreateImage(half, "Escudo", Color.white, null, new Vector2(x, BlockY + 0.8f), Vector2.one * 0.8f);
            view.Crest.preserveAspect = true;
            view.Name = CreateText(half, "Nome", 22, FontStyle.Bold, Color.white, new Vector2(x, BlockY + 0.13f), new Vector2(1.4f, 0.32f));
            view.Abbreviation = CreateText(half, "Abreviação", 18, FontStyle.Normal, SoftText, new Vector2(x, BlockY - 0.17f), new Vector2(1.4f, 0.26f));
            CreateButton(half, "Time anterior", "<", new Vector2(x - 0.42f, BlockY - 0.7f), Vector2.one * 0.56f, () => ChangeTeam(side, -1));
            CreateButton(half, "Próximo time", ">", new Vector2(x + 0.42f, BlockY - 0.7f), Vector2.one * 0.56f, () => ChangeTeam(side, 1));

            // Direita: o esquema tático.
            x = StripX;
            CreateText(half, "Esquema", 18, FontStyle.Normal, SoftText, new Vector2(x, BlockY + 0.9f), new Vector2(1.4f, 0.26f)).text = "Esquema";
            view.Scheme = CreateText(half, "Nome do esquema", 32, FontStyle.Bold, Color.white, new Vector2(x, BlockY + 0.5f), new Vector2(1.4f, 0.44f));
            view.Style = CreateText(half, "Estilo", 18, FontStyle.Normal, SoftText, new Vector2(x, BlockY + 0.13f), new Vector2(1.4f, 0.26f));
            CreateButton(half, "Esquema anterior", "<", new Vector2(x - 0.42f, BlockY - 0.7f), Vector2.one * 0.56f, () => ChangeScheme(side, -1));
            CreateButton(half, "Próximo esquema", ">", new Vector2(x + 0.42f, BlockY - 0.7f), Vector2.one * 0.56f, () => ChangeScheme(side, 1));
            return view;
        }

        // ---- UI ----

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

        /// <summary>Imagem em unidades do campo (posição e tamanho multiplicados por <see cref="Unit"/>).</summary>
        private static Image CreateImage(Transform parent, string objectName, Color color, Sprite sprite, Vector2 position = default, Vector2 size = default)
        {
            var image = CreateRect(parent, objectName, position * Unit, size * Unit).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Text CreateText(Transform parent, string objectName, int fontSize, FontStyle style, Color color, Vector2 position, Vector2 size)
        {
            var text = CreateRect(parent, objectName, position * Unit, size * Unit).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 10;
            text.resizeTextMaxSize = fontSize;
            text.raycastTarget = false;
            return text;
        }

        private void CreateButton(Transform parent, string objectName, string label, Vector2 position, Vector2 size, UnityAction action)
        {
            var image = CreateImage(parent, objectName, ButtonColor, null, position, size);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            var text = CreateText(image.rectTransform, "Texto", 26, FontStyle.Bold, ButtonTextColor, Vector2.zero, size);
            text.text = label;
            Stretch(text.rectTransform);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static TeamSide Other(TeamSide side) => side == TeamSide.Bottom ? TeamSide.Top : TeamSide.Bottom;

        private static int Wrap(int value, int count) => count == 0 ? 0 : ((value % count) + count) % count;
    }
}
