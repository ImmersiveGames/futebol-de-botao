using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>Estilos de jogo da IA. Vêm do estilo do esquema tático dela.</summary>
    public enum AiStyle
    {
        Balanced,
        Offensive,
        Defensive
    }

    /// <summary>
    /// Personalidade da IA: o que ela prefere fazer (o nível diz quão bem ela faz). Pesos nos objetivos, vontade de
    /// anunciar o Vai chutar e cuidado com falta. Assets em Resources/IA/Perfis (Ofensivo, Equilibrado, Defensivo).
    /// </summary>
    [CreateAssetMenu(menuName = "Futebol de Botão/Perfil da IA", fileName = "PerfilDaIA")]
    public sealed class AiProfile : ScriptableObject
    {
        [Header("Peso dos objetivos (multiplica a prioridade)")]
        [Tooltip("Tirar do perigo. Ofensivo 0,9; Equilibrado 1; Defensivo 1,15.")]
        [Min(0f)] public float clearDangerWeight = 1f;
        [Tooltip("Preparar o chute. Ofensivo 1,2; Equilibrado 1; Defensivo 0,85.")]
        [Min(0f)] public float prepareShotWeight = 1f;
        [Tooltip("Fechar o gol. Ofensivo 0,85; Equilibrado 1; Defensivo 1,25.")]
        [Min(0f)] public float closeGoalWeight = 1f;
        [Tooltip("Avançar. Ofensivo 1,1; Equilibrado 1; Defensivo 0,9.")]
        [Min(0f)] public float advanceWeight = 1f;

        [Header("Vai chutar")]
        [Tooltip("Somado ao valor do chute ao comparar com a outra jogada. Positivo chuta mais. Ofensivo 12; Defensivo -12.")]
        public float shotValueBonus = 0f;
        [Tooltip("Somado à distância do Vai chutar do nível. Ofensivo 0,5; Defensivo -0,5.")]
        public float shotCallDistanceOffset = 0f;
        [Tooltip("Somado à chance mínima do Vai chutar do nível. Ofensivo -0,03; Defensivo 0,05.")]
        public float callShotChanceOffset = 0f;

        [Header("Riscos")]
        [Tooltip("Somado à chance de falta que o nível aceita. Ofensivo 0,1; Defensivo -0,05.")]
        public float maxFoulChanceOffset = 0f;
        [Tooltip("Multiplica o cuidado com falta do nível. Ofensivo 0,8; Defensivo 1,2.")]
        [Min(0f)] public float foulCautionScale = 1f;
        [Tooltip("Somado à chance de olhar o perigo do nível. Ofensivo -0,1; Defensivo 0,15.")]
        public float dangerAwarenessOffset = 0f;

        /// <summary>Peso do objetivo neste perfil. Marcar e Toque seguro não mudam (o chute pesa pelo Vai chutar).</summary>
        public float Weight(AiGoal goal) => goal switch
        {
            ClearDangerGoal => clearDangerWeight,
            PrepareShotGoal => prepareShotWeight,
            CloseGoalGoal => closeGoalWeight,
            AdvanceGoal => advanceWeight,
            _ => 1f
        };

        /// <summary>Escreve em <paramref name="into"/> a mistura de <paramref name="a"/> (t = 0) com <paramref name="b"/> (t = 1).</summary>
        public static void Blend(AiProfile a, AiProfile b, float t, AiProfile into)
        {
            into.clearDangerWeight = Mathf.Lerp(a.clearDangerWeight, b.clearDangerWeight, t);
            into.prepareShotWeight = Mathf.Lerp(a.prepareShotWeight, b.prepareShotWeight, t);
            into.closeGoalWeight = Mathf.Lerp(a.closeGoalWeight, b.closeGoalWeight, t);
            into.advanceWeight = Mathf.Lerp(a.advanceWeight, b.advanceWeight, t);
            into.shotValueBonus = Mathf.Lerp(a.shotValueBonus, b.shotValueBonus, t);
            into.shotCallDistanceOffset = Mathf.Lerp(a.shotCallDistanceOffset, b.shotCallDistanceOffset, t);
            into.callShotChanceOffset = Mathf.Lerp(a.callShotChanceOffset, b.callShotChanceOffset, t);
            into.maxFoulChanceOffset = Mathf.Lerp(a.maxFoulChanceOffset, b.maxFoulChanceOffset, t);
            into.foulCautionScale = Mathf.Lerp(a.foulCautionScale, b.foulCautionScale, t);
            into.dangerAwarenessOffset = Mathf.Lerp(a.dangerAwarenessOffset, b.dangerAwarenessOffset, t);
        }

        /// <summary>Aplica o perfil aos números do nível: <paramref name="into"/> recebe <paramref name="level"/> ajustado.</summary>
        public void Apply(AiDifficulty level, AiDifficulty into)
        {
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(level), into);
            into.shotCallDistance = Mathf.Max(0f, level.shotCallDistance + shotCallDistanceOffset);
            into.callShotChance = Mathf.Clamp01(level.callShotChance + callShotChanceOffset);
            into.maxFoulChance = Mathf.Clamp01(level.maxFoulChance + maxFoulChanceOffset);
            into.foulCaution = Mathf.Clamp(level.foulCaution * foulCautionScale, 0f, 2f);
            into.dangerAwareness = Mathf.Clamp01(level.dangerAwareness + dangerAwarenessOffset);
        }

        /// <summary>Estilo pelo texto do esquema ("Ofensivo", "Defensivo"; o resto, como Losango, é Equilibrado).</summary>
        public static AiStyle StyleOf(Formation scheme)
        {
            string style = scheme != null ? scheme.Style : string.Empty;
            if (style.StartsWith("Ofens")) return AiStyle.Offensive;
            if (style.StartsWith("Defens")) return AiStyle.Defensive;
            return AiStyle.Balanced;
        }

        /// <summary>Asset do perfil em Resources/IA/Perfis. Sem o asset, usa os números do plano direto do código.</summary>
        public static AiProfile Load(AiStyle style)
        {
            int index = (int)style;
            if (Loaded[index] == null)
            {
                var asset = Resources.Load<AiProfile>($"IA/Perfis/{FileName(style)}");
                Loaded[index] = asset != null ? asset : Create(style);
            }
            return Loaded[index];
        }

        private static readonly AiProfile[] Loaded = new AiProfile[3];

        public static string FileName(AiStyle style) => style switch
        {
            AiStyle.Offensive => "Ofensivo",
            AiStyle.Defensive => "Defensivo",
            _ => "Equilibrado"
        };

        /// <summary>Números do plano para cada estilo (os mesmos dos assets).</summary>
        public static AiProfile Create(AiStyle style)
        {
            var profile = CreateInstance<AiProfile>();
            profile.name = $"{FileName(style)} (padrão)";
            profile.hideFlags = HideFlags.DontSave;
            switch (style)
            {
                case AiStyle.Offensive:
                    profile.clearDangerWeight = 0.9f;
                    profile.prepareShotWeight = 1.2f;
                    profile.closeGoalWeight = 0.85f;
                    profile.advanceWeight = 1.1f;
                    profile.shotValueBonus = 12f;
                    profile.shotCallDistanceOffset = 0.5f;
                    profile.callShotChanceOffset = -0.03f;
                    profile.maxFoulChanceOffset = 0.1f;
                    profile.foulCautionScale = 0.8f;
                    profile.dangerAwarenessOffset = -0.1f;
                    break;
                case AiStyle.Defensive:
                    profile.clearDangerWeight = 1.15f;
                    profile.prepareShotWeight = 0.85f;
                    profile.closeGoalWeight = 1.25f;
                    profile.advanceWeight = 0.9f;
                    profile.shotValueBonus = -12f;
                    profile.shotCallDistanceOffset = -0.5f;
                    profile.callShotChanceOffset = 0.05f;
                    profile.maxFoulChanceOffset = -0.05f;
                    profile.foulCautionScale = 1.2f;
                    profile.dangerAwarenessOffset = 0.15f;
                    break;
            }
            return profile;
        }
    }
}
