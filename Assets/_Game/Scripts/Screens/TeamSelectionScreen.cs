using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>
    /// Seleção de times, no Menu, depois das Opções da partida: o campo desenhado com os dois times nos esquemas escolhidos.
    /// Em cada metade, à esquerda, as setas do time lado a lado com o nome e a abreviação à direita e, embaixo, as da segunda
    /// cor e as da camisa;
    /// à direita, as setas do esquema; as setas da camisa do goleiro ficam uma de cada lado dele.
    /// A metade de cima fica girada 180° para o Jogador 2 (base da versão de celular, 2 jogadores na mesma tela); contra a IA,
    /// as opções dela ficam em pé, como as do jogador.
    /// Contra a IA, ela sorteia time e esquema ao abrir a tela. A UI é montada no Awake.
    /// Cada metade tem o seu Pronto e a partida começa com os dois prontos (contra a IA, só o de baixo). Com dispositivos
    /// separados (tela Controles), cada jogador tem o próprio cursor e só mexe na sua metade; revezando ou contra a IA,
    /// um cursor só passa pelas duas. Pronto marcado trava as setas daquele time; Q/B desmarca.
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
        private static readonly Color ReadyColor = new(0.55f, 0.9f, 0.55f);
        private static readonly Color SecondPlayerFrame = new(0.45f, 0.85f, 1f, 1f);
        private static readonly Vector2 MiddleButtonSize = new(1.5f, 0.5f);
        private const float MiddleButtonX = 2.45f;
        private const float MiddleButtonY = -0.32f;
        // Time, segunda cor e camisa, uma linha embaixo da outra: as duas setas lado a lado e o texto à direita delas.
        private const float TeamRowY = BlockY + 0.35f;
        private const float SecondaryY = BlockY - 0.35f;
        private const float ShirtY = BlockY - 1.05f;
        private const float RowArrowSize = 0.5f;
        private static readonly float[] RowArrowX = { -StripX - 0.65f, -StripX - 0.1f };
        private const float RowLabelLeft = -StripX + 0.25f;
        private const float RowLabelWidth = 1.3f;
        private const float KeeperArrowX = 0.85f;

        [SerializeField] private MenuScreen menu;
        [SerializeField] private MatchOptions defaults;
        [Tooltip("Círculo branco usado nos botões e na bola sem a arte.")]
        [SerializeField] private Sprite circle;
        [Tooltip("Recortes da mesa e da bola (Field_Sprites, Field_Marks e Ball_SpriteSheet), achados pelo nome. " +
                 "Sem eles, o campo é desenhado com formas lisas.")]
        [SerializeField] private Sprite[] tableSprites;

        /// <summary>Botão ou goleiro: as camadas da camisa (com a arte) ou as formas lisas do protótipo.</summary>
        private sealed class PieceView
        {
            public RectTransform Root;
            public Image[] Layers;
            public Image Body;
            public Image Ring;
            public Image Center;
        }

        private sealed class HalfView
        {
            public readonly List<PieceView> Discs = new();
            public PieceView Keeper;
            public RectTransform KeeperArrows;
            public Text Name;
            public Text Abbreviation;
            public Text Scheme;
            public Text Style;
            public MenuEntry TeamItem;
            public MenuEntry SchemeItem;
            public MenuEntry SecondaryItem;
            public MenuEntry ShirtItem;
            public MenuEntry KeeperItem;
            public RectTransform Root;
            /// <summary>Setas e textos da metade, com a posição de cada um no time de baixo.</summary>
            public RectTransform Controls;
            public readonly List<(RectTransform rect, Vector2 position)> ControlPositions = new();
            public Button Ready;
            public MenuEntry ReadyItem;
            public Button[] Arrows;
            public MenuNavigator Navigator;
            public bool IsReady;
        }

        private readonly Dictionary<TeamSide, HalfView> halves = new();
        private RectTransform field;
        private GameObject bigGoals;
        private GameObject smallGoals;
        private GameObject bigPoles;
        private GameObject smallPoles;
        private Font font;
        private bool built;
        private bool starting;
        private MenuNavigator sharedNavigator;
        private ShirtArt shirts;

        public void Configure(MenuScreen menuScreen, MatchOptions defaultOptions, Sprite circleSprite, Sprite[] tableArt)
        {
            menu = menuScreen;
            defaults = defaultOptions;
            circle = circleSprite;
            tableSprites = tableArt;
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
                if (shirts != null)
                {
                    MatchSession.SetDiscPattern(TeamSide.Top, Random.Range(0, Mathf.Max(1, shirts.disc.PatternCount)));
                    MatchSession.SetKeeperPattern(TeamSide.Top, Random.Range(0, Mathf.Max(1, shirts.keeper.PatternCount)));
                }
                MatchSession.SetSecondaryIndex(TeamSide.Top, 0);
            }
            EnsureDifferentTeams();

            starting = false;
            foreach (var pair in halves) SetReady(pair.Key, false);
            halves[TeamSide.Top].Ready.gameObject.SetActive(MatchSession.VsAi != true);
            SetUpright(halves[TeamSide.Top], MatchSession.VsAi == true);

            // Um cursor por jogador só com um dispositivo para cada um.
            bool separate = MatchSession.VsAi != true && !MatchSession.SharedDevice && MatchSession.Player2Device != null;
            sharedNavigator.enabled = false;
            foreach (var pair in halves) pair.Value.Navigator.enabled = false;
            sharedNavigator.enabled = !separate;
            foreach (var pair in halves) pair.Value.Navigator.enabled = separate;
            Refresh();
        }

        public void ChangeTeam(TeamSide side, int step)
        {
            var catalog = TeamCatalog.Load();
            int other = Wrap(MatchSession.TeamIndex(Other(side)), catalog.TeamCount);
            int index = Wrap(MatchSession.TeamIndex(side) + step, catalog.TeamCount);
            if (index == other) index = Wrap(index + step, catalog.TeamCount);
            MatchSession.SetTeamIndex(side, index);
            MatchSession.SetSecondaryIndex(side, 0);
            Refresh();
        }

        /// <summary>Próxima segunda cor do time (a primeira é a do próprio time).</summary>
        public void ChangeSecondary(TeamSide side, int step)
        {
            int count = TeamCatalog.Load().SecondaryOptions(MatchSession.Team(side)).Length;
            MatchSession.SetSecondaryIndex(side, Wrap(MatchSession.SecondaryIndex(side) + step, count));
            Refresh();
        }

        public void ChangeScheme(TeamSide side, int step)
        {
            var catalog = TeamCatalog.Load();
            int discs = MatchSession.Options(defaults).discsPerTeam;
            int count = catalog.SchemeCount(discs);
            MatchSession.SetSchemeIndex(side, discs, Wrap(MatchSession.SchemeIndex(side, discs) + step, count));
            Refresh();
        }

        /// <summary>Próxima camisa dos botões (ou do goleiro) do time.</summary>
        public void ChangeShirt(TeamSide side, bool keeper, int step)
        {
            int count = shirts == null ? 1 : Mathf.Max(1, (keeper ? shirts.keeper : shirts.disc).PatternCount);
            if (keeper) MatchSession.SetKeeperPattern(side, Wrap(MatchSession.KeeperPattern(side) + step, count));
            else MatchSession.SetDiscPattern(side, Wrap(MatchSession.DiscPattern(side) + step, count));
            Refresh();
        }

        public void Back()
        {
            if (menu != null) menu.ShowOptions();
        }

        public void Play()
        {
            GameSave.Save("selecao de times");
            if (menu != null) menu.Play();
        }

        /// <summary>Pronto do time: marca ou desmarca; com todos prontos, vai para a Partida.</summary>
        public void ToggleReady(TeamSide side)
        {
            var view = halves[side];
            SetReady(side, !view.IsReady);
            view.Navigator.Select(view.ReadyItem);
            sharedNavigator.Select(view.ReadyItem);

            bool bottomReady = halves[TeamSide.Bottom].IsReady;
            bool topReady = MatchSession.VsAi == true || halves[TeamSide.Top].IsReady;
            if (bottomReady && topReady && !starting)
            {
                starting = true;
                Play();
            }
        }

        private void SetReady(TeamSide side, bool ready)
        {
            var view = halves[side];
            view.IsReady = ready;
            foreach (var arrow in view.Arrows) arrow.interactable = !ready;
            // Pronto marcado: o botão fica verde (já confirmou); desmarcado volta ao amarelo de ação.
            if (!ButtonSkin.Apply(view.Ready, ready ? ButtonStyle.Green : ButtonStyle.Yellow))
                ((Image)view.Ready.targetGraphic).color = ready ? ReadyColor : ButtonColor;
            view.Ready.GetComponentInChildren<Text>().text = ready ? "Pronto!" : "Pronto";
        }

        /// <summary>Q/B de um jogador com cursor próprio: desmarca o Pronto; sem Pronto, o Jogador 1 volta às Opções.</summary>
        private void SideBack(TeamSide side)
        {
            if (starting) return;
            if (halves[side].IsReady) SetReady(side, false);
            else if (side == TeamSide.Bottom) Back();
        }

        private void EnsureDifferentTeams()
        {
            var catalog = TeamCatalog.Load();
            int bottom = Wrap(MatchSession.TeamIndex(TeamSide.Bottom), catalog.TeamCount);
            int top = Wrap(MatchSession.TeamIndex(TeamSide.Top), catalog.TeamCount);
            MatchSession.SetTeamIndex(TeamSide.Bottom, bottom);
            if (top == bottom)
            {
                top = Wrap(top + 1, catalog.TeamCount);
                MatchSession.SetSecondaryIndex(TeamSide.Top, 0);
            }
            MatchSession.SetTeamIndex(TeamSide.Top, top);
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
                var secondary = MatchSession.SecondaryColor(side);
                var scheme = MatchSession.Scheme(side, discs);

                for (int i = 0; i < view.Discs.Count; i++)
                {
                    var disc = view.Discs[i];
                    bool shown = i < discs;
                    disc.Root.gameObject.SetActive(shown);
                    if (!shown) continue;
                    Vector2 position = scheme != null && i < scheme.Count
                        ? scheme.PositionFor(i, TeamSide.Bottom)
                        : Formation.Mirror((discs == 3 ? Formation.Default3 : Formation.Default5)[Mathf.Min(i, discs - 1)], TeamSide.Bottom);
                    disc.Root.anchoredPosition = position * Unit;
                    PaintPiece(disc, shirts != null ? shirts.disc : null, team, secondary, MatchSession.DiscPattern(side));
                    if (disc.Layers == null) disc.Center.color = team.PrimaryColor;
                }

                view.Keeper.Root.gameObject.SetActive(options.hasGoalkeeper);
                SetActive(view.KeeperArrows.gameObject, options.hasGoalkeeper && shirts != null);
                SetActive(bigGoals, options.hasGoalkeeper);
                SetActive(bigPoles, options.hasGoalkeeper);
                SetActive(smallGoals, !options.hasGoalkeeper);
                SetActive(smallPoles, !options.hasGoalkeeper);
                PaintPiece(view.Keeper, shirts != null ? shirts.keeper : null, team, secondary, MatchSession.KeeperPattern(side));
                if (view.Keeper.Layers == null) view.Keeper.Body.color = Color.Lerp(team.PrimaryColor, Color.black, 0.25f);

                view.Name.text = team.TeamName;
                view.Abbreviation.text = team.Abbreviation;
                view.Scheme.text = scheme != null ? scheme.DisplayName : "-";
                view.Style.text = scheme != null ? scheme.Style : string.Empty;
            }
        }

        private static void PaintPiece(PieceView piece, ShirtArt.Sheet sheet, TeamData team, Color secondary, int pattern)
        {
            if (piece.Layers == null)
            {
                piece.Body.color = team.PrimaryColor;
                if (piece.Ring != null) piece.Ring.color = secondary;
                return;
            }
            for (int i = 0; i < piece.Layers.Length; i++)
            {
                var layer = (ShirtLayer)i;
                var image = piece.Layers[i];
                image.sprite = sheet.Get(layer, pattern);
                image.color = ShirtArt.Tint(layer, team.PrimaryColor, secondary);
                image.enabled = image.sprite != null;
            }
        }

        // ---- Montagem ----

        private void Build()
        {
            if (built) return;
            built = true;
            font = GameFont.Get();
            shirts = ShirtArt.Load();

            var root = (RectTransform)transform;
            Stretch(CreateImage(root, "Fundo", Background, null).rectTransform);

            var art = new Dictionary<string, Sprite>();
            if (tableSprites != null)
                foreach (var sprite in tableSprites)
                    if (sprite != null) art[sprite.name] = sprite;
            bool hasArt = System.Array.TrueForAll(RequiredArt, art.ContainsKey);
            if (hasArt) BuildTableArt(root, art);
            else BuildPlainTable(root);

            // Metades: a de cima é a de baixo girada 180°.
            halves[TeamSide.Bottom] = BuildHalf(TeamSide.Bottom);
            halves[TeamSide.Top] = BuildHalf(TeamSide.Top);

            // Traves e rede por cima dos botões, como na Partida.
            if (hasArt)
            {
                bigPoles = CreateGoalPieces(field, "Traves grandes", art, "Pole");
                smallPoles = CreateGoalPieces(field, "Traves pequenas", art, "Small_Pole");
            }

            // Faixa do meio-campo: o Voltar é do Jogador 1 (fica na metade de baixo); cada Pronto à direita do seu dono.
            var top = halves[TeamSide.Top];
            var bottom = halves[TeamSide.Bottom];
            var back = CreateButton(bottom.Root, "Voltar", "Voltar", new Vector2(-MiddleButtonX, MiddleButtonY), MiddleButtonSize, Back, ButtonStyle.Red);
            var backItem = back.gameObject.AddComponent<MenuEntry>();
            backItem.Configure(back);

            // Teclado/controle, um cursor só (contra a IA ou revezando): de cima para baixo na tela; começa no time de baixo.
            sharedNavigator = gameObject.AddComponent<MenuNavigator>();
            sharedNavigator.Configure(new[]
            {
                top.KeeperItem, top.ShirtItem, top.SecondaryItem, top.TeamItem, top.SchemeItem, top.ReadyItem, backItem,
                bottom.ReadyItem, bottom.TeamItem, bottom.SchemeItem, bottom.SecondaryItem, bottom.ShirtItem, bottom.KeeperItem,
            }, back, 8, 1);

            // Um cursor por jogador, cada um na sua metade (começa no time).
            bottom.Navigator = bottom.Root.gameObject.AddComponent<MenuNavigator>();
            bottom.Navigator.Configure(new[] { backItem, bottom.ReadyItem, bottom.TeamItem, bottom.SchemeItem, bottom.SecondaryItem, bottom.ShirtItem, bottom.KeeperItem }, back, 2, 1);
            bottom.Navigator.BackAction = () => SideBack(TeamSide.Bottom);
            top.Navigator = top.Root.gameObject.AddComponent<MenuNavigator>();
            top.Navigator.Configure(new[] { top.ReadyItem, top.TeamItem, top.SchemeItem, top.SecondaryItem, top.ShirtItem, top.KeeperItem }, null, 1, 2);
            top.Navigator.SetFrameColor(SecondPlayerFrame);
            top.Navigator.BackAction = () => SideBack(TeamSide.Top);
            top.Navigator.enabled = bottom.Navigator.enabled = false;

            Update();
        }

        // Mesa em pixel art: 9 x 13 tiles (1 tile = 1 unidade do campo), com as mesmas peças da Partida.
        private static readonly string[] RequiredArt =
        {
            "Wall_Fill", "Wall_Top", "Wall_Bottom", "Wall_Left", "Wall_Right",
            "Top_Corner_L", "Top_Corner_R", "Bottom_Corner_L", "Bottom_Corner_R", "Field_Braight", "Field_Dark",
        };
        private static readonly string[] GoalPieces = { "Corner_Left", "Center", "Corner_Right" };
        private static readonly Vector2Int WoodTiles = new(45, 29);
        private static readonly Color BallShadowColor = new(0f, 0f, 0f, 0.4f);

        private void BuildTableArt(RectTransform root, Dictionary<string, Sprite> art)
        {
            float halfW = FieldLayout.HalfWidth + 0.5f, halfH = FieldLayout.HalfHeight + 0.5f;
            field = CreateRect(root, "Campo", Vector2.zero, new Vector2(halfW, halfH) * 2f * Unit);

            // Madeira por toda a tela, na mesma escala da mesa.
            CreateTiled(field, "Madeira", art["Wall_Fill"], Vector2.zero, WoodTiles);

            // Moldura: faixas das paredes e os 4 cantos.
            var horizontal = new Vector2(FieldLayout.Width, 1f);
            var vertical = new Vector2(1f, FieldLayout.Height);
            CreateTiled(field, "Parede de cima", art["Wall_Top"], new Vector2(0f, halfH), horizontal);
            CreateTiled(field, "Parede de baixo", art["Wall_Bottom"], new Vector2(0f, -halfH), horizontal);
            CreateTiled(field, "Parede da esquerda", art["Wall_Left"], new Vector2(-halfW, 0f), vertical);
            CreateTiled(field, "Parede da direita", art["Wall_Right"], new Vector2(halfW, 0f), vertical);
            CreateImage(field, "Canto de cima esquerdo", Color.white, art["Top_Corner_L"], new Vector2(-halfW, halfH), Vector2.one);
            CreateImage(field, "Canto de cima direito", Color.white, art["Top_Corner_R"], new Vector2(halfW, halfH), Vector2.one);
            CreateImage(field, "Canto de baixo esquerdo", Color.white, art["Bottom_Corner_L"], new Vector2(-halfW, -halfH), Vector2.one);
            CreateImage(field, "Canto de baixo direito", Color.white, art["Bottom_Corner_R"], new Vector2(halfW, -halfH), Vector2.one);

            // Feltro: faixas horizontais, clara embaixo (o meio fica claro).
            int rows = Mathf.RoundToInt(FieldLayout.Height);
            for (int row = 0; row < rows; row++)
                CreateTiled(field, $"Feltro {row + 1}", art[row % 2 == 0 ? "Field_Braight" : "Field_Dark"],
                    new Vector2(0f, -FieldLayout.HalfHeight + 0.5f + row), horizontal);

            if (art.TryGetValue("Field_Marks", out var marks))
                CreateImage(field, "Marcações", Color.white, marks, Vector2.zero, new Vector2(FieldLayout.Width, FieldLayout.Height));

            // Fundo dos gols embaixo da bola e dos botões (grande com goleiro, pequeno sem).
            bigGoals = CreateGoalPieces(field, "Gols grandes", art, "Goal");
            smallGoals = CreateGoalPieces(field, "Gols pequenos", art, "Small_Goal");

            // Bola no meio: sombra, desenho e luz, 1 tile cada.
            if (art.TryGetValue("Ball_Shadow", out var shadow)) CreateImage(field, "Sombra da bola", BallShadowColor, shadow, Vector2.zero, Vector2.one);
            if (art.TryGetValue("Ball_Frame_1", out var ball)) CreateImage(field, "Bola", Color.white, ball, Vector2.zero, Vector2.one);
            else CreateImage(field, "Bola", Color.white, circle, Vector2.zero, Vector2.one * 0.4f);
            if (art.TryGetValue("Ball_Light", out var light)) CreateImage(field, "Luz da bola", Color.white, light, Vector2.zero, Vector2.one);
        }

        /// <summary>Um par de gols (cima e baixo) com 3 peças cada, no lugar dos 3 tiles do meio da moldura. Nulo se faltar recorte.</summary>
        private static GameObject CreateGoalPieces(Transform parent, string groupName, Dictionary<string, Sprite> art, string prefix)
        {
            foreach (var end in new[] { "Top", "Bottom" })
                foreach (var piece in GoalPieces)
                    if (!art.ContainsKey($"{prefix}_{end}_{piece}")) return null;

            var group = CreateRect(parent, groupName, Vector2.zero, Vector2.zero);
            float y = FieldLayout.HalfHeight + 0.5f;
            foreach (var (end, sign) in new[] { ("Top", 1f), ("Bottom", -1f) })
                for (int i = 0; i < GoalPieces.Length; i++)
                    CreateImage(group, $"{end} {i + 1}", Color.white, art[$"{prefix}_{end}_{GoalPieces[i]}"], new Vector2(i - 1, sign * y), Vector2.one);
            return group.gameObject;
        }

        /// <summary>Imagem repetida tile a tile (32 px = 1 unidade do campo = <see cref="Unit"/> da UI).</summary>
        private static Image CreateTiled(Transform parent, string objectName, Sprite sprite, Vector2 position, Vector2 tiles)
        {
            var image = CreateImage(parent, objectName, Color.white, sprite, position, tiles);
            image.type = Image.Type.Tiled;
            // Image.Tiled desenha cada tile com (pixels / PPU) x 100 unidades da UI; Unit = 100, então 1 tile = 1 unidade.
            image.pixelsPerUnitMultiplier = 100f / Unit;
            return image;
        }

        // Desenho liso do protótipo, para quando a arte da mesa não está no projeto.
        private void BuildPlainTable(RectTransform root)
        {
            float width = FieldLayout.Width + 2f * Wall;
            float height = FieldLayout.Height + 2f * FieldLayout.GoalDepth;
            field = CreateRect(root, "Campo", Vector2.zero, new Vector2(width, height) * Unit);

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
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null) target.SetActive(active);
        }

        private HalfView BuildHalf(TeamSide side)
        {
            var view = new HalfView();
            var half = CreateRect(field, side == TeamSide.Bottom ? "Time de baixo" : "Time de cima", Vector2.zero, Vector2.zero);
            if (side == TeamSide.Top) half.localRotation = Quaternion.Euler(0f, 0f, 180f);
            view.Root = half;

            var keeperPosition = new Vector2(0f, -(FieldLayout.HalfHeight - 0.35f));
            view.Keeper = CreatePiece(half, "Goleiro", keeperPosition, side, true);
            for (int i = 0; i < 5; i++) view.Discs.Add(CreatePiece(half, $"Botão {i + 1}", Vector2.zero, side, false));

            // Setas e textos num grupo próprio: contra a IA, o grupo da metade de cima fica em pé (ver SetUpright).
            var controls = CreateRect(half, "Opções", Vector2.zero, Vector2.zero);
            view.Controls = controls;

            // Esquerda (de quem joga deste lado): as setas do time com o nome e a abreviação à direita.
            var previousTeam = CreateButton(controls, "Time anterior", "<", new Vector2(RowArrowX[0], TeamRowY), Vector2.one * RowArrowSize, () => ChangeTeam(side, -1));
            var nextTeam = CreateButton(controls, "Próximo time", ">", new Vector2(RowArrowX[1], TeamRowY), Vector2.one * RowArrowSize, () => ChangeTeam(side, 1));
            view.Name = CreateRowLabel(controls, "Nome", 22, FontStyle.Bold, Color.white, TeamRowY + 0.13f, 0.32f);
            view.Abbreviation = CreateRowLabel(controls, "Abreviação", 18, FontStyle.Normal, SoftText, TeamRowY - 0.15f, 0.26f);
            view.TeamItem = CreateRect(controls, "Item do time", Vector2.zero, Vector2.zero).gameObject.AddComponent<MenuEntry>();
            view.TeamItem.Configure(null, previousTeam, nextTeam, view.Name.rectTransform,
                view.Abbreviation.rectTransform, (RectTransform)previousTeam.transform, (RectTransform)nextTeam.transform);

            // Direita: o esquema tático.
            float x = StripX;
            var schemeTitle = CreateText(controls, "Esquema", 18, FontStyle.Normal, SoftText, new Vector2(x, BlockY + 0.9f), new Vector2(1.4f, 0.26f));
            schemeTitle.text = "Esquema";
            view.Scheme = CreateText(controls, "Nome do esquema", 32, FontStyle.Bold, Color.white, new Vector2(x, BlockY + 0.5f), new Vector2(1.4f, 0.44f));
            view.Style = CreateText(controls, "Estilo", 18, FontStyle.Normal, SoftText, new Vector2(x, BlockY + 0.13f), new Vector2(1.4f, 0.26f));
            var previousScheme = CreateButton(controls, "Esquema anterior", "<", new Vector2(x - 0.42f, BlockY - 0.7f), Vector2.one * 0.56f, () => ChangeScheme(side, -1));
            var nextScheme = CreateButton(controls, "Próximo esquema", ">", new Vector2(x + 0.42f, BlockY - 0.7f), Vector2.one * 0.56f, () => ChangeScheme(side, 1));
            view.SchemeItem = CreateRect(controls, "Item do esquema", Vector2.zero, Vector2.zero).gameObject.AddComponent<MenuEntry>();
            view.SchemeItem.Configure(null, previousScheme, nextScheme, schemeTitle.rectTransform, view.Scheme.rectTransform,
                view.Style.rectTransform, (RectTransform)previousScheme.transform, (RectTransform)nextScheme.transform);

            // Segunda cor: embaixo do time, no mesmo formato.
            var previousSecondary = CreateButton(controls, "Cor 2 anterior", "<", new Vector2(RowArrowX[0], SecondaryY), Vector2.one * RowArrowSize, () => ChangeSecondary(side, -1));
            var nextSecondary = CreateButton(controls, "Próxima cor 2", ">", new Vector2(RowArrowX[1], SecondaryY), Vector2.one * RowArrowSize, () => ChangeSecondary(side, 1));
            var secondaryTitle = CreateRowLabel(controls, "Cor 2", 20, FontStyle.Bold, Color.white, SecondaryY, 0.32f);
            secondaryTitle.text = "Cor 2";
            view.SecondaryItem = CreateRect(controls, "Item da cor 2", Vector2.zero, Vector2.zero).gameObject.AddComponent<MenuEntry>();
            view.SecondaryItem.Configure(null, previousSecondary, nextSecondary, secondaryTitle.rectTransform,
                (RectTransform)previousSecondary.transform, (RectTransform)nextSecondary.transform);

            // Camisa dos botões: embaixo da segunda cor, no mesmo formato.
            var previousShirt = CreateButton(controls, "Camisa anterior", "<", new Vector2(RowArrowX[0], ShirtY), Vector2.one * RowArrowSize, () => ChangeShirt(side, false, -1));
            var nextShirt = CreateButton(controls, "Próxima camisa", ">", new Vector2(RowArrowX[1], ShirtY), Vector2.one * RowArrowSize, () => ChangeShirt(side, false, 1));
            var shirtTitle = CreateRowLabel(controls, "Camisa", 20, FontStyle.Bold, Color.white, ShirtY, 0.32f);
            shirtTitle.text = "Camisa";
            view.ShirtItem = CreateRect(controls, "Item da camisa", Vector2.zero, Vector2.zero).gameObject.AddComponent<MenuEntry>();
            view.ShirtItem.Configure(null, previousShirt, nextShirt, shirtTitle.rectTransform,
                (RectTransform)previousShirt.transform, (RectTransform)nextShirt.transform);
            view.ShirtItem.gameObject.SetActive(shirts != null);
            SetActive(shirtTitle.gameObject, shirts != null);
            SetActive(previousShirt.gameObject, shirts != null);
            SetActive(nextShirt.gameObject, shirts != null);

            // Camisa do goleiro: uma seta de cada lado dele (somem sem goleiro).
            view.KeeperArrows = CreateRect(controls, "Setas do goleiro", keeperPosition * Unit, Vector2.zero);
            var previousKeeper = CreateButton(view.KeeperArrows, "Goleiro anterior", "<", new Vector2(-KeeperArrowX, 0f), Vector2.one * 0.45f, () => ChangeShirt(side, true, -1));
            var nextKeeper = CreateButton(view.KeeperArrows, "Próximo goleiro", ">", new Vector2(KeeperArrowX, 0f), Vector2.one * 0.45f, () => ChangeShirt(side, true, 1));
            view.KeeperItem = view.KeeperArrows.gameObject.AddComponent<MenuEntry>();
            view.KeeperItem.Configure(null, previousKeeper, nextKeeper, view.Keeper.Root,
                (RectTransform)previousKeeper.transform, (RectTransform)nextKeeper.transform);

            view.Arrows = new[] { previousTeam, nextTeam, previousScheme, nextScheme, previousSecondary, nextSecondary, previousShirt, nextShirt, previousKeeper, nextKeeper };
            view.Ready = CreateButton(half, "Pronto", "Pronto", new Vector2(MiddleButtonX, MiddleButtonY), MiddleButtonSize, () => ToggleReady(side), ButtonStyle.Yellow);
            view.ReadyItem = view.Ready.gameObject.AddComponent<MenuEntry>();
            view.ReadyItem.Configure(view.Ready);
            foreach (RectTransform child in controls) view.ControlPositions.Add((child, child.anchoredPosition));
            return view;
        }

        /// <summary>
        /// Em pé: as opções da metade ficam como as do time de baixo, lidas por quem está embaixo (time à esquerda, esquema à
        /// direita), só espelhadas para cima. Usado na metade de cima contra a IA.
        /// </summary>
        private static void SetUpright(HalfView view, bool upright)
        {
            view.Controls.localRotation = upright ? Quaternion.Euler(0f, 0f, 180f) : Quaternion.identity;
            foreach (var (rect, position) in view.ControlPositions)
                rect.anchoredPosition = upright ? new Vector2(position.x, -position.y) : position;
        }

        /// <summary>
        /// Botão ou goleiro com as camadas da camisa (célula de 32x32 = 1 unidade). No time de cima a peça desgira os 180°
        /// da metade, como na Partida: a luz e a frente do goleiro ficam iguais nos dois times.
        /// </summary>
        private PieceView CreatePiece(RectTransform half, string objectName, Vector2 position, TeamSide side, bool keeper)
        {
            var piece = new PieceView();
            if (shirts == null)
            {
                // Sem a arte: círculos (botão) ou retângulo (goleiro) lisos.
                piece.Body = keeper
                    ? CreateImage(half, objectName, Color.white, null, position, new Vector2(1f, 0.25f))
                    : CreateImage(half, objectName, Color.white, circle, position, Vector2.one * DiscSize);
                piece.Root = piece.Body.rectTransform;
                if (!keeper)
                {
                    piece.Ring = CreateImage(piece.Root, "Anel", Color.white, circle, Vector2.zero, Vector2.one * 0.55f);
                    piece.Center = CreateImage(piece.Root, "Centro", Color.white, circle, Vector2.zero, Vector2.one * 0.42f);
                }
                return piece;
            }

            piece.Root = CreateRect(half, objectName, position * Unit, Vector2.one * Unit);
            if (side == TeamSide.Top) piece.Root.localRotation = Quaternion.Euler(0f, 0f, 180f);
            piece.Layers = new Image[ShirtArt.LayerCount];
            for (int i = 0; i < piece.Layers.Length; i++)
            {
                var layer = (ShirtLayer)i;
                var offset = keeper && layer == ShirtLayer.Shadow ? ShirtArt.KeeperShadowOffset : Vector2.zero;
                piece.Layers[i] = CreateImage(piece.Root, layer.ToString(), Color.white, null, offset, Vector2.one);
            }
            return piece;
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

        /// <summary>Texto à direita das setas lado a lado (time e camisa), alinhado à esquerda.</summary>
        private Text CreateRowLabel(Transform parent, string objectName, int fontSize, FontStyle style, Color color, float y, float height)
        {
            var text = CreateText(parent, objectName, fontSize, style, color, new Vector2(RowLabelLeft + RowLabelWidth * 0.5f, y), new Vector2(RowLabelWidth, height));
            text.alignment = TextAnchor.MiddleLeft;
            return text;
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

        private Button CreateButton(Transform parent, string objectName, string label, Vector2 position, Vector2 size, UnityAction action,
            ButtonStyle style = ButtonStyle.Green)
        {
            var image = CreateImage(parent, objectName, ButtonColor, null, position, size);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            var text = CreateText(image.rectTransform, "Texto", 26, FontStyle.Bold, ButtonTextColor, Vector2.zero, size);
            text.text = label;
            Stretch(text.rectTransform);
            ButtonSkin.Apply(button, style);
            return button;
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
