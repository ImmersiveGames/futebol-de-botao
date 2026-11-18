using UnityEditor;

namespace FutebolDeBotao.Editor
{
    /// <summary>
    /// Inspector do torneio: mostra só os campos do formato escolhido (grupos só na Copa; turnos na Copa e na Liga;
    /// prorrogação só onde tem mata-mata).
    /// </summary>
    [CustomEditor(typeof(TournamentConfig))]
    public sealed class TournamentConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var format = (TournamentFormat)serializedObject.FindProperty("format").enumValueIndex;
            var property = serializedObject.GetIterator();
            bool enter = true;
            while (property.NextVisible(enter))
            {
                enter = false;
                if (!Shown(property.name, format)) continue;
                using (new EditorGUI.DisabledScope(property.name == "m_Script"))
                    EditorGUILayout.PropertyField(property, true);
            }
            if (format == TournamentFormat.Copa)
            {
                var config = (TournamentConfig)target;
                EditorGUILayout.HelpBox($"{config.GroupCount} grupos de {config.GroupSize}; {config.KnockoutTeams} no mata-mata " +
                                        $"({config.Qualifiers} por grupo).", MessageType.Info);
            }
            if (format == TournamentFormat.Liga)
            {
                var config = (TournamentConfig)target;
                int teams = config.TeamCount;
                EditorGUILayout.HelpBox($"1 turno: {teams - 1} jogos, campeão {config.PrizeFor(1, 1)} moedas, peso {config.RankingWeightFor(1):0.##}.\n" +
                                        $"2 turnos: {2 * (teams - 1)} jogos, campeão {config.PrizeFor(1, 2)} moedas, peso {config.RankingWeightFor(2):0.##}.",
                    MessageType.Info);
            }
            serializedObject.ApplyModifiedProperties();
        }

        private static bool Shown(string name, TournamentFormat format) => name switch
        {
            "groupSize" or "qualifiersPerGroup" => format == TournamentFormat.Copa,
            "legs" => format != TournamentFormat.MataMata,
            "extraTimePercent" => format != TournamentFormat.Liga,
            "twoLegsPrizeMultiplier" or "twoLegsRankingMultiplier" => format == TournamentFormat.Liga,
            _ => true
        };
    }
}
