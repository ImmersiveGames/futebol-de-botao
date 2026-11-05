using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Cores que o jogador escolhe para o Meu time (principal e segunda cor), na ordem das setas. Fica em
    /// Resources/CoresDoTime: é só acrescentar, tirar ou mudar as cores ali. O save guarda a cor em si (não a posição na
    /// lista), então mexer na lista não troca a cor de quem já escolheu.
    /// </summary>
    [CreateAssetMenu(menuName = "Futebol de Botão/Cores do time", fileName = "CoresDoTime")]
    public sealed class TeamColorPalette : ScriptableObject
    {
        public const string ResourcePath = "CoresDoTime";

        [System.Serializable]
        public sealed class Entry
        {
            [Tooltip("Nome que o jogador vê.")]
            public string name = "Cor";
            public Color color = Color.white;
        }

        public Entry[] colors = { };

        private static TeamColorPalette cached;

        public int Count => colors.Length;

        public Entry this[int index] => colors[((index % colors.Length) + colors.Length) % colors.Length];

        /// <summary>Posição da cor na lista (a mais parecida, se não houver igual); -1 se ela não estiver na lista.</summary>
        public int IndexOf(Color color)
        {
            int best = -1;
            float bestDistance = 0.02f;
            for (int i = 0; i < colors.Length; i++)
            {
                if (colors[i] == null) continue;
                var c = colors[i].color;
                float distance = Mathf.Abs(c.r - color.r) + Mathf.Abs(c.g - color.g) + Mathf.Abs(c.b - color.b);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>Nome da cor na lista, ou vazio se ela não estiver mais lá.</summary>
        public string NameOf(Color color)
        {
            int index = IndexOf(color);
            return index >= 0 ? colors[index].name : string.Empty;
        }

        /// <summary>A cor <paramref name="step"/> casas depois de <paramref name="current"/> (dá a volta).</summary>
        public Color Step(Color current, int step)
        {
            if (colors.Length == 0) return current;
            int index = IndexOf(current);
            if (index < 0) return this[step > 0 ? 0 : -1].color;
            return this[index + step].color;
        }

        public static TeamColorPalette Load()
        {
            if (cached != null) return cached;
            cached = Resources.Load<TeamColorPalette>(ResourcePath);
            if (cached == null || cached.colors == null || cached.colors.Length == 0)
            {
                if (cached != null) Debug.LogWarning("[Futebol de Botão] Cores do time vazias; usando as do código.");
                cached = CreateInstance<TeamColorPalette>();
                cached.name = "Cores padrão";
                cached.hideFlags = HideFlags.DontSave;
                cached.colors = new[]
                {
                    new Entry { name = "Azul", color = PlayerProfile.DefaultPrimary },
                    new Entry { name = "Branco", color = Color.white },
                    new Entry { name = "Vermelho", color = new Color(0.85f, 0.17f, 0.17f) },
                    new Entry { name = "Verde", color = new Color(0.12f, 0.62f, 0.24f) },
                    new Entry { name = "Amarelo", color = new Color(0.96f, 0.82f, 0.13f) },
                    new Entry { name = "Preto", color = new Color(0.12f, 0.12f, 0.12f) },
                };
            }
            return cached;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => cached = null;

#if UNITY_EDITOR
        private void OnValidate() => cached = null;
#endif
    }
}
