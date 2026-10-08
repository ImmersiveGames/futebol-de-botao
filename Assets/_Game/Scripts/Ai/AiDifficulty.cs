using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Números de um nível da IA (Fácil, Médio, Difícil). Quanto mais jogadas ela testa e menos erra, mais forte fica.
    /// </summary>
    [CreateAssetMenu(menuName = "Futebol de Botão/Nível da IA", fileName = "NivelDaIA")]
    public sealed class AiDifficulty : ScriptableObject
    {
        [Header("Escolha da jogada")]
        [Tooltip("Quantos botões, dos mais perto da bola, a IA considera.")]
        [Min(1)] public int discsToTest = 3;
        [Tooltip("Quantas jogadas, das melhores pela geometria, vão para a mesa simulada.")]
        [Min(1)] public int simulations = 8;
        [Tooltip("Escolhe ao acaso entre as N melhores jogadas simuladas. 1 = sempre a melhor.")]
        [Min(1)] public int pickAmongBest = 2;
        [Tooltip("Quanto a IA evita jogadas com risco de falta. 1 = evita bastante; menor = arrisca mais.")]
        [Range(0f, 1f)] public float foulCaution = 1f;

        [Header("Erro na execução")]
        [Tooltip("Erro máximo no ângulo do peteleco, em graus para cada lado.")]
        [Min(0f)] public float angleErrorDegrees = 4f;
        [Tooltip("Erro máximo na força, em fração (0,08 = 8% a mais ou a menos).")]
        [Range(0f, 0.5f)] public float powerError = 0.08f;

        [Header("Vai chutar e goleiro")]
        [Tooltip("Anuncia o \"Vai chutar\" quando a bola está a esta distância do gol (ou menos) e o caminho está livre.")]
        [Min(0f)] public float shotCallDistance = 4.5f;
        [Tooltip("Erro máximo do goleiro da IA, em unidades para cada lado.")]
        [Min(0f)] public float keeperError = 0.25f;

        [Header("Ritmo")]
        [Tooltip("Pausa antes de começar a pensar.")]
        [Min(0f)] public float thinkSeconds = 0.4f;
        [Tooltip("Tempo mostrando a mira e a barra de força enchendo antes de chutar.")]
        [Min(0f)] public float aimSeconds = 0.7f;

        /// <summary>Nível Médio do plano, para usar enquanto não há assets dos 3 níveis.</summary>
        public static AiDifficulty CreateMedium()
        {
            var difficulty = CreateInstance<AiDifficulty>();
            difficulty.name = "Médio (padrão)";
            difficulty.hideFlags = HideFlags.DontSave;
            return difficulty;
        }
    }
}
