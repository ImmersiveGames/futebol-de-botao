using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Placar dos jogos sorteados do torneio (os que o jogador não joga): peso de cada número de gols, de 0 a 10, para
    /// cada time. Um asset só (Resources/Torneios/Placares) serve todos os torneios; para um torneio diferente, crie outro
    /// e aponte nele.
    /// </summary>
    [CreateAssetMenu(menuName = "Futebol de Botão/Placares sorteados", fileName = "Placares")]
    public sealed class SimulatedScores : ScriptableObject
    {
        public const string DefaultPath = "Torneios/Placares";

        /// <summary>Pesos padrão: 0 a 3 iguais, depois caindo (4 = 6, 5 = 4, 6 = 2,5, 7 = 0,6... 10 = 0,05).</summary>
        public static readonly float[] DefaultWeights = { 10f, 10f, 10f, 10f, 6f, 4f, 2.5f, 0.6f, 0.3f, 0.1f, 0.05f };

        [Tooltip("Peso de cada número de gols: a posição 0 é 0 gol, a 1 é 1 gol... até 10. Maior = sai mais.")]
        public float[] weights = (float[])DefaultWeights.Clone();

        private static SimulatedScores fallback;

        /// <summary>O asset padrão (Resources/Torneios/Placares); sem ele, os pesos do código.</summary>
        public static SimulatedScores Default
        {
            get
            {
                if (fallback != null) return fallback;
                fallback = Resources.Load<SimulatedScores>(DefaultPath);
                if (fallback == null)
                {
                    fallback = CreateInstance<SimulatedScores>();
                    fallback.hideFlags = HideFlags.DontSave;
                }
                return fallback;
            }
        }

        /// <summary>Gols de um time, sorteados pelos pesos.</summary>
        public int Roll()
        {
            var list = weights != null && weights.Length > 0 ? weights : DefaultWeights;
            float total = 0f;
            foreach (float weight in list) total += Mathf.Max(0f, weight);
            if (total <= 0f) return 0;
            float pick = Random.value * total;
            for (int goals = 0; goals < list.Length; goals++)
            {
                pick -= Mathf.Max(0f, list[goals]);
                if (pick < 0f) return goals;
            }
            return list.Length - 1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => fallback = null;
    }
}
