using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FutebolDeBotao.Editor
{
    /// <summary>
    /// Inspector da mesa: os campos de sempre e a prévia da mesa inteira montada como na Partida (placas, camadas de cor,
    /// Fill, rede e trave), com gol grande ou pequeno, a cor escolhida e a cor do feltro por baixo.
    /// </summary>
    [CustomEditor(typeof(TableTheme))]
    public sealed class TableThemeEditor : UnityEditor.Editor
    {
        private const float MaxCell = 24f;

        private bool smallGoal;
        private int colorIndex;
        private Color feltColor = new(0.102f, 0.478f, 0.243f);

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var table = (TableTheme)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Prévia", EditorStyles.boldLabel);
            if (!table.HasBoards)
            {
                EditorGUILayout.HelpBox("Faltam peças de placa (Corner, Wall, Big Goal, Big Goal Corner, Small Goal, Small Goal Corner): a Partida usa a moldura antiga da cena.", MessageType.Info);
                return;
            }
            if (table.goal == null || !table.goal.Complete)
                EditorGUILayout.HelpBox("Sem trave completa: os gols ficam sem trave.", MessageType.Warning);

            smallGoal = EditorGUILayout.Toggle("Gol pequeno (sem goleiro)", smallGoal);
            var palette = ColorPalette.Load();
            int count = palette.Count;
            if (count > 0)
            {
                var names = new string[count];
                for (int i = 0; i < count; i++) names[i] = palette.colors[i]?.name ?? "?";
                colorIndex = EditorGUILayout.Popup("Cor", Mathf.Clamp(colorIndex, 0, count - 1), names);
            }
            feltColor = EditorGUILayout.ColorField("Cor do feltro", feltColor);

            var pieces = table.Boards(smallGoal, colorIndex);
            pieces.AddRange(table.Goals(smallGoal, colorIndex));
            // Mesma ordem da Partida: Fill, rede, placa, camada de cor, trave.
            var ordered = new List<TableTheme.Piece>();
            foreach (var kind in new[] { TableTheme.PieceKind.Fill, TableTheme.PieceKind.Net, TableTheme.PieceKind.Board, TableTheme.PieceKind.BoardColor, TableTheme.PieceKind.Frame })
                foreach (var piece in pieces)
                    if (piece.Kind == kind) ordered.Add(piece);

            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (var piece in ordered)
            {
                minX = Mathf.Min(minX, piece.Position.x - 0.5f);
                maxX = Mathf.Max(maxX, piece.Position.x + 0.5f);
                minY = Mathf.Min(minY, piece.Position.y - 0.5f);
                maxY = Mathf.Max(maxY, piece.Position.y + 0.5f);
            }
            float columns = maxX - minX, rows = maxY - minY;
            float width = EditorGUIUtility.currentViewWidth - 40f;
            float cell = Mathf.Max(4f, Mathf.Min(MaxCell, Mathf.Floor(width / columns)));
            var area = GUILayoutUtility.GetRect(columns * cell, rows * cell, GUILayout.ExpandWidth(false));
            area.x += Mathf.Max(0f, (width - columns * cell) * 0.5f);
            area.size = new Vector2(columns * cell, rows * cell);
            if (Event.current.type != EventType.Repaint) return;

            // O feltro passa por baixo de tudo, como na Partida.
            EditorGUI.DrawRect(area, feltColor);
            foreach (var piece in ordered)
            {
                var cellRect = new Rect(area.x + (piece.Position.x - 0.5f - minX) * cell, area.y + (maxY - piece.Position.y - 0.5f) * cell, cell, cell);
                DrawPiece(piece, cellRect);
            }
        }

        /// <summary>Uma peça na célula: espelhada e depois girada (no sentido do jogo) em volta do centro, com a cor.</summary>
        private static void DrawPiece(TableTheme.Piece piece, Rect cellRect)
        {
            var sprite = piece.Sprite;
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
            if (piece.FlipX)
            {
                rect.x = cellRect.xMax - (rect.x - cellRect.x) - rect.width;
                coords = new Rect(coords.xMax, coords.y, -coords.width, coords.height);
            }

            var matrix = GUI.matrix;
            var color = GUI.color;
            // No jogo o giro é anti-horário com o y para cima; na tela do editor o y desce, então o sinal inverte.
            GUIUtility.RotateAroundPivot(-piece.Rotation, cellRect.center);
            GUI.color = piece.Tint;
            GUI.DrawTextureWithTexCoords(rect, texture, coords, true);
            GUI.color = color;
            GUI.matrix = matrix;
        }
    }
}
