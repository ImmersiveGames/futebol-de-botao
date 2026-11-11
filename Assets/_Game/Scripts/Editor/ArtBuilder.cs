using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

namespace FutebolDeBotao.Editor
{
    /// <summary>
    /// Liga as artes em pixel art (Assets/_Game/Art) à cena da partida: acerta a importação (32 px por unidade,
    /// filtro Point, sem compressão) sem mexer nos recortes feitos no Sprite Editor e troca os placeholders da bola
    /// e da mesa (madeira, moldura, feltro, marcações e gols em tilemap e sprites) e dos botões e goleiros (camisas em camadas).
    /// Menu: Futebol de Botão > Aplicar artes.
    /// </summary>
    public static class ArtBuilder
    {
        public const int PixelsPerUnit = 32;

        private const string ScenePath = "Assets/_Game/Scenes/Partida.unity";
        private const string BallSheetPath = "Assets/_Game/Art/Ball/Ball_SpriteSheet.png";
        private const string BallShineName = "Ball_Light";
        private const string BallShadowName = "Ball_Shadow";
        private static readonly Regex BallFrameName = new(@"^Ball_Frame_(\d+)$");
        private const string BallVisualName = "Desenho da bola";
        // Rastro: quadros de 32x64 lado a lado (o 1º vazio); a bola fica no meio da metade de cima e o rastro sai para baixo.
        private const string BallTrailPath = "Assets/_Game/Art/Ball/ball_Effect.png";
        private const string BallTrailPrefix = "Bola_Rastro";
        private static readonly Vector2Int BallTrailCell = new(32, 64);
        private static readonly Vector2 BallTrailPivot = new(0.5f, 0.75f);
        private static readonly Color BallTrailColor = new(1f, 1f, 1f, 0.5f);

        // Mira: folha com as bolinhas (recortes do Sprite Editor: Aim_Visual_Dot e Aim_Visual_Mark).
        private const string AimSheetPath = "Assets/_Game/Art/Aim Visual/Aim_Visual.png";
        private const string AimDotName = "Aim_Visual_Dot";
        private const string AimTipName = "Aim_Visual_Mark";
        // Força: quadros de 100x100 lado a lado; o quadro N tem N anéis acesos (o 0 vazio), centrados no botão.
        private const string PowerRingsPath = "Assets/_Game/Art/Aim Visual/Power_Rings.png";
        private const string PowerRingsPrefix = "Power_Rings";
        private static readonly Vector2Int PowerRingsCell = new(100, 100);

        private const string FieldSheetPath = "Assets/_Game/Art/Campo/Field_Sprites.png";
        private const string FieldMarksPath = "Assets/_Game/Art/Campo/Field_Marks.png";
        private const string TilesFolder = "Assets/_Game/Art/Campo/Tiles";
        private const string FieldAtlasPath = "Assets/_Game/Art/Campo/Campo.spriteatlasv2";
        private const string TableArtName = "Desenho da mesa";
        // Feltros: tiles brancos de 32x32 (liso, gradiente, textura) que o jogo pinta com as cores de cada feltro.
        private const string FeltSheetPath = "Assets/_Game/Art/Campo/padrão_feltros_Sheet.png";
        private const string FeltAtlasPath = "Assets/_Game/Art/Campo/Feltros.spriteatlasv2";
        private const string FeltArtPath = "Assets/_Game/Resources/FeltroArte.asset";
        private const string FeltThemesFolder = "Assets/_Game/Resources/Feltros";
        private static readonly string[] FeltSpriteNames = { "Felt_White", "Felt_White_Gradient", "Felt_White_Texture" };

        private const string BoardSheetPath = "Assets/_Game/Art/Campo/Board_02_Tiles_Sheet.png";
        private const string GoalFramesPath = "Assets/_Game/Art/Campo/Goal_Frames_Sheet.png";
        private const string TableAtlasPath = "Assets/_Game/Art/Campo/Mesas.spriteatlasv2";
        private const string TablesFolder = "Assets/_Game/Resources/Mesas";
        private static readonly string[] BoardSpriteNames =
        {
            "Board_02_Corner", "Board_02_Near_Corner_Left", "Board_02_Center", "Board_02_Near_Corner_right",
            "Board_02_Big_Goal_frame", "Board_02_Small_Goal_frame",
        };
        private static readonly (string suffix, string name)[] GoalColorNames = { ("White", "Branca"), ("Red", "Vermelha"), ("Yellow", "Amarela") };

        // Camisas: células de 32x32, uma coluna por camada e uma linha por padrão (as colunas do goleiro têm outra ordem).
        private const string DiscSheetPath = "Assets/_Game/Art/Botoes/Botoes_Base_Sheet.png";
        private const string KeeperSheetPath = "Assets/_Game/Art/Botoes/Goal_Keeper_sheet.png";
        private const string ShirtPatternsFolder = "Assets/_Game/Resources/PadroesCamisa";
        private const string BallsFolder = "Assets/_Game/Resources/Bolas";
        private const string PieceVisualName = "Desenho";
        private const int ShirtCell = 32;
        private static readonly ShirtLayer[] DiscColumns =
            { ShirtLayer.Shadow, ShirtLayer.Body, ShirtLayer.Pattern, ShirtLayer.Outline, ShirtLayer.InnerShade, ShirtLayer.Shine };
        // O goleiro não tem contorno.
        private static readonly ShirtLayer[] KeeperColumns =
            { ShirtLayer.Shadow, ShirtLayer.Body, ShirtLayer.Pattern, ShirtLayer.Shine, ShirtLayer.InnerShade };

        // A mesa: o feltro de 7x11 no centro, a moldura de 9x13 em volta e madeira sobrando bem para fora da tela.
        // O Grid fica deslocado meio tile para as células baterem com o feltro (de -3,5 a 3,5).
        private static readonly Vector2Int FeltOrigin = new(-4, -6);
        private static readonly Vector2Int FeltSize = new(7, 11);
        private static readonly Vector2Int WoodOrigin = new(-23, -10);
        private static readonly Vector2Int WoodSize = new(45, 19);

        // Ordem de desenho: madeira, moldura, feltro, marcações, fundo do gol, bola (sombra, rastro, bola, luz), botões (10 a 12), traves e rede.
        // A bola e os botões passam por baixo da trave e da rede; a mira (50) continua por cima de tudo.
        private const int WoodOrder = -30;
        private const int FrameOrder = -25;
        private const int FeltOrder = -20;
        private const int MarksOrder = -15;
        private const int GoalBackOrder = -14;
        private const int BallOrder = 7;
        private const int GoalFrontOrder = 13;

        // Sombra preta semitransparente: escurece igual a faixa clara e a escura do feltro.
        private static readonly Color ShadowColor = new(0f, 0f, 0f, 0.4f);

        [MenuItem("Futebol de Botão/Aplicar artes")]
        public static void Build()
        {
            bool hasBall = File.Exists(BallSheetPath);
            bool hasTable = File.Exists(FieldSheetPath);
            bool hasShirts = File.Exists(DiscSheetPath) && File.Exists(KeeperSheetPath);
            bool hasAim = File.Exists(AimSheetPath);
            if (!hasBall && !hasTable && !hasShirts && !hasAim)
            {
                Debug.LogWarning($"[Futebol de Botão] Nenhuma arte encontrada (esperava {BallSheetPath} ou {FieldSheetPath}). Nada foi alterado.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var ball = Object.FindAnyObjectByType<Ball>();
            if (ball == null)
            {
                Debug.LogError($"[Futebol de Botão] A cena {ScenePath} não tem bola. Rode \"Criar cena da partida\" antes.");
                return;
            }

            if (hasBall) BuildBall(ball);
            if (hasTable) BuildTable(ball.transform.parent);
            if (hasShirts) BuildShirts();
            if (hasAim) BuildAim();
            AlignWallsToArt();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Futebol de Botão] Artes aplicadas. Cena salva em {ScenePath}.");
        }

        // ---- Mira ----

        private static void BuildAim()
        {
            ConfigureImport(AimSheetPath);
            var sprites = LoadSprites(AimSheetPath);
            sprites.TryGetValue(AimDotName, out var dot);
            sprites.TryGetValue(AimTipName, out var tip);
            if (dot == null) Debug.LogWarning($"[Futebol de Botão] {AimSheetPath} sem o recorte {AimDotName}: a mira fica com a bolinha provisória.");

            var visuals = Object.FindAnyObjectByType<AimVisuals>();
            if (visuals == null)
            {
                Debug.LogWarning($"[Futebol de Botão] A cena {ScenePath} não tem AimVisuals: mira sem arte.");
                return;
            }
            var rings = File.Exists(PowerRingsPath)
                ? SliceStrip(PowerRingsPath, PowerRingsPrefix, PowerRingsCell, new Vector2(0.5f, 0.5f))
                : new Sprite[0];
            visuals.ConfigureArt(dot, tip, rings);
            if (rings.Length > 0) Debug.Log($"[Futebol de Botão] Força com {rings.Length} quadros de anéis.");
            // Espaçamento combinando com a bolinha de 4 px (uma bolinha de vão).
            var so = new SerializedObject(visuals);
            so.FindProperty("dotSpacing").floatValue = 0.25f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(visuals);
        }

        // ---- Bola ----

        private static void BuildBall(Ball ball)
        {
            ConfigureImport(BallSheetPath);
            var sprites = LoadSprites(BallSheetPath);
            // Quadros em ordem pelo número do nome (Ball_Frame_1, Ball_Frame_2...).
            var frames = sprites.Values
                .Select(s => (sprite: s, match: BallFrameName.Match(s.name)))
                .Where(x => x.match.Success)
                .OrderBy(x => int.Parse(x.match.Groups[1].Value))
                .Select(x => x.sprite)
                .ToArray();
            sprites.TryGetValue(BallShineName, out var shine);
            sprites.TryGetValue(BallShadowName, out var shadow);

            if (frames.Length == 0)
            {
                Debug.LogError($"[Futebol de Botão] {BallSheetPath} não tem recortes \"Ball_Frame_N\". Recorte a sprite sheet no Sprite Editor.");
                return;
            }

            var trail = File.Exists(BallTrailPath) ? SliceStrip(BallTrailPath, BallTrailPrefix, BallTrailCell, BallTrailPivot) : new Sprite[0];
            SetupBall(ball, frames, shine, shadow, trail);
            CreateDefaultBall(frames, shine, shadow);
            Debug.Log($"[Futebol de Botão] Bola com {frames.Length} quadros de rolagem" +
                      $"{(shine != null ? ", luz" : "")}{(shadow != null ? ", sombra" : "")}" +
                      $"{(trail.Length > 0 ? $", rastro de {trail.Length} quadros" : "")}.");
        }

        /// <summary>
        /// Recorta uma folha de quadros lado a lado em Prefixo_0, _1... (da esquerda para a direita), com o pivô dado
        /// (no rastro, o centro da bola). Pode rodar quantas vezes quiser: os recortes com o mesmo nome mantêm o ID.
        /// </summary>
        private static Sprite[] SliceStrip(string path, string prefix, Vector2Int cell, Vector2 pivot)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            int count = width / cell.x;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var existing = provider.GetSpriteRects().GroupBy(r => r.name).ToDictionary(g => g.Key, g => g.First());

            var rects = new System.Collections.Generic.List<SpriteRect>();
            for (int i = 0; i < count; i++)
            {
                string name = $"{prefix}_{i}";
                if (!existing.TryGetValue(name, out var rect)) rect = new SpriteRect { name = name, spriteID = GUID.Generate() };
                rect.rect = new Rect(i * cell.x, height - cell.y, cell.x, cell.y);
                rect.alignment = SpriteAlignment.Custom;
                rect.pivot = pivot;
                rect.border = Vector4.zero;
                rects.Add(rect);
            }
            provider.SetSpriteRects(rects.ToArray());
            var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            names?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();

            var sprites = LoadSprites(path);
            return Enumerable.Range(0, count)
                .Select(i => sprites.TryGetValue($"{prefix}_{i}", out var sprite) ? sprite : null)
                .Where(sprite => sprite != null).ToArray();
        }

        private static void SetupBall(Ball ball, Sprite[] frames, Sprite shine, Sprite shadow, Sprite[] trail)
        {
            var old = ball.transform.Find(BallVisualName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            // O círculo branco do protótipo fica guardado, só desligado.
            // O destaque da vez (TurnHighlight) copia a ordem dele, então ele também vai para a ordem da bola.
            var placeholder = ball.GetComponent<SpriteRenderer>();
            const int order = BallOrder;
            if (placeholder != null)
            {
                placeholder.sortingOrder = order;
                placeholder.enabled = false;
            }

            // A bola é escalada pelo diâmetro da colisão; o desenho volta para 1:1 (32 px = 1 unidade).
            var root = new GameObject(BallVisualName).transform;
            root.SetParent(ball.transform, false);
            float scale = Mathf.Max(ball.transform.localScale.x, 0.0001f);
            root.localScale = Vector3.one / scale;

            // O rastro fica entre a sombra e a bola.
            var shadowTransform = shadow != null ? CreateLayer(root, "Sombra", shadow, order - 2, ShadowColor) : null;
            var trailRenderer = trail.Length > 0
                ? CreateLayer(root, "Rastro", trail[0], order - 1, BallTrailColor).GetComponent<SpriteRenderer>()
                : null;
            var rolling = CreateLayer(root, "Rolagem", frames[0], order, Color.white).GetComponent<SpriteRenderer>();
            var shineTransform = shine != null ? CreateLayer(root, "Luz", shine, order + 1, Color.white) : null;

            var visual = root.gameObject.AddComponent<BallVisual>();
            visual.Configure(rolling, shineTransform, shadowTransform, frames);
            visual.ConfigureTrail(trailRenderer, trail);
        }

        private static Transform CreateLayer(Transform parent, string layerName, Sprite sprite, int order, Color color)
        {
            var go = new GameObject(layerName);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            renderer.color = color;
            return go.transform;
        }

        // ---- Camisas (botões e goleiros) ----

        private static void BuildShirts()
        {
            // As folhas de camisa não são mais recortadas aqui: os recortes são do usuário e cada ShirtPattern aponta os seus.
            ImportShirtSheet(DiscSheetPath);
            ImportShirtSheet(KeeperSheetPath);
            MigrateShirtPatterns(ShirtPattern.Kind.Botao, DiscSheetPath, "Botao", DiscColumns);
            MigrateShirtPatterns(ShirtPattern.Kind.Goleiro, KeeperSheetPath, "Goleiro", KeeperColumns);
            AssetDatabase.SaveAssets();

            var firstDisc = FindShirtPatterns(ShirtPattern.Kind.Botao).FirstOrDefault(p => p.Usable);
            var firstKeeper = FindShirtPatterns(ShirtPattern.Kind.Goleiro).FirstOrDefault(p => p.Usable);
            if (firstDisc == null) Debug.LogWarning($"[Futebol de Botão] Nenhum padrão de botão com recortes em {ShirtPatternsFolder}.");

            // Mesma ordem dos círculos do protótipo: goleiro embaixo da bola, botões por cima dela.
            foreach (var disc in Object.FindObjectsByType<Disc>())
            {
                SetupPiece(disc.transform, firstDisc, false, 10);
                foreach (var placeholder in new[] { "Anel", "Centro" })
                {
                    var child = disc.transform.Find(placeholder);
                    if (child != null && child.TryGetComponent(out SpriteRenderer renderer)) renderer.enabled = false;
                }
            }
            foreach (var keeper in Object.FindObjectsByType<Goalkeeper>())
                SetupPiece(keeper.transform, firstKeeper, true, 5);

            Debug.Log($"[Futebol de Botão] Camisas: {FindShirtPatterns(ShirtPattern.Kind.Botao).Count(p => p.Usable)} padrões de botão e " +
                      $"{FindShirtPatterns(ShirtPattern.Kind.Goleiro).Count(p => p.Usable)} de goleiro em {ShirtPatternsFolder}.");
        }

        /// <summary>Padrões do tipo em Resources/PadroesCamisa (direto do AssetDatabase), na ordem das setas.</summary>
        private static ShirtPattern[] FindShirtPatterns(ShirtPattern.Kind kind) =>
            AssetDatabase.FindAssets("t:ShirtPattern", new[] { ShirtPatternsFolder })
                .Select(guid => AssetDatabase.LoadAssetAtPath<ShirtPattern>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(p => p != null && p.kind == kind)
                .OrderBy(p => p.order).ThenBy(p => p.id, System.StringComparer.Ordinal)
                .ToArray();

        /// <summary>Só a importação da folha (sprite em vários recortes, 32 px por unidade, sem filtro); os recortes ficam.</summary>
        private static void ImportShirtSheet(string path)
        {
            if (!File.Exists(path)) return;
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            bool changed = importer.textureType != TextureImporterType.Sprite
                           || importer.spriteImportMode != SpriteImportMode.Multiple;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            if (changed) importer.SaveAndReimport();
            ConfigureImport(path);
        }

        /// <summary>
        /// Uma vez só: os padrões que ainda não têm recortes (criados antes de o padrão guardar os seus) pegam os da linha
        /// antiga da folha (Botao_Corpo_3...), e a ordem das setas fica a da linha. Sem nenhum padrão do tipo, cria um por
        /// linha recortada com esses nomes, já liberado. Padrões com recortes não mudam.
        /// </summary>
        private static void MigrateShirtPatterns(ShirtPattern.Kind kind, string sheetPath, string prefix, ShirtLayer[] columns)
        {
            if (!AssetDatabase.IsValidFolder(ShirtPatternsFolder)) AssetDatabase.CreateFolder("Assets/_Game/Resources", "PadroesCamisa");
            var sprites = File.Exists(sheetPath) ? LoadSprites(sheetPath) : new System.Collections.Generic.Dictionary<string, Sprite>();
            Sprite Find(ShirtLayer layer, int row) =>
                columns.Contains(layer) && sprites.TryGetValue(SpriteName(prefix, layer, row), out var sprite) ? sprite : null;

            void Fill(ShirtPattern pattern)
            {
                pattern.shadow = Find(ShirtLayer.Shadow, pattern.row);
                pattern.body = Find(ShirtLayer.Body, pattern.row);
                pattern.pattern = Find(ShirtLayer.Pattern, pattern.row);
                pattern.outline = Find(ShirtLayer.Outline, pattern.row);
                pattern.innerShade = Find(ShirtLayer.InnerShade, pattern.row);
                pattern.shine = Find(ShirtLayer.Shine, pattern.row);
                pattern.order = pattern.row;
                EditorUtility.SetDirty(pattern);
            }

            var existing = FindShirtPatterns(kind);
            foreach (var pattern in existing)
                if (!pattern.Usable && pattern.row >= 0) Fill(pattern);
            if (existing.Length > 0) return;

            string idPrefix = kind == ShirtPattern.Kind.Botao ? "botao" : "goleiro";
            string label = kind == ShirtPattern.Kind.Botao ? "Botão" : "Goleiro";
            for (int row = 0; Find(ShirtLayer.Body, row) != null; row++)
            {
                var pattern = ScriptableObject.CreateInstance<ShirtPattern>();
                pattern.id = $"{idPrefix}_{row + 1}";
                pattern.displayName = $"{label} {row + 1}";
                pattern.kind = kind;
                pattern.row = row;
                pattern.unlockedByDefault = true;
                Fill(pattern);
                AssetDatabase.CreateAsset(pattern, $"{ShirtPatternsFolder}/{pattern.id}.asset");
            }
        }

        /// <summary>A bola padrão (a folha atual, branca, liberada) em Resources/Bolas, se ainda não houver; senão só religa os recortes.</summary>
        private static void CreateDefaultBall(Sprite[] frames, Sprite shine, Sprite shadow)
        {
            if (!AssetDatabase.IsValidFolder(BallsFolder)) AssetDatabase.CreateFolder("Assets/_Game/Resources", "Bolas");
            string path = $"{BallsFolder}/bola_padrao.asset";
            var ball = AssetDatabase.LoadAssetAtPath<BallTheme>(path);
            if (ball == null)
            {
                ball = ScriptableObject.CreateInstance<BallTheme>();
                ball.id = "bola_padrao";
                ball.displayName = "Clássica";
                ball.color = Color.white;
                ball.unlockedByDefault = true;
                AssetDatabase.CreateAsset(ball, path);
            }
            ball.frames = frames;
            ball.shine = shine;
            ball.shadow = shadow;
            EditorUtility.SetDirty(ball);
            AssetDatabase.SaveAssets();
        }

        private static string SpriteName(string prefix, ShirtLayer layer, int row) => $"{prefix}_{LayerName(layer)}_{row + 1}";

        private static string LayerName(ShirtLayer layer) => layer switch
        {
            ShirtLayer.Shadow => "Sombra",
            ShirtLayer.Body => "Corpo",
            ShirtLayer.Pattern => "Padrao",
            ShirtLayer.Outline => "Contorno",
            ShirtLayer.InnerShade => "SombraInterna",
            _ => "Brilho",
        };

        /// <summary>
        /// Troca o desenho do protótipo pelas camadas, dentro de um Sorting Group na ordem antiga da peça. O círculo/retângulo
        /// do protótipo fica desligado (o destaque da vez copia o formato dele).
        /// </summary>
        private static void SetupPiece(Transform piece, ShirtPattern pattern, bool keeper, int order)
        {
            var old = piece.Find(PieceVisualName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            if (piece.TryGetComponent(out SpriteRenderer placeholder))
            {
                placeholder.sortingOrder = order;
                placeholder.enabled = false;
            }

            // A peça é escalada pela colisão; o desenho volta para 1:1 (32 px = 1 unidade).
            var root = new GameObject(PieceVisualName).transform;
            root.SetParent(piece, false);
            var scale = piece.localScale;
            root.localScale = new Vector3(1f / Mathf.Max(scale.x, 0.0001f), 1f / Mathf.Max(scale.y, 0.0001f), 1f);
            root.gameObject.AddComponent<SortingGroup>().sortingOrder = order;

            var spinning = new GameObject("Gira").transform;
            spinning.SetParent(root, false);
            var still = new GameObject("Parado").transform;
            still.SetParent(root, false);

            var renderers = new SpriteRenderer[ShirtArt.LayerCount];
            for (int i = 0; i < renderers.Length; i++)
            {
                var layer = (ShirtLayer)i;
                var go = CreateLayer(ShirtArt.Spins(layer) ? spinning : still, LayerName(layer), pattern != null ? pattern.Get(layer) : null, i,
                    ShirtArt.Tint(layer, Color.white, Color.white));
                if (keeper && layer == ShirtLayer.Shadow) go.localPosition = ShirtArt.KeeperShadowOffset;
                renderers[i] = go.GetComponent<SpriteRenderer>();
            }
            root.gameObject.AddComponent<PieceArt>().Configure(keeper, renderers, keeper ? null : spinning, still);
        }

        // ---- Mesa ----

        private static void BuildTable(Transform table)
        {
            ConfigureImport(FieldSheetPath);
            if (File.Exists(FieldMarksPath)) ConfigureImport(FieldMarksPath);
            EnsureAtlas(FieldAtlasPath, FieldSheetPath);
            BuildFelts();
            BuildTables();
            var sprites = LoadSprites(FieldSheetPath);

            string[] required =
            {
                "Wall_Fill", "Wall_Top", "Wall_Bottom", "Wall_Left", "Wall_Right",
                "Top_Corner_L", "Top_Corner_R", "Bottom_Corner_L", "Bottom_Corner_R", "Field_Braight", "Field_Dark",
            };
            var missing = required.Where(n => !sprites.ContainsKey(n)).ToArray();
            if (missing.Length > 0)
            {
                Debug.LogError($"[Futebol de Botão] Faltam recortes em {FieldSheetPath}: {string.Join(", ", missing)}. A mesa não foi montada.");
                return;
            }

            var old = table.Find(TableArtName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            HidePrototypeTable(table);

            var root = new GameObject(TableArtName, typeof(Grid));
            root.transform.SetParent(table, false);
            root.transform.position = new Vector3(0.5f, 0.5f, 0f);

            // Madeira por tudo; a moldura por cima dela; o feltro dentro da moldura.
            var wood = CreateTilemap(root.transform, "Madeira", WoodOrder);
            var woodTile = LoadOrCreateTile(sprites["Wall_Fill"]);
            for (int x = 0; x < WoodSize.x; x++)
                for (int y = 0; y < WoodSize.y; y++)
                    wood.SetTile(new Vector3Int(WoodOrigin.x + x, WoodOrigin.y + y, 0), woodTile);

            var frame = CreateTilemap(root.transform, "Moldura", FrameOrder);
            int left = FeltOrigin.x - 1, right = FeltOrigin.x + FeltSize.x;
            int bottom = FeltOrigin.y - 1, top = FeltOrigin.y + FeltSize.y;
            for (int x = left + 1; x < right; x++)
            {
                frame.SetTile(new Vector3Int(x, top, 0), LoadOrCreateTile(sprites["Wall_Top"]));
                frame.SetTile(new Vector3Int(x, bottom, 0), LoadOrCreateTile(sprites["Wall_Bottom"]));
            }
            for (int y = bottom + 1; y < top; y++)
            {
                frame.SetTile(new Vector3Int(left, y, 0), LoadOrCreateTile(sprites["Wall_Left"]));
                frame.SetTile(new Vector3Int(right, y, 0), LoadOrCreateTile(sprites["Wall_Right"]));
            }
            frame.SetTile(new Vector3Int(left, top, 0), LoadOrCreateTile(sprites["Top_Corner_L"]));
            frame.SetTile(new Vector3Int(right, top, 0), LoadOrCreateTile(sprites["Top_Corner_R"]));
            frame.SetTile(new Vector3Int(left, bottom, 0), LoadOrCreateTile(sprites["Bottom_Corner_L"]));
            frame.SetTile(new Vector3Int(right, bottom, 0), LoadOrCreateTile(sprites["Bottom_Corner_R"]));

            var felt = CreateTilemap(root.transform, "Feltro", FeltOrder);

            // Marcações: um sprite só do tamanho do feltro, no centro.
            var marks = AssetDatabase.LoadAssetAtPath<Sprite>(FieldMarksPath);
            if (marks != null) CreateSpriteObject(root.transform, "Marcações", marks, Vector2.zero, MarksOrder);
            else Debug.LogWarning($"[Futebol de Botão] Sem {FieldMarksPath}: campo sem marcações.");

            // Gols: o fundo fica no lugar dos 3 tiles do meio da moldura, embaixo da bola; traves e rede por cima.
            var big = CreateGoals(root.transform, "Gols grandes", sprites, "Goal", "Pole", top, bottom);
            var small = CreateGoals(root.transform, "Gols pequenos", sprites, "Small_Goal", "Small_Pole", top, bottom);

            root.AddComponent<TableArt>().Configure(felt,
                LoadOrCreateTile(sprites["Field_Braight"]), LoadOrCreateTile(sprites["Field_Dark"]),
                FeltOrigin, FeltSize, frame.gameObject, big, small);

            Debug.Log("[Futebol de Botão] Mesa em pixel art montada (madeira, moldura, feltro, marcações e gols).");
        }

        // ---- Mesas ----

        /// <summary>
        /// Mesas em Resources/Mesas: a clássica (a moldura e os gols da folha do campo) e a de placas (folhas
        /// Board_02_Tiles_Sheet e Goal_Frames_Sheet), as duas já liberadas. Cria só as que faltam; as que existem só
        /// têm os recortes religados (para seguir a folha se o usuário recortar de novo).
        /// </summary>
        private static void BuildTables()
        {
            if (!AssetDatabase.IsValidFolder(TablesFolder)) AssetDatabase.CreateFolder("Assets/_Game/Resources", "Mesas");

            var classic = LoadOrCreateTable("mesa_classica");
            if (classic.displayName == "Mesa")
            {
                classic.displayName = "Clássica";
                classic.classic = true;
                classic.wallInsetPixels = 3f;
                classic.unlockedByDefault = true;
            }
            EditorUtility.SetDirty(classic);

            if (!File.Exists(BoardSheetPath) || !File.Exists(GoalFramesPath))
            {
                Debug.LogWarning($"[Futebol de Botão] Sem {BoardSheetPath} ou {GoalFramesPath}: só a mesa clássica.");
                AssetDatabase.SaveAssets();
                return;
            }
            ConfigureImport(BoardSheetPath);
            ConfigureImport(GoalFramesPath);
            EnsureAtlas(TableAtlasPath, BoardSheetPath, GoalFramesPath);
            var boards = LoadSprites(BoardSheetPath);
            var frames = LoadSprites(GoalFramesPath);
            var missing = BoardSpriteNames.Where(n => !boards.ContainsKey(n)).ToArray();
            if (missing.Length > 0)
            {
                Debug.LogError($"[Futebol de Botão] Faltam recortes em {BoardSheetPath}: {string.Join(", ", missing)}. Mesa de placas não montada.");
                AssetDatabase.SaveAssets();
                return;
            }

            var table = LoadOrCreateTable("mesa_placas");
            if (table.displayName == "Mesa")
            {
                table.displayName = "Placas";
                table.wallInsetPixels = 0f;
                table.unlockedByDefault = true;
            }
            table.classic = false;
            table.corner = boards[BoardSpriteNames[0]];
            table.afterCorner = boards[BoardSpriteNames[1]];
            table.center = boards[BoardSpriteNames[2]];
            table.beforeCorner = boards[BoardSpriteNames[3]];
            table.bigGoalBoard = boards[BoardSpriteNames[4]];
            table.smallGoalBoard = boards[BoardSpriteNames[5]];
            Sprite Frame(string spriteName) => frames.TryGetValue(spriteName, out var sprite) ? sprite : null;
            table.goalColors = GoalColorNames
                .Select(c => new TableTheme.GoalFrames
                {
                    name = c.name,
                    bigLeft = Frame($"Big_Left_{c.suffix}"),
                    bigRight = Frame($"Big_Right_{c.suffix}"),
                    smallLeft = Frame($"Small_Left_{c.suffix}"),
                    smallRight = Frame($"Small_Right_{c.suffix}"),
                    center = Frame($"Goal_Center_{c.suffix}"),
                })
                .Where(f => f.Complete)
                .ToArray();
            if (table.goalColors.Length < GoalColorNames.Length)
                Debug.LogWarning($"[Futebol de Botão] {GoalFramesPath}: só {table.goalColors.Length} de {GoalColorNames.Length} cores de trave com todos os recortes.");
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Futebol de Botão] Mesas: clássica e de placas ({table.goalColors.Length} cores de trave) em {TablesFolder}.");
        }

        private static TableTheme LoadOrCreateTable(string id)
        {
            string path = $"{TablesFolder}/{id}.asset";
            var table = AssetDatabase.LoadAssetAtPath<TableTheme>(path);
            if (table != null) return table;
            table = ScriptableObject.CreateInstance<TableTheme>();
            table.id = id;
            AssetDatabase.CreateAsset(table, path);
            return table;
        }

        // ---- Feltros ----

        /// <summary>
        /// Folha de feltros: importa, põe num atlas, cria um Tile sem trava de cor por recorte, grava a
        /// <see cref="FeltArt"/> em Resources e, se a pasta Resources/Feltros estiver vazia, cria os feltros de teste.
        /// </summary>
        private static void BuildFelts()
        {
            if (!File.Exists(FeltSheetPath))
            {
                Debug.LogWarning($"[Futebol de Botão] Sem {FeltSheetPath}: o feltro fica nas faixas antigas.");
                return;
            }
            ConfigureImport(FeltSheetPath);
            EnsureAtlas(FeltAtlasPath, FeltSheetPath);
            var sprites = LoadSprites(FeltSheetPath);
            var missing = FeltSpriteNames.Where(n => !sprites.ContainsKey(n)).ToArray();
            if (missing.Length > 0)
            {
                Debug.LogError($"[Futebol de Botão] Faltam recortes em {FeltSheetPath}: {string.Join(", ", missing)}. Feltros não montados.");
                return;
            }

            var art = AssetDatabase.LoadAssetAtPath<FeltArt>(FeltArtPath);
            if (art == null)
            {
                art = ScriptableObject.CreateInstance<FeltArt>();
                AssetDatabase.CreateAsset(art, FeltArtPath);
            }
            art.sprites = FeltSpriteNames.Select(n => sprites[n]).ToArray();
            art.tiles = art.sprites.Select(sprite =>
            {
                var tile = LoadOrCreateTile(sprite);
                // Cada célula leva a cor da sua faixa: o Tile não pode travar a cor.
                tile.flags = TileFlags.None;
                return (TileBase)tile;
            }).ToArray();
            EditorUtility.SetDirty(art);
            CreateTestFelts();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Futebol de Botão] Feltros: {FeltSpriteNames.Length} tiles e {AssetDatabase.FindAssets("t:FeltTheme", new[] { FeltThemesFolder }).Length} feltros em {FeltThemesFolder}.");
        }

        /// <summary>Feltros de teste, um de cada padrão e tile; só quando a pasta ainda não tem nenhum.</summary>
        private static void CreateTestFelts()
        {
            if (!AssetDatabase.IsValidFolder(FeltThemesFolder)) AssetDatabase.CreateFolder("Assets/_Game/Resources", "Feltros");
            if (AssetDatabase.FindAssets("t:FeltTheme", new[] { FeltThemesFolder }).Length > 0) return;

            var green = Hex("1A7A3E");
            var darkGreen = Hex("24523B");
            var blue = Hex("2F5FA8");
            var purple = Hex("6A3D9A");
            CreateFelt("verde_listrado", "Verde listrado", FeltTheme.Pattern.Listrado, FeltTheme.TileKind.Liso, green, darkGreen, 0, true);
            CreateFelt("verde_listrado_gradiente", "Verde listrado com gradiente", FeltTheme.Pattern.Listrado, FeltTheme.TileKind.Gradiente, green, darkGreen, 0, true);
            CreateFelt("azul_liso", "Azul liso", FeltTheme.Pattern.Liso, FeltTheme.TileKind.Textura, blue, blue, 100, false);
            CreateFelt("roxo_meio_a_meio", "Roxo e verde meio a meio", FeltTheme.Pattern.MeioAMeio, FeltTheme.TileKind.Liso, purple, green, 150, false);
            CreateFelt("azul_roxo_listrado", "Azul e roxo listrado", FeltTheme.Pattern.Listrado, FeltTheme.TileKind.Gradiente, blue, purple, 200, false);
        }

        private static void CreateFelt(string id, string displayName, FeltTheme.Pattern pattern, FeltTheme.TileKind tile,
            Color primary, Color secondary, int price, bool unlocked)
        {
            var felt = ScriptableObject.CreateInstance<FeltTheme>();
            felt.id = id;
            felt.displayName = displayName;
            felt.pattern = pattern;
            felt.tile = tile;
            felt.primary = primary;
            felt.secondary = secondary;
            felt.price = price;
            felt.unlockedByDefault = unlocked;
            AssetDatabase.CreateAsset(felt, $"{FeltThemesFolder}/{id}.asset");
        }

        private static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var color) ? color : Color.magenta;

        /// <summary>
        /// Tiles lado a lado na mesma imagem "vazam" um pixel do vizinho na emenda (linhas finas no feltro). Um Sprite
        /// Atlas com folga entre os recortes resolve: a Unity repete a beira de cada recorte na folga.
        /// </summary>
        private static void EnsureAtlas(string atlasPath, params string[] texturePaths)
        {
            if (EditorSettings.spritePackerMode != SpritePackerMode.SpriteAtlasV2)
                EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;

            if (!File.Exists(atlasPath))
            {
                var atlas = new SpriteAtlasAsset();
                atlas.Add(texturePaths.Select(p => (Object)AssetDatabase.LoadAssetAtPath<Texture2D>(p)).ToArray());
                SpriteAtlasAsset.Save(atlas, atlasPath);
                AssetDatabase.ImportAsset(atlasPath);
            }

            var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(atlasPath);
            importer.includeInBuild = true;
            importer.packingSettings = new SpriteAtlasPackingSettings
            {
                blockOffset = 1, padding = 4, enableRotation = false, enableTightPacking = false, enableAlphaDilation = false,
            };
            importer.textureSettings = new SpriteAtlasTextureSettings
            {
                filterMode = FilterMode.Point, generateMipMaps = false, readable = false, sRGB = true, anisoLevel = 0,
            };
            var platform = importer.GetPlatformSettings("DefaultTexturePlatform");
            platform.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformSettings(platform);
            importer.SaveAndReimport();
            SpriteAtlasUtility.PackAllAtlases(EditorUserBuildSettings.activeBuildTarget);
        }

        /// <summary>
        /// Paredes de colisão afastadas <see cref="FieldLayout.ClassicWallInset"/> para dentro da face desenhada (a mesma conta
        /// do "Criar cena da partida"); pode rodar quantas vezes quiser. A rede do gol não muda.
        /// </summary>
        private static void AlignWallsToArt()
        {
            float inset = FieldLayout.ClassicWallInset;
            float sideSegment = (FieldLayout.Width - FieldLayout.GoalWidth) * 0.5f + inset;
            foreach (var wall in Object.FindObjectsByType<Wall>())
            {
                if (!wall.PushesBack) continue;
                var t = wall.transform;
                var position = t.position;
                var scale = t.localScale;
                if (Mathf.Abs(position.x) > FieldLayout.HalfWidth)
                {
                    position.x = Mathf.Sign(position.x) * (FieldLayout.HalfWidth + inset + scale.x * 0.5f);
                    scale.y = FieldLayout.Height + 2f * (scale.x + inset);
                }
                else if (Mathf.Abs(position.y) > FieldLayout.HalfHeight)
                {
                    position.y = Mathf.Sign(position.y) * (FieldLayout.HalfHeight + inset + scale.y * 0.5f);
                    position.x = Mathf.Sign(position.x) * (FieldLayout.GoalWidth * 0.5f + sideSegment * 0.5f);
                    scale.x = sideSegment;
                }
                else continue;
                t.position = position;
                t.localScale = scale;
            }
        }

        /// <summary>Desliga o desenho do protótipo (feltro, linhas, paredes e rede); as colisões continuam.</summary>
        private static void HidePrototypeTable(Transform table)
        {
            foreach (Transform child in table)
            {
                if (!child.TryGetComponent(out SpriteRenderer renderer)) continue;
                if (child.GetComponent<Wall>() != null || renderer.sortingOrder < 0) renderer.enabled = false;
            }
        }

        /// <summary>
        /// Um par de gols (cima e baixo), cada um com 3 peças de fundo e 3 de trave/rede. Peças: {prefixo}_Top_Corner_Left,
        /// _Top_Center, _Top_Corner_Right e o mesmo com Bottom. Nulo se faltar algum recorte.
        /// </summary>
        private static GameObject CreateGoals(Transform parent, string groupName, System.Collections.Generic.Dictionary<string, Sprite> sprites,
            string backPrefix, string frontPrefix, int topRow, int bottomRow)
        {
            string[] pieces = { "Corner_Left", "Center", "Corner_Right" };
            var names = new[] { "Top", "Bottom" }
                .SelectMany(end => pieces.SelectMany(p => new[] { $"{backPrefix}_{end}_{p}", $"{frontPrefix}_{end}_{p}" }))
                .ToArray();
            var missing = names.Where(n => !sprites.ContainsKey(n)).ToArray();
            if (missing.Length > 0)
            {
                Debug.LogWarning($"[Futebol de Botão] {groupName}: faltam recortes ({string.Join(", ", missing)}).");
                return null;
            }

            var group = new GameObject(groupName).transform;
            group.SetParent(parent, false);
            foreach (var (end, row) in new[] { ("Top", topRow), ("Bottom", bottomRow) })
            {
                for (int i = 0; i < pieces.Length; i++)
                {
                    // Centro da célula: o Grid já está deslocado meio tile, então a célula (x, y) tem centro em (x + 1, y + 1).
                    var center = new Vector2(i - 1, row + 1);
                    CreateSpriteObject(group, $"Fundo {end} {i + 1}", sprites[$"{backPrefix}_{end}_{pieces[i]}"], center, GoalBackOrder);
                    CreateSpriteObject(group, $"Trave {end} {i + 1}", sprites[$"{frontPrefix}_{end}_{pieces[i]}"], center, GoalFrontOrder);
                }
            }
            return group.gameObject;
        }

        private static Tilemap CreateTilemap(Transform parent, string layerName, int order)
        {
            var go = new GameObject(layerName, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<TilemapRenderer>().sortingOrder = order;
            return go.GetComponent<Tilemap>();
        }

        private static void CreateSpriteObject(Transform parent, string objectName, Sprite sprite, Vector2 worldPosition, int order)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPosition;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
        }

        /// <summary>Um asset de Tile por recorte, em Art/Campo/Tiles (reaproveitado se já existir).</summary>
        private static Tile LoadOrCreateTile(Sprite sprite)
        {
            if (!AssetDatabase.IsValidFolder(TilesFolder)) AssetDatabase.CreateFolder("Assets/_Game/Art/Campo", "Tiles");
            string path = $"{TilesFolder}/{sprite.name}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.None;
            EditorUtility.SetDirty(tile);
            return tile;
        }

        private static System.Collections.Generic.Dictionary<string, Sprite> LoadSprites(string path) =>
            AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                .GroupBy(s => s.name).ToDictionary(g => g.Key, g => g.First());

        /// <summary>Acerta só a escala e a nitidez; os recortes do Sprite Editor ficam como estão.</summary>
        private static void ConfigureImport(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            bool changed = importer.spritePixelsPerUnit != PixelsPerUnit
                           || importer.filterMode != FilterMode.Point
                           || importer.textureCompression != TextureImporterCompression.Uncompressed
                           || importer.mipmapEnabled;
            if (!changed) return;

            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
    }
}
