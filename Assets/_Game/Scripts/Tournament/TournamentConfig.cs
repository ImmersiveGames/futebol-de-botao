using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Um torneio mata-mata (asset em Resources/Torneios): quantos times, os prêmios por colocação, o máximo de gols dos
    /// jogos sorteados e os troféus. Sem asset, vale o padrão do código (8 times, 50/25/10).
    /// </summary>
    [CreateAssetMenu(menuName = "Futebol de Botão/Torneio", fileName = "Torneio")]
    public sealed class TournamentConfig : ScriptableObject
    {
        public const string ResourceFolder = "Torneios";

        public string id = "copa8";
        public string displayName = "Copa";
        [Tooltip("Times na chave: 8, 16 ou 32 (potência de 2). O jogador + times sorteados do catálogo.")]
        public int teamCount = 8;
        [Tooltip("Moedas por colocação: campeão, vice, 3º lugar... (quem não está na lista não ganha prêmio).")]
        public int[] prizes = { 50, 25, 10 };
        [Tooltip("Jogos entre os outros times: cada um faz de 0 até este número de gols (sorteado).")]
        [Min(0)] public int maxSimulatedGoals = 3;
        [Tooltip("Prorrogação dos jogos do jogador empatados, em % do tempo (depois vêm os pênaltis).")]
        [Range(10, 100)] public int extraTimePercent = 50;
        [Tooltip("Troféus por colocação: campeão, vice, 3º lugar. Vazio: um troféu simples feito pelo código.")]
        public Sprite[] trophies = new Sprite[3];

        /// <summary>Times na chave: potência de 2, de 2 a 32.</summary>
        public int TeamCount => Mathf.Clamp(Mathf.ClosestPowerOfTwo(Mathf.Max(teamCount, 2)), 2, 32);

        /// <summary>Número de fases (8 times = 3: quartas, semifinal e final).</summary>
        public int Rounds => Mathf.RoundToInt(Mathf.Log(TeamCount, 2f));

        public int PrizeFor(int position) => prizes != null && position >= 1 && position <= prizes.Length ? Mathf.Max(0, prizes[position - 1]) : 0;

        public Sprite TrophyFor(int position) =>
            trophies != null && position >= 1 && position <= trophies.Length ? trophies[position - 1] : null;

        private static TournamentConfig[] cached;
        private static TournamentConfig fallback;

        public static TournamentConfig[] All()
        {
            if (cached != null) return cached;
            var list = new List<TournamentConfig>(Resources.LoadAll<TournamentConfig>(ResourceFolder));
            list.Sort((a, b) => a.TeamCount != b.TeamCount ? a.TeamCount.CompareTo(b.TeamCount) : string.CompareOrdinal(a.id, b.id));
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
