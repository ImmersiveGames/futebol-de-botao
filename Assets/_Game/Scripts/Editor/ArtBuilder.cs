using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FutebolDeBotao.Editor
{
    /// <summary>
    /// Liga as artes em pixel art (Assets/_Game/Art) à cena da partida: acerta a importação (32 px por unidade,
    /// filtro Point, sem compressão) sem mexer nos recortes feitos no Sprite Editor e troca os placeholders.
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

        // Sombra preta semitransparente: escurece igual a faixa clara e a escura do feltro.
        private static readonly Color ShadowColor = new(0f, 0f, 0f, 0.4f);

        [MenuItem("Futebol de Botão/Aplicar artes")]
        public static void Build()
        {
            if (!File.Exists(BallSheetPath))
            {
                Debug.LogWarning($"[Futebol de Botão] Nenhuma arte encontrada (esperava {BallSheetPath}). Nada foi alterado.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            ConfigureImport(BallSheetPath);
            var sprites = AssetDatabase.LoadAllAssetsAtPath(BallSheetPath).OfType<Sprite>().ToArray();
            // Quadros em ordem pelo número do nome (Ball_Frame_1, Ball_Frame_2...).
            var frames = sprites
                .Select(s => (sprite: s, match: BallFrameName.Match(s.name)))
                .Where(x => x.match.Success)
                .OrderBy(x => int.Parse(x.match.Groups[1].Value))
                .Select(x => x.sprite)
                .ToArray();
            var shine = sprites.FirstOrDefault(s => s.name == BallShineName);
            var shadow = sprites.FirstOrDefault(s => s.name == BallShadowName);

            if (frames.Length == 0)
            {
                Debug.LogError($"[Futebol de Botão] {BallSheetPath} não tem recortes \"Ball_Frame_N\". Recorte a sprite sheet no Sprite Editor.");
                return;
            }

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
