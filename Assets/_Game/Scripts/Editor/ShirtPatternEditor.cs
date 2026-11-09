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
            bool keeper = shirt.kind == ShirtPattern.Kind.Goleiro;
            for (int i = 0; i < ShirtArt.LayerCount; i++)
            {
                var layer = (ShirtLayer)i;
                var sprite = shirt.Get(layer);
                if (sprite == null || sprite.texture == null) continue;
                var rect = area;
                if (keeper && layer == ShirtLayer.Shadow)
                    rect.position += new Vector2(ShirtArt.KeeperShadowOffset.x, -ShirtArt.KeeperShadowOffset.y) * PreviewSize;
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
                GUI.color = ShirtArt.Tint(layer, previewPrimary, previewSecondary, keeper);
                GUI.DrawTextureWithTexCoords(rect, texture, coords, true);
                GUI.color = old;
            }
            if (!shirt.Usable) EditorGUILayout.HelpBox("Sem o recorte do corpo este padrão não aparece no jogo.", MessageType.Warning);
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
    }
}
