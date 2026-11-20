using UnityEditor;
using UnityEngine;

namespace FutebolDeBotao.Editor
{
    /// <summary>
    /// Inspector da bola: os campos de sempre e a prévia de todos os quadros lado a lado, montados como na Partida
    /// (sombra, camada de cor por baixo, quadro e luz), na cor escolhida entre as que a bola aceita e sobre o feltro.
    /// </summary>
    [CustomEditor(typeof(BallTheme))]
    [CanEditMultipleObjects]
    public sealed class BallThemeEditor : UnityEditor.Editor
    {
        private const float MaxCell = 96f;
        private static readonly Color ShadowColor = new(0f, 0f, 0f, 0.4f);

        private int colorIndex = -1;
        private Color feltColor = new(0.102f, 0.478f, 0.243f);

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            // Vários selecionados: os campos editam todos; a prévia é só de um.
            if (targets.Length > 1) return;

            var ball = (BallTheme)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Prévia", EditorStyles.boldLabel);
            if (!ball.HasFrames)
            {
                EditorGUILayout.HelpBox("Sem quadros (Frames): a bola não aparece no jogo nem na Loja.", MessageType.Info);
                return;
            }
            if (ball.colorFrames != null && ball.colorFrames.Length > 0 && !ball.HasColorLayer)
                EditorGUILayout.HelpBox("Color Frames precisa de um recorte para cada quadro (mesma quantidade que Frames). Sem isso, os próprios quadros recebem a cor.", MessageType.Warning);

            var allowed = ball.AllowedColors();
            var palette = ColorPalette.Load();
            if (allowed.Count > 0 && palette != null)
            {
                var names = new string[allowed.Count];
                for (int i = 0; i < allowed.Count; i++) names[i] = palette.colors[allowed[i]]?.name ?? "?";
                int position = Mathf.Max(0, allowed.IndexOf(colorIndex));
                position = EditorGUILayout.Popup("Cor", position, names);
                colorIndex = allowed[position];
            }
            else colorIndex = 0;
            feltColor = EditorGUILayout.ColorField("Cor do feltro", feltColor);

            int count = ball.frames.Length;
            float width = EditorGUIUtility.currentViewWidth - 40f;
            float cell = Mathf.Max(16f, Mathf.Min(MaxCell, Mathf.Floor(width / count)));
            var area = GUILayoutUtility.GetRect(count * cell, cell, GUILayout.ExpandWidth(false));
            area.x += Mathf.Max(0f, (width - count * cell) * 0.5f);
            area.size = new Vector2(count * cell, cell);
            if (Event.current.type != EventType.Repaint) return;

            EditorGUI.DrawRect(area, feltColor);
            var tint = BallTheme.Tint(colorIndex);
            for (int i = 0; i < count; i++)
            {
                var cellRect = new Rect(area.x + i * cell, area.y, cell, cell);
                DrawSprite(ball.shadow, cellRect, ShadowColor);
                DrawSprite(ball.ColorFrame(i), cellRect, tint);
                DrawSprite(ball.frames[i], cellRect, ball.FrameTint(colorIndex));
                DrawSprite(ball.shine, cellRect, Color.white);
            }
        }

        /// <summary>O recorte na célula, no tamanho dele dentro do quadro (respeita o corte do atlas), com a cor.</summary>
        private static void DrawSprite(Sprite sprite, Rect cellRect, Color tint)
        {
            if (sprite == null || sprite.texture == null) return;
            var texture = sprite.texture;
            var full = sprite.rect;
            var source = sprite.packed ? sprite.textureRect : full;
            var offset = sprite.packed ? sprite.textureRectOffset : Vector2.zero;
            var rect = new Rect(
                cellRect.x + offset.x / full.width * cellRect.width,
                cellRect.y + (full.height - offset.y - source.height) / full.height * cellRect.height,
                source.width / full.width * cellRect.width,
                source.height / full.height * cellRect.height);
            var coords = new Rect(source.x / texture.width, source.y / texture.height, source.width / texture.width, source.height / texture.height);
            var color = GUI.color;
            GUI.color = tint;
            GUI.DrawTextureWithTexCoords(rect, texture, coords, true);
            GUI.color = color;
        }
    }
}
