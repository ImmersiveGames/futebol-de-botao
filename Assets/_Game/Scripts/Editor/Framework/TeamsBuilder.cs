using System.IO;
using UnityEditor;
using UnityEngine;

namespace FutebolDeBotao.Editor
{
    /// <summary>
    /// Times e esquemas táticos da Seleção de times, em Assets/_Game/Resources/Times.
    /// Só cria o que falta: nome, cores ou posições editados no Inspector não são sobrescritos.
    /// "Criar telas" roda isto antes de montar o Menu.
    /// </summary>
    public static class TeamsBuilder
    {
        private const string Folder = "Assets/_Game/Resources/Times";
        private const string SchemesFolder = Folder + "/Esquemas";
        private const string CatalogPath = "Assets/_Game/Resources/" + TeamCatalog.ResourcePath + ".asset";

        // Cores do GDD: cor do botão e do anel (o anel separa os pares parecidos).
        private static readonly (string name, string abbreviation, Color primary, Color secondary)[] Teams =
        {
            ("Azul", "AZU", new Color(0.2f, 0.45f, 0.95f), Color.white),
            ("Vermelho", "VRM", new Color(0.9f, 0.2f, 0.2f), Color.white),
            ("Branco", "BRA", new Color(0.95f, 0.95f, 0.95f), new Color(0.15f, 0.15f, 0.15f)),
            ("Preto", "PRE", new Color(0.12f, 0.12f, 0.12f), Color.white),
            ("Amarelo", "AMA", new Color(1f, 0.85f, 0.15f), new Color(0.1f, 0.45f, 0.2f)),
            ("Verde", "VRD", new Color(0.15f, 0.65f, 0.25f), Color.white),
            ("Laranja", "LAR", new Color(1f, 0.55f, 0.1f), new Color(0.15f, 0.15f, 0.15f)),
            ("Roxo", "ROX", new Color(0.5f, 0.25f, 0.75f), Color.white),
            ("Rosa", "ROS", new Color(1f, 0.5f, 0.75f), Color.white),
            ("Ciano", "CIA", new Color(0.2f, 0.8f, 0.9f), new Color(0.05f, 0.2f, 0.45f)),
            ("Grena", "GRE", new Color(0.5f, 0.1f, 0.15f), new Color(1f, 0.8f, 0.2f))
        };

        [MenuItem("Futebol de Botão/Criar times")]
        public static void Build()
        {
            var catalog = CreateAssets();
            if (catalog != null) Debug.Log($"[Futebol de Botão] Times e esquemas em {Folder}. Catálogo: {CatalogPath}.");
        }

        public static TeamCatalog CreateAssets()
        {
            Directory.CreateDirectory(SchemesFolder);
            AssetDatabase.Refresh();

            var teams = new TeamData[Teams.Length];
            for (int i = 0; i < Teams.Length; i++)
            {
                var (teamName, abbreviation, primary, secondary) = Teams[i];
                teams[i] = LoadOrCreate<TeamData>($"{Folder}/{teamName}.asset", team => team.Set(teamName, abbreviation, primary, secondary));
            }

            var schemes5 = new[]
            {
                Scheme("5 - 2-2-1", "2-2-1", "Equilibrado", Formation.Default5),
                Scheme("5 - 3-2", "3-2", "Defensivo", Formation.Defensive5),
                Scheme("5 - 2-3", "2-3", "Ofensivo", Formation.Offensive5),
                Scheme("5 - 2-1-2", "2-1-2", "Losango", Formation.Diamond5)
            };
            var schemes3 = new[]
            {
                Scheme("3 - 2-1", "2-1", "Equilibrado", Formation.Default3),
                Scheme("3 - 1-2", "1-2", "Ofensivo", Formation.Offensive3)
            };

            var catalog = LoadOrCreate<TeamCatalog>(CatalogPath, _ => { });
            // O catálogo sempre lista os assets da pasta na ordem acima.
            catalog.Set(teams, schemes5, schemes3);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        private static Formation Scheme(string assetName, string displayName, string style, Vector2[] positions) =>
            LoadOrCreate<Formation>($"{SchemesFolder}/{assetName}.asset", formation => formation.Set(displayName, style, positions));

        private static T LoadOrCreate<T>(string path, System.Action<T> initialize) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            initialize(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
