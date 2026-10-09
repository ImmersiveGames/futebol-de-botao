using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Times e esquemas táticos que aparecem na Seleção de times. Fica em Resources/Times/CatalogoDeTimes
    /// (criado por "Futebol de Botão/Criar telas"); sem o asset, o jogo usa Azul x Vermelho e os esquemas padrão.
    /// </summary>
    [CreateAssetMenu(menuName = "Futebol de Botão/Catálogo de times", fileName = "CatalogoDeTimes")]
    public sealed class TeamCatalog : ScriptableObject
    {
        public const string ResourcePath = "Times/CatalogoDeTimes";

        [SerializeField] private TeamData[] teams = new TeamData[0];
        [Tooltip("Esquemas para 5 botões; o primeiro é o padrão.")]
        [SerializeField] private Formation[] schemes5 = new Formation[0];
        [Tooltip("Esquemas para 3 botões; o primeiro é o padrão.")]
        [SerializeField] private Formation[] schemes3 = new Formation[0];

        private static TeamCatalog cached;

        public int TeamCount => teams.Length;
        public TeamData Team(int index) => teams[Wrap(index, teams.Length)];
        public int IndexOf(TeamData team) => System.Array.IndexOf(teams, team);

        public int SchemeCount(int discs) => Schemes(discs).Length;
        public Formation Scheme(int discs, int index)
        {
            var list = Schemes(discs);
            return list[Wrap(index, list.Length)];
        }

        private Formation[] Schemes(int discs) => discs == 3 ? schemes3 : schemes5;

        public void Set(TeamData[] newTeams, Formation[] newSchemes5, Formation[] newSchemes3)
        {
            teams = newTeams;
            schemes5 = newSchemes5;
            schemes3 = newSchemes3;
        }

        public static TeamCatalog Load()
        {
            if (cached != null) return cached;
            cached = Resources.Load<TeamCatalog>(ResourcePath);
            if (cached == null || cached.teams.Length < 2 || cached.schemes5.Length == 0 || cached.schemes3.Length == 0)
            {
                if (cached != null) Debug.LogWarning("[Futebol de Botão] Catálogo de times incompleto; usando Azul x Vermelho.");
                cached = CreateFallback();
            }
            return cached;
        }

        private static TeamCatalog CreateFallback()
        {
            var catalog = CreateInstance<TeamCatalog>();
            catalog.name = "Catálogo padrão";
            catalog.hideFlags = HideFlags.DontSave;
            catalog.Set(
                new[]
                {
                    TeamData.Create("Azul", "AZU", new Color(0.2f, 0.45f, 0.95f), Color.white),
                    TeamData.Create("Vermelho", "VRM", new Color(0.9f, 0.2f, 0.2f), Color.white)
                },
                new[] { Formation.Create("2-2-1", "Equilibrado", Formation.Default5) },
                new[] { Formation.Create("2-1", "Equilibrado", Formation.Default3) });
            return catalog;
        }

        private static int Wrap(int value, int count) => count == 0 ? 0 : ((value % count) + count) % count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => cached = null;
    }
}
