using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FutebolDeBotao.Editor
{
    /// <summary>
    /// Desenha os campos de um asset como o Inspector padrão, trocando as listas de nomes de cor (string[]) pelo dropdown
    /// de marcar da Color Palette, como o das camadas da física: tudo marcado = qualquer cor (a lista fica vazia);
    /// desmarcar tira a cor. Usado nos Inspectors da bola e do feltro.
    /// </summary>
    public static class PaletteMaskInspector
    {
        public static void Draw(SerializedObject serializedObject, params string[] colorFields)
        {
            serializedObject.Update();
            var fields = new HashSet<string>(colorFields);
            var property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                using (new EditorGUI.DisabledScope(property.propertyPath == "m_Script"))
                {
                    if (fields.Contains(property.name) && property.isArray && property.propertyType != SerializedPropertyType.String)
                        DrawMask(property);
                    else EditorGUILayout.PropertyField(property, true);
                }
            }
            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawMask(SerializedProperty list)
        {
            var palette = ColorPalette.Load();
            var names = new List<string>();
            if (palette != null)
                foreach (var entry in palette.colors)
                    if (entry != null && !string.IsNullOrWhiteSpace(entry.name) && !names.Contains(entry.name.Trim()))
                        names.Add(entry.name.Trim());
            // O dropdown de marcar guarda as escolhas num int: até 31 cores (o bit de cima é o "Everything").
            if (names.Count == 0 || names.Count > 31)
            {
                EditorGUILayout.PropertyField(list, true);
                return;
            }

            int all = (1 << names.Count) - 1;
            int mask = 0;
            var unknown = new List<string>();
            for (int i = 0; i < list.arraySize; i++)
            {
                string value = list.GetArrayElementAtIndex(i).stringValue?.Trim() ?? string.Empty;
                int index = names.FindIndex(n => string.Equals(n, value, System.StringComparison.OrdinalIgnoreCase));
                if (index >= 0) mask |= 1 << index;
                else if (value.Length > 0) unknown.Add(value);
            }
            if (list.arraySize == 0 || mask == all) mask = -1;

            var label = new GUIContent(list.displayName, "Marcadas = cores aceitas. Tudo marcado (ou nada) = qualquer cor da Color Palette.");
            EditorGUI.showMixedValue = list.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            int picked = EditorGUILayout.MaskField(label, mask, names.ToArray());
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
            {
                var chosen = new List<string>();
                if (picked != -1 && (picked & all) != all && (picked & all) != 0)
                    for (int i = 0; i < names.Count; i++)
                        if ((picked & (1 << i)) != 0) chosen.Add(names[i]);
                list.arraySize = chosen.Count;
                for (int i = 0; i < chosen.Count; i++) list.GetArrayElementAtIndex(i).stringValue = chosen[i];
            }
            if (unknown.Count > 0)
                EditorGUILayout.HelpBox($"Cores que não existem mais na Color Palette: {string.Join(", ", unknown)}. Mexer no dropdown tira essas.", MessageType.Warning);
        }
    }

    /// <summary>Inspector do feltro: os campos de sempre, com as cores de trave no dropdown de marcar.</summary>
    [CustomEditor(typeof(FeltTheme))]
    [CanEditMultipleObjects]
    public sealed class FeltThemeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI() => PaletteMaskInspector.Draw(serializedObject, nameof(FeltTheme.goalColors));
    }
}
