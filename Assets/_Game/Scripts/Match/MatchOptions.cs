using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>Opções da partida do GDD. A tela de Opções vai editar este asset na fase 2.</summary>
    [CreateAssetMenu(menuName = "Futebol de Botão/Opções da partida", fileName = "OpcoesDaPartida")]
    public sealed class MatchOptions : ScriptableObject
    {
        private static readonly int[] Durations = { 3, 5, 8, 10 };

        [Header("Partida")]
        [Tooltip("Tempo único: 3, 5, 8 ou 10 minutos.")]
        public int durationMinutes = 5;
        [Tooltip("Toques por vez, de 1 a 3.")]
        [Range(1, 3)] public int touchesPerTurn = 3;
        public bool goalAfterWallIsValid;

        [Header("Times")]
        [Tooltip("5 ou 3 botões por time.")]
        public int discsPerTeam = 5;
        public bool hasGoalkeeper = true;
        public Formation formation5;
        public Formation formation3;

        [Header("Mira")]
        public AimAssistMode aimAssist = AimAssistMode.Short;
        [Min(1f)] public float aimTimeSeconds = 30f;
        [Tooltip("Tempo que o defensor tem para ajustar o goleiro depois do \"Vai chutar\".")]
        [Min(1f)] public float shotCallSeconds = 30f;

        [Header("Pênalti")]
        [Tooltip("Tempo para o atacante posicionar o batedor em volta da bola (pênalti e tiro livre).")]
        [Min(1f)] public float penaltySetupSeconds = 30f;
        [Tooltip("Distância entre a borda do batedor e a borda da bola no pênalti e no tiro livre.")]
        [Min(0.05f)] public float penaltyDiscGap = 0.6f;
        [Tooltip("Abertura do arco atrás da bola onde o botão pode ficar, em graus para cada lado.")]
        [Range(0f, 90f)] public float penaltyArcDegrees = 70f;

        public float DurationSeconds => durationMinutes * 60f;

        /// <summary>Posição inicial do botão <paramref name="index"/> do time <paramref name="side"/>.</summary>
        public Vector2 FormationPosition(int index, TeamSide side)
        {
            var formation = discsPerTeam == 3 ? formation3 : formation5;
            if (formation != null && index < formation.Count) return formation.PositionFor(index, side);
            var fallback = discsPerTeam == 3 ? Formation.Default3 : Formation.Default5;
            return Formation.Mirror(fallback[Mathf.Min(index, fallback.Length - 1)], side);
        }

        public static MatchOptions CreateDefault()
        {
            var options = CreateInstance<MatchOptions>();
            options.name = "Opções padrão";
            return options;
        }

        private void OnValidate()
        {
            discsPerTeam = discsPerTeam <= 4 ? 3 : 5;

            int closest = Durations[0];
            foreach (int d in Durations)
                if (Mathf.Abs(d - durationMinutes) < Mathf.Abs(closest - durationMinutes)) closest = d;
            durationMinutes = closest;
        }
    }
}
