using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FutebolDeBotao.Editor
{
    /// <summary>Lista com os nomes das cores da Color Palette, no lugar de digitar o nome.</summary>
    [CustomPropertyDrawer(typeof(GoalColorNameAttribute))]
    public sealed class GoalColorNameDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var names = new List<string>();
            foreach (var entry in ColorPalette.Load().colors)
                if (entry != null && !string.IsNullOrWhiteSpace(entry.name) && !names.Contains(entry.name.Trim()))
                    names.Add(entry.name.Trim());

            string current = property.stringValue?.Trim() ?? string.Empty;
            int index = names.FindIndex(n => string.Equals(n, current, System.StringComparison.OrdinalIgnoreCase));
            var options = new List<string>(names);
            // Nome que não existe mais em nenhuma mesa: aparece marcado, para não sumir sem aviso.
            if (index < 0)
            {
                options.Insert(0, string.IsNullOrEmpty(current) ? "(escolha)" : $"{current} (não existe na paleta)");
                index = 0;
            }

            EditorGUI.BeginProperty(position, label, property);
            int picked = EditorGUI.Popup(position, label.text, index, options.ToArray());
            if (picked != index || (names.Count > 0 && !names.Contains(current) && picked != 0))
                property.stringValue = options.Count > names.Count && picked == 0 ? property.stringValue : options[picked];
            EditorGUI.EndProperty();
        }
    }
}
