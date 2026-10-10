using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace FutebolDeBotao.Editor
{
    /// <summary>
    /// Liga as artes em pixel art (Assets/_Game/Art) à cena da partida: configura a importação (32 px por unidade,
    /// filtro Point, sem compressão), recorta as tiras de animação e troca os placeholders. Peça sem arquivo fica como está.
    /// Menu: Futebol de Botão > Aplicar artes.
    /// </summary>
    public static class ArtBuilder
    {
        public const int PixelsPerUnit = 32;
        private const int FrameSize = 32;

        private const string ScenePath = "Assets/_Game/Scenes/Partida.unity";
        private const string BallFolder = "Assets/_Game/Art/Bola";
        private const string BallRollingPath = BallFolder + "/BolaRolando.png";
        private const string BallShinePath = BallFolder + "/BolaLuz.png";
        private const string BallShadowPath = BallFolder + "/BolaSombra.png";
        private const string BallVisualName = "Desenho da bola";

        // Sombra preta semitransparente: escurece igual a faixa clara e a escura do feltro.
        private static readonly Color ShadowColor = new(0f, 0f, 0f, 0.4f);

        [MenuItem("Futebol de Botão/Aplicar artes")]
        public static void Build()
        {
            if (!File.Exists(BallRollingPath))
            {
                Debug.LogWarning($"[Futebol de Botão] Nenhuma arte encontrada (esperava {BallRollingPath}). Nada foi alterado.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var frames = ImportStrip(BallRollingPath);
            var shine = ImportSingle(BallShinePath);
            var shadow = ImportSingle(BallShadowPath);

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var ball = Object.FindAnyObjectByType<Ball>();
            if (ball == null)
            {
                Debug.LogError($"[Futebol de Botão] A cena {ScenePath} não tem bola. Rode \"Criar cena da partida\" antes.");
                return;
            }

            SetupBall(ball, frames, shine, shadow);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Futebol de Botão] Bola com {frames.Length} quadros de rolagem" +
                      $"{(shine != null ? ", luz" : "")}{(shadow != null ? ", sombra" : "")}. Cena salva em {ScenePath}.");
        }

        private static void SetupBall(Ball ball, Sprite[] frames, Sprite shine, Sprite shadow)
        {
            var old = ball.transform.Find(BallVisualName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            // O círculo branco do protótipo fica guardado, só desligado.
            var placeholder = ball.GetComponent<SpriteRenderer>();
            int order = placeholder != null ? placeholder.sortingOrder : 10;
            if (placeholder != null) placeholder.enabled = false;

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

        private static Sprite ImportSingle(string path)
        {
            if (!File.Exists(path)) return null;
            var importer = Configure(path, SpriteImportMode.Single);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Tira horizontal de quadros de 32 x 32, recortada na ordem da esquerda para a direita.</summary>
        private static Sprite[] ImportStrip(string path)
        {
            var importer = Configure(path, SpriteImportMode.Multiple);
            importer.SaveAndReimport();

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            int count = Mathf.Max(1, texture.width / FrameSize);
            string baseName = Path.GetFileNameWithoutExtension(path);

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            // Reaproveita os ids dos quadros que já existem, para não quebrar referências.
            var existing = provider.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);
            var rects = new List<SpriteRect>();
            for (int i = 0; i < count; i++)
            {
                string frameName = $"{baseName}_{i}";
                rects.Add(new SpriteRect
                {
                    name = frameName,
                    rect = new Rect(i * FrameSize, 0, FrameSize, FrameSize),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    spriteID = existing.TryGetValue(frameName, out var id) ? id : GUID.Generate()
                });
            }
            provider.SetSpriteRects(rects.ToArray());

            var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            names?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));

            provider.Apply();
            importer.SaveAndReimport();

            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);
            return rects.Select(r => sprites[r.name]).ToArray();
        }

        private static TextureImporter Configure(string path, SpriteImportMode mode)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = mode;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            return importer;
        }
    }
}
