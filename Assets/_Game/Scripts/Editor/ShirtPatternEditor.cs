using UnityEditor;
using UnityEngine;

namespace FutebolDeBotao.Editor
{
    /// <summary>Inspector do padrão de camisa: prévia das camadas montadas e pintadas com duas cores de exemplo.</summary>
    [CustomEditor(typeof(ShirtPattern))]
    public sealed class ShirtPatternEditor : UnityEditor.Editor
    {
        private const float PreviewSize = 128f;
        private static Color previewPrimary = new(0.85f, 0.15f, 0.15f);
        private static Color previewSecondary = Color.white;

        public override void OnInspectorGUI()
        {
            var shirt = (ShirtPattern)target;
            EditorGUILayout.LabelField("Prévia", EditorStyles.boldLabel);
            previewPrimary = EditorGUILayout.ColorField("Cor principal (exemplo)", previewPrimary);
            previewSecondary = EditorGUILayout.ColorField("Segunda cor (exemplo)", previewSecondary);
            var area = GUILayoutUtility.GetRect(PreviewSize, PreviewSize, GUILayout.ExpandWidth(false));
            EditorGUI.DrawRect(area, new Color(0.1f, 0.4f, 0.2f));
            ShirtPreview.Draw(area, shirt, previewPrimary, previewSecondary);
            if (!shirt.Usable) EditorGUILayout.HelpBox("Sem o recorte do corpo este padrão não aparece no jogo.", MessageType.Warning);
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
    }

    /// <summary>Desenha um padrão de camisa montado e pintado (prévia no Inspector).</summary>
    public static class ShirtPreview
    {
        /// <summary>Tamanho da prévia com a altura <paramref name="height"/>, na proporção do recorte do corpo.</summary>
        public static Vector2 Size(ShirtPattern shirt, float height)
        {
            if (shirt == null || shirt.body == null || shirt.body.rect.height <= 0f) return new Vector2(height, height);
            return new Vector2(height * shirt.body.rect.width / shirt.body.rect.height, height);
        }

        public static void Draw(Rect area, ShirtPattern shirt, Color primary, Color secondary)
        {
            if (shirt == null) return;
            bool keeper = shirt.kind == ShirtPattern.Kind.Goleiro;
            // Pontos da prévia por unidade do jogo (o deslocamento da sombra do goleiro é em unidades).
            float perUnit = shirt.body != null ? area.width / (shirt.body.rect.width / shirt.body.pixelsPerUnit) : area.width;
            for (int i = 0; i < ShirtArt.LayerCount; i++)
            {
                var layer = (ShirtLayer)i;
                var sprite = shirt.Get(layer);
                if (sprite == null || sprite.texture == null) continue;
                var rect = area;
                if (keeper && layer == ShirtLayer.Shadow)
                    rect.position += new Vector2(ShirtArt.KeeperShadowOffset.x, -ShirtArt.KeeperShadowOffset.y) * perUnit;
                var texture = sprite.texture;
                // Fora do atlas vale o recorte inteiro; num atlas, a parte guardada (sem o transparente) vai no lugar dela
                // dentro da célula, senão cada camada estica de um jeito e o botão fica torto.
                var full = sprite.rect;
                var source = sprite.packed ? sprite.textureRect : full;
                var offset = sprite.packed ? sprite.textureRectOffset : Vector2.zero;
                rect = new Rect(
                    rect.x + offset.x / full.width * rect.width,
                    rect.y + (full.height - offset.y - source.height) / full.height * rect.height,
                    source.width / full.width * rect.width,
                    source.height / full.height * rect.height);
                var coords = new Rect(source.x / texture.width, source.y / texture.height, source.width / texture.width, source.height / texture.height);
                var old = GUI.color;
                GUI.color = ShirtArt.Tint(layer, primary, secondary, keeper);
                GUI.DrawTextureWithTexCoords(rect, texture, coords, true);
                GUI.color = old;
            }
        }
    }
}
