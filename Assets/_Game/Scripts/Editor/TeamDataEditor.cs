using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FutebolDeBotao.Editor
{
    /// <summary>
    /// Inspector do time: os campos de sempre e a prévia do botão e do goleiro de cada uniforme, com as cores e as camisas
    /// escolhidas (em "Sortear", mostra o primeiro padrão como exemplo).
    /// </summary>
    [CustomEditor(typeof(TeamData))]
    public sealed class TeamDataEditor : UnityEditor.Editor
    {
        private const float PreviewHeight = 96f;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var team = (TeamData)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Prévia", EditorStyles.boldLabel);
            bool anyRandom = false;
            for (int i = 0; i < TeamData.UniformCount; i++)
            {
                var disc = Pick(ShirtPattern.Kind.Botao, team.DiscPatternOf(i), out bool discRandom);
                var keeper = Pick(ShirtPattern.Kind.Goleiro, team.KeeperPatternOf(i), out bool keeperRandom);
                if (disc == null && keeper == null)
                {
                    EditorGUILayout.HelpBox("Nenhum padrão de camisa em Resources/PadroesCamisa.", MessageType.Info);
                    return;
                }
                anyRandom |= discRandom || keeperRandom;
                EditorGUILayout.LabelField($"Uniforme {i + 1}", EditorStyles.miniBoldLabel);
                DrawRow(disc, keeper, team.PrimaryOf(i), team.SecondaryOf(i));
            }
            if (anyRandom)
                EditorGUILayout.HelpBox("Em \"Sortear\" a prévia mostra o primeiro padrão; no jogo a IA sorteia um a cada partida.",
                    MessageType.None);
        }

        /// <summary>Uma linha da prévia: o botão e o goleiro pintados com as duas cores.</summary>
        private static void DrawRow(ShirtPattern disc, ShirtPattern keeper, Color primary, Color secondary)
        {
            var discSize = ShirtPreview.Size(disc, PreviewHeight);
            var keeperSize = ShirtPreview.Size(keeper, PreviewHeight * 0.5f);
            var row = GUILayoutUtility.GetRect(discSize.x + keeperSize.x + 24f, PreviewHeight + 8f, GUILayout.ExpandWidth(false));
            EditorGUI.DrawRect(row, new Color(0.1f, 0.4f, 0.2f));
            var discArea = new Rect(row.x + 4f, row.y + 4f, discSize.x, discSize.y);
            var keeperArea = new Rect(discArea.xMax + 16f, row.y + 4f + (PreviewHeight - keeperSize.y) * 0.5f, keeperSize.x, keeperSize.y);
            ShirtPreview.Draw(discArea, disc, primary, secondary);
            ShirtPreview.Draw(keeperArea, keeper, primary, secondary);
        }

        /// <summary>O padrão escolhido, ou o primeiro do tipo quando está em "Sortear" (ou o escolhido sumiu).</summary>
        private static ShirtPattern Pick(ShirtPattern.Kind kind, string id, out bool random)
        {
            var chosen = ShirtPattern.FindOf(kind, id);
            random = chosen == null;
            if (chosen != null) return chosen;
            var all = ShirtPattern.Of(kind, false);
            return all.Count > 0 ? all[0] : null;
        }
    }

    /// <summary>
    /// Times antigos (com a cor reserva, sem Uniforme 2): ao abrir o editor, todos ganham o Uniforme 2 (a reserva, ou as
    /// cores invertidas) e são gravados, para o asset convertido ir para o git.
    /// </summary>
    internal static class TeamUniformConverter
    {
        [InitializeOnLoadMethod]
        private static void Schedule() => EditorApplication.delayCall += ConvertAll;

        private static void ConvertAll()
        {
            int converted = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:TeamData"))
            {
                var team = AssetDatabase.LoadAssetAtPath<TeamData>(AssetDatabase.GUIDToAssetPath(guid));
                if (team == null || !team.EnsureUniform2()) continue;
                EditorUtility.SetDirty(team);
                converted++;
            }
            if (converted == 0) return;
            AssetDatabase.SaveAssets();
            Debug.Log($"Times convertidos para Uniforme 1 e 2: {converted}.");
        }
    }

    /// <summary>Lista dos padrões do tipo (mais "Sortear") no lugar de digitar o id.</summary>
    [CustomPropertyDrawer(typeof(ShirtPatternIdAttribute))]
    public sealed class ShirtPatternIdDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var kind = ((ShirtPatternIdAttribute)attribute).Kind;
            var patterns = ShirtPattern.Of(kind, false);
            var ids = new List<string> { string.Empty };
            var names = new List<string> { "Sortear" };
            foreach (var pattern in patterns)
            {
                ids.Add(pattern.id);
                names.Add(pattern.unlockedByDefault ? $"{pattern.displayName} ({pattern.id})" : $"{pattern.displayName} ({pattern.id}, loja)");
            }

            string current = property.stringValue ?? string.Empty;
            int index = ids.IndexOf(current);
            if (index < 0)
            {
                // Id que não existe mais: aparece na lista para não sumir sem aviso.
                ids.Add(current);
                names.Add($"{current} (não encontrado)");
                index = ids.Count - 1;
            }

            EditorGUI.BeginProperty(position, label, property);
            int chosen = EditorGUI.Popup(position, label.text, index, names.ToArray());
            if (chosen != index) property.stringValue = ids[chosen];
            EditorGUI.EndProperty();
        }
    }
}
