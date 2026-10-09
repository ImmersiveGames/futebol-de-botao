using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>Níveis da IA escolhidos no Menu.</summary>
    public enum AiLevel
    {
        Easy,
        Medium,
        Hard
    }

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

        [Header("Posicionamento")]
        [Tooltip("Quando a melhor jogada é ruim (falta provável ou sem boa chance na bola), chance de só reposicionar um " +
                 "botão sem tocar na bola, em vez de arriscar. A vez passa. Fácil ~0,2; Médio ~0,5; Difícil ~0,8.")]
        [Range(0f, 1f)] public float repositionChance = 0.5f;
        [Tooltip("Nota abaixo da qual a melhor jogada conta como ruim.")]
        public float poorShotScore = 0f;
        [Tooltip("Quando toda jogada na bola tem risco de gol contra (testando o erro de mira e de força), chance de só " +
                 "pôr um botão bloqueando entre a bola e o gol. Fácil 0,4; Médio 0,85; Difícil 1.")]
        [Range(0f, 1f)] public float ownGoalDefendChance = 0.85f;

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
        [Tooltip("Quantos chutes do adversário o goleiro da IA considera para escolher onde ficar. Mais = cobre melhor o gol.")]
        [Min(1)] public int keeperShotsToConsider = 6;

        [Header("Ritmo")]
        [Tooltip("Pausa antes de começar a pensar.")]
        [Min(0f)] public float thinkSeconds = 0.4f;
        [Tooltip("Tempo mostrando a mira e a barra de força enchendo antes de chutar.")]
        [Min(0f)] public float aimSeconds = 0.7f;

        /// <summary>
        /// Asset do nível em Resources/IA (Facil, Medio, Dificil), que você ajusta no Inspector.
        /// Sem o asset, usa os números do plano direto do código.
        /// </summary>
        public static AiDifficulty Load(AiLevel level)
        {
            var asset = Resources.Load<AiDifficulty>($"IA/{FileName(level)}");
            return asset != null ? asset : Create(level);
        }

        public static string FileName(AiLevel level) => level switch
        {
            AiLevel.Easy => "Facil",
            AiLevel.Hard => "Dificil",
            _ => "Medio"
        };

        public static string DisplayName(AiLevel level) => level switch
        {
            AiLevel.Easy => "Fácil",
            AiLevel.Hard => "Difícil",
            _ => "Médio"
        };

        /// <summary>Números do plano da IA para cada nível (os mesmos dos assets).</summary>
        public static AiDifficulty Create(AiLevel level)
        {
            var difficulty = CreateInstance<AiDifficulty>();
            difficulty.name = $"{DisplayName(level)} (padrão)";
            difficulty.hideFlags = HideFlags.DontSave;
            switch (level)
            {
                case AiLevel.Easy:
                    difficulty.discsToTest = 2;
                    difficulty.simulations = 4;
                    difficulty.pickAmongBest = 3;
                    difficulty.foulCaution = 0.5f;
                    difficulty.repositionChance = 0.2f;
                    difficulty.ownGoalDefendChance = 0.4f;
                    difficulty.angleErrorDegrees = 8f;
                    difficulty.powerError = 0.15f;
                    difficulty.shotCallDistance = 3f;
                    difficulty.keeperError = 0.5f;
                    difficulty.keeperShotsToConsider = 3;
                    difficulty.thinkSeconds = 0.6f;
                    difficulty.aimSeconds = 0.9f;
                    break;
                case AiLevel.Hard:
                    difficulty.discsToTest = 3;
                    difficulty.simulations = 12;
                    difficulty.pickAmongBest = 1;
                    difficulty.foulCaution = 1f;
                    difficulty.repositionChance = 0.8f;
                    difficulty.ownGoalDefendChance = 1f;
                    difficulty.angleErrorDegrees = 1.5f;
                    difficulty.powerError = 0.03f;
                    difficulty.shotCallDistance = 6f;
                    difficulty.keeperError = 0.08f;
                    difficulty.keeperShotsToConsider = 10;
                    difficulty.thinkSeconds = 0.3f;
                    difficulty.aimSeconds = 0.6f;
                    break;
            }
            return difficulty;
        }
    }
}
