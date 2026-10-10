using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FutebolDeBotao.Editor
{
    /// <summary>
    /// Liga as artes em pixel art (Assets/_Game/Art) à cena da partida: acerta a importação (32 px por unidade,
    /// filtro Point, sem compressão) sem mexer nos recortes feitos no Sprite Editor e troca os placeholders da bola
    /// e da mesa (madeira, moldura, feltro, marcações e gols em tilemap e sprites).
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

        private const string FieldSheetPath = "Assets/_Game/Art/Campo/Field_Sprites.png";
        private const string FieldMarksPath = "Assets/_Game/Art/Campo/Field_Marks.png";
        private const string TilesFolder = "Assets/_Game/Art/Campo/Tiles";
        private const string FieldAtlasPath = "Assets/_Game/Art/Campo/Campo.spriteatlasv2";
        private const string TableArtName = "Desenho da mesa";

        // A mesa: o feltro de 7x11 no centro, a moldura de 9x13 em volta e madeira sobrando bem para fora da tela.
        // O Grid fica deslocado meio tile para as células baterem com o feltro (de -3,5 a 3,5).
        private static readonly Vector2Int FeltOrigin = new(-4, -6);
        private static readonly Vector2Int FeltSize = new(7, 11);
        private static readonly Vector2Int WoodOrigin = new(-23, -10);
        private static readonly Vector2Int WoodSize = new(45, 19);

        // Ordem de desenho: madeira, moldura, feltro, marcações, fundo do gol, bola, botões (10 a 12), traves e rede.
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
            if (!hasBall && !hasTable)
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
            AlignWallsToArt();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Futebol de Botão] Artes aplicadas. Cena salva em {ScenePath}.");
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

            SetupBall(ball, frames, shine, shadow);
            Debug.Log($"[Futebol de Botão] Bola com {frames.Length} quadros de rolagem" +
                      $"{(shine != null ? ", luz" : "")}{(shadow != null ? ", sombra" : "")}.");
        }

        private static void SetupBall(Ball ball, Sprite[] frames, Sprite shine, Sprite shadow)
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

            var shadowTransform = shadow != null ? CreateLayer(root, "Sombra", shadow, order - 1, ShadowColor) : null;
            var rolling = CreateLayer(root, "Rolagem", frames[0], order, Color.white).GetComponent<SpriteRenderer>();
            var shineTransform = shine != null ? CreateLayer(root, "Luz", shine, order + 1, Color.white) : null;

            root.gameObject.AddComponent<BallVisual>().Configure(rolling, shineTransform, shadowTransform, frames);
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

        // ---- Mesa ----

        private static void BuildTable(Transform table)
        {
            ConfigureImport(FieldSheetPath);
            if (File.Exists(FieldMarksPath)) ConfigureImport(FieldMarksPath);
            EnsureFieldAtlas();
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

            sprites.TryGetValue("Field_Braight_Gradient", out var brightGradient);
            sprites.TryGetValue("Field_Dark_Gradient", out var darkGradient);
            root.AddComponent<TableArt>().Configure(felt,
                LoadOrCreateTile(sprites["Field_Braight"]), LoadOrCreateTile(sprites["Field_Dark"]),
                brightGradient != null ? LoadOrCreateTile(brightGradient) : null,
                darkGradient != null ? LoadOrCreateTile(darkGradient) : null,
                FeltOrigin, FeltSize, big, small);

            Debug.Log("[Futebol de Botão] Mesa em pixel art montada (madeira, moldura, feltro, marcações e gols).");
        }

        /// <summary>
        /// Tiles lado a lado na mesma imagem "vazam" um pixel do vizinho na emenda (linhas finas no feltro). Um Sprite
        /// Atlas com folga entre os recortes resolve: a Unity repete a beira de cada recorte na folga.
        /// </summary>
        private static void EnsureFieldAtlas()
        {
            if (EditorSettings.spritePackerMode != SpritePackerMode.SpriteAtlasV2)
                EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;

            if (!File.Exists(FieldAtlasPath))
            {
                var atlas = new SpriteAtlasAsset();
                atlas.Add(new Object[] { AssetDatabase.LoadAssetAtPath<Texture2D>(FieldSheetPath) });
                SpriteAtlasAsset.Save(atlas, FieldAtlasPath);
                AssetDatabase.ImportAsset(FieldAtlasPath);
            }

            var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(FieldAtlasPath);
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
        /// Paredes de colisão afastadas <see cref="FieldLayout.WallInset"/> para dentro da face desenhada (a mesma conta
        /// do "Criar cena da partida"); pode rodar quantas vezes quiser. A rede do gol não muda.
        /// </summary>
        private static void AlignWallsToArt()
        {
            float inset = FieldLayout.WallInset;
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
