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
    /// Personalidade da IA: o nível diz quão bem ela joga, o perfil mexe só em duas coisas, de quão longe ela anuncia o
    /// Vai chutar e o risco de falta que ela aceita. Assets em Resources/IA/Perfis (Ofensivo, Equilibrado, Defensivo).
    /// </summary>
    [CreateAssetMenu(menuName = "Futebol de Botão/Perfil da IA", fileName = "PerfilDaIA")]
    public sealed class AiProfile : ScriptableObject
    {
        [Tooltip("Somado à distância do gol para anunciar o Vai chutar do nível. Ofensivo 0,5 (de mais longe); Defensivo -0,5.")]
        public float shotCallDistanceOffset = 0f;
        [Tooltip("Somado à chance de falta que o nível aceita. Ofensivo 0,1; Defensivo -0,05.")]
        public float maxFoulChanceOffset = 0f;

        /// <summary>Escreve em <paramref name="into"/> a mistura de <paramref name="a"/> (t = 0) com <paramref name="b"/> (t = 1).</summary>
        public static void Blend(AiProfile a, AiProfile b, float t, AiProfile into)
        {
            into.shotCallDistanceOffset = Mathf.Lerp(a.shotCallDistanceOffset, b.shotCallDistanceOffset, t);
            into.maxFoulChanceOffset = Mathf.Lerp(a.maxFoulChanceOffset, b.maxFoulChanceOffset, t);
        }

        /// <summary>Aplica o perfil aos números do nível: <paramref name="into"/> recebe <paramref name="level"/> ajustado.</summary>
        public void Apply(AiDifficulty level, AiDifficulty into)
        {
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(level), into);
            into.shotCallDistance = Mathf.Max(0f, level.shotCallDistance + shotCallDistanceOffset);
            into.maxFoulChance = Mathf.Clamp01(level.maxFoulChance + maxFoulChanceOffset);
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
                    profile.shotCallDistanceOffset = 0.5f;
                    profile.maxFoulChanceOffset = 0.1f;
                    break;
                case AiStyle.Defensive:
                    profile.shotCallDistanceOffset = -0.5f;
                    profile.maxFoulChanceOffset = -0.05f;
                    break;
            }
            return profile;
        }
    }
}
