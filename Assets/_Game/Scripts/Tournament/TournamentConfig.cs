using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>Como o torneio é disputado.</summary>
    public enum TournamentFormat
    {
        /// <summary>Chave de 2 a 32 times (potência de 2), com 3º lugar.</summary>
        MataMata,
        /// <summary>Grupos (todos contra todos no grupo), e os primeiros de cada grupo vão para o mata-mata.</summary>
        Copa,
        /// <summary>Pontos corridos: todos contra todos, campeão quem fizer mais pontos. Tem save próprio (Liga no Jogar).</summary>
        Liga
    }

    /// <summary>
    /// Um torneio (asset em Resources/Torneios): formato, quantos times, os prêmios por colocação, o peso no ranking
    /// semanal, os placares sorteados dos jogos sorteados e os troféus. Sem asset, vale o padrão do código (8 times, 50/25/10).
    /// </summary>
    [CreateAssetMenu(menuName = "Futebol de Botão/Torneio", fileName = "Torneio")]
    public sealed class TournamentConfig : ScriptableObject
    {
        public const string ResourceFolder = "Torneios";

        public string id = "copa8";
        public string displayName = "Copa";
        [Tooltip("Mata-mata: só a chave. Copa: grupos e depois mata-mata. Liga: pontos corridos (aparece no botão Liga).")]
        public TournamentFormat format = TournamentFormat.MataMata;
        [Tooltip("Times: o jogador + times sorteados do catálogo. Mata-mata: 8, 16 ou 32. Copa: múltiplo do tamanho do grupo.")]
        public int teamCount = 8;
        [Header("Copa (grupos)")]
        [Tooltip("Times em cada grupo.")]
        [Min(2)] public int groupSize = 4;
        [Tooltip("Classificados de cada grupo para o mata-mata. Grupos × classificados precisa dar 2, 4, 8, 16 ou 32.")]
        [Min(1)] public int qualifiersPerGroup = 2;
        [Header("Copa e Liga")]
        [Tooltip("Turnos: 1 = só ida; 2 = ida e volta (mando invertido).")]
        [Range(1, 2)] public int legs = 1;
        [Header("Prêmios")]
        [Tooltip("Multiplica os pontos do ranking semanal (Copa 8 = 1).")]
        [Min(0f)] public float rankingWeight = 1f;
        [Tooltip("Moedas por colocação: campeão, vice, 3º lugar... (quem não está na lista não ganha prêmio).")]
        public int[] prizes = { 50, 25, 10 };
        [Tooltip("Placar dos jogos entre os outros times (pesos de 0 a 10 gols). Vazio: o padrão (Resources/Torneios/Placares).")]
        public SimulatedScores simulatedScores;
        [Tooltip("Prorrogação dos jogos do jogador empatados, em % do tempo (depois vêm os pênaltis).")]
        [Range(10, 100)] public int extraTimePercent = 50;
        [Tooltip("Troféus por colocação: campeão, vice, 3º lugar. Vazio: um troféu simples feito pelo código.")]
        public Sprite[] trophies = new Sprite[3];

        public bool IsLeague => format == TournamentFormat.Liga;
        public bool HasGroups => format != TournamentFormat.MataMata;

        /// <summary>Times no torneio: mata-mata em potência de 2 (2 a 32); copa em grupos inteiros; liga de 2 a 40.</summary>
        public int TeamCount => format switch
        {
            TournamentFormat.Copa => GroupCount * GroupSize,
            TournamentFormat.Liga => Mathf.Clamp(teamCount, 2, 40),
            _ => Mathf.Clamp(Mathf.ClosestPowerOfTwo(Mathf.Max(teamCount, 2)), 2, 32)
        };

        public int GroupSize => Mathf.Max(2, groupSize);

        /// <summary>Grupos da copa (a liga é um grupo só; o mata-mata não tem).</summary>
        public int GroupCount => format switch
        {
            TournamentFormat.Copa => Mathf.Max(1, Mathf.Max(teamCount, GroupSize) / GroupSize),
            TournamentFormat.Liga => 1,
            _ => 0
        };

        /// <summary>Times no mata-mata: a chave toda, ou os classificados dos grupos (potência de 2); liga não tem.</summary>
        public int KnockoutTeams => format switch
        {
            TournamentFormat.Copa => Mathf.Clamp(Mathf.ClosestPowerOfTwo(Mathf.Max(2, GroupCount * Mathf.Min(qualifiersPerGroup, GroupSize))), 2, 32),
            TournamentFormat.Liga => 0,
            _ => TeamCount
        };

        /// <summary>Classificados por grupo (o que fecha com <see cref="KnockoutTeams"/>).</summary>
        public int Qualifiers => GroupCount > 0 ? Mathf.Max(1, KnockoutTeams / GroupCount) : 0;

        /// <summary>Fases do mata-mata (8 times = 3: quartas, semifinal e final; liga = 0).</summary>
        public int Rounds => KnockoutTeams >= 2 ? Mathf.RoundToInt(Mathf.Log(KnockoutTeams, 2f)) : 0;

        public int PrizeFor(int position) => prizes != null && position >= 1 && position <= prizes.Length ? Mathf.Max(0, prizes[position - 1]) : 0;

        /// <summary>Os pesos de placar deste torneio (ou o padrão).</summary>
        public SimulatedScores Scores => simulatedScores != null ? simulatedScores : SimulatedScores.Default;

        public Sprite TrophyFor(int position) =>
            trophies != null && position >= 1 && position <= trophies.Length ? trophies[position - 1] : null;

        /// <summary>Os torneios de um tipo (ligas ou não) que o catálogo tem times para montar.</summary>
        public static List<TournamentConfig> Available(bool leagues)
        {
            int teams = TeamCatalog.Load().TeamCount;
            var list = new List<TournamentConfig>();
            foreach (var config in All())
                if (config != null && config.IsLeague == leagues && config.TeamCount - 1 <= teams) list.Add(config);
            return list;
        }

        /// <summary>O escolhido, se é do tipo e dá para montar; senão o primeiro do tipo (ou nulo se não há nenhum).</summary>
        public static TournamentConfig FindAvailable(string configId, bool leagues)
        {
            var list = Available(leagues);
            foreach (var config in list)
                if (config.id == configId) return config;
            return list.Count > 0 ? list[0] : null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (format == TournamentFormat.Copa && GroupCount * Mathf.Min(qualifiersPerGroup, GroupSize) != KnockoutTeams)
                Debug.LogWarning($"Torneio {name}: grupos ({GroupCount}) × classificados ({qualifiersPerGroup}) não dá 2, 4, 8, 16 ou 32; " +
                                 $"o mata-mata fica com {KnockoutTeams}.", this);
        }
#endif

        private static TournamentConfig[] cached;
        private static TournamentConfig fallback;

        public static TournamentConfig[] All()
        {
            if (cached != null) return cached;
            var list = new List<TournamentConfig>(Resources.LoadAll<TournamentConfig>(ResourceFolder));
            list.Sort((a, b) => a.format != b.format ? a.format.CompareTo(b.format)
                : a.TeamCount != b.TeamCount ? a.TeamCount.CompareTo(b.TeamCount) : string.CompareOrdinal(a.id, b.id));
            cached = list.ToArray();
            return cached;
        }

        /// <summary>O torneio do id; sem ele, o primeiro (o menor); sem nenhum asset, o padrão do código.</summary>
        public static TournamentConfig Find(string configId)
        {
            var all = All();
            foreach (var config in all)
                if (config != null && config.id == configId) return config;
            if (all.Length > 0) return all[0];
            if (fallback == null)
            {
                fallback = CreateInstance<TournamentConfig>();
                fallback.name = "Torneio (padrão)";
                fallback.hideFlags = HideFlags.DontSave;
            }
            return fallback;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => cached = null;
    }
}
