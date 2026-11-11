using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Um uniforme do time: cor principal (corpo do botão), segunda cor (padrão da camisa e anel) e os padrões de camisa
    /// do botão e do goleiro (vazio: sorteado quando a IA joga).
    /// </summary>
    [System.Serializable]
    public sealed class TeamUniform
    {
        public Color primary = Color.white;
        public Color secondary = Color.black;
        [ShirtPatternId(ShirtPattern.Kind.Botao)] public string discPatternId = "";
        [ShirtPatternId(ShirtPattern.Kind.Goleiro)] public string keeperPatternId = "";

        public TeamUniform() { }

        public TeamUniform(Color primary, Color secondary, string discPatternId, string keeperPatternId)
        {
            this.primary = primary;
            this.secondary = secondary;
            this.discPatternId = discPatternId ?? "";
            this.keeperPatternId = keeperPatternId ?? "";
        }
    }

    /// <summary>
    /// Um time: nome, abreviação do placar, escudo e dois uniformes. O Uniforme 1 é o de sempre; o 2 entra quando as
    /// cores ficam parecidas com as do adversário (a IA escolhe sozinha; o jogador escolhe na Seleção de times).
    /// </summary>
    [CreateAssetMenu(menuName = "Futebol de Botão/Time", fileName = "Time")]
    public sealed class TeamData : ScriptableObject
    {
        /// <summary>Cores principais mais perto que isto (soma das diferenças de R, G e B): times parecidos.</summary>
        public const float ClashDistance = 0.35f;

        /// <summary>Quantos uniformes cada time tem.</summary>
        public const int UniformCount = 2;

        [SerializeField] private string teamName = "Time";
        [Tooltip("3 letras, usadas no placar.")]
        [SerializeField] private string abbreviation = "TIM";
        [Tooltip("Escudo. Vazio: a tela mostra um círculo nas cores do time.")]
        [SerializeField] private Sprite crest;

        // Uniforme 1: os campos antigos (os nomes ficam para os assets não perderem as cores).
        [Header("Uniforme 1")]
        [Tooltip("Cor do botão.")]
        [SerializeField] private Color primaryColor = Color.white;
        [Tooltip("Cor do padrão da camisa e do anel.")]
        [SerializeField] private Color secondaryColor = Color.black;
        [Tooltip("Padrão dos botões. Sortear: quando a IA joga, um diferente a cada partida.")]
        [ShirtPatternId(ShirtPattern.Kind.Botao)]
        [SerializeField] private string discPatternId = "";
        [Tooltip("Padrão do goleiro. Sortear: quando a IA joga, um diferente a cada partida.")]
        [ShirtPatternId(ShirtPattern.Kind.Goleiro)]
        [SerializeField] private string keeperPatternId = "";

        [Header("Uniforme 2")]
        [Tooltip("Usado quando as cores ficam parecidas com as do adversário.")]
        [SerializeField] private TeamUniform uniform2 = new();
        [Tooltip("O Uniforme 2 já foi preenchido (assets antigos são convertidos da cor reserva ou com as cores invertidas).")]
        [SerializeField, HideInInspector] private bool uniform2Set;

        // Cor reserva (versão antiga): só para converter os assets no Uniforme 2.
        [SerializeField, HideInInspector] private bool hasReserve;
        [SerializeField, HideInInspector] private Color reserveColor = Color.white;
        [SerializeField, HideInInspector] private int reserveReplaces;

        public string TeamName => teamName;
        public string Abbreviation => abbreviation;
        /// <summary>Cores do Uniforme 1.</summary>
        public Color PrimaryColor => primaryColor;
        public Color SecondaryColor => secondaryColor;
        public Sprite Crest => crest;
        /// <summary>Padrões do Uniforme 1.</summary>
        public string DiscPatternId => discPatternId;
        public string KeeperPatternId => keeperPatternId;

        /// <summary>Uniforme 1 (0) ou 2 (1).</summary>
        public TeamUniform Uniform(int index)
        {
            if (index <= 0) return new TeamUniform(primaryColor, secondaryColor, discPatternId, keeperPatternId);
            EnsureUniform2();
            return uniform2;
        }

        public Color PrimaryOf(int uniform) => uniform <= 0 ? primaryColor : Uniform(1).primary;

        public Color SecondaryOf(int uniform) => uniform <= 0 ? secondaryColor : Uniform(1).secondary;

        public string DiscPatternOf(int uniform) => uniform <= 0 ? discPatternId : Uniform(1).discPatternId;

        public string KeeperPatternOf(int uniform) => uniform <= 0 ? keeperPatternId : Uniform(1).keeperPatternId;

        /// <summary>
        /// Asset antigo (sem Uniforme 2): a cor reserva vira o Uniforme 2; sem reserva, as cores invertidas. As camisas
        /// são as do Uniforme 1. Devolve true se mudou (o editor grava o asset).
        /// </summary>
        public bool EnsureUniform2()
        {
            if (uniform2Set) return false;
            uniform2Set = true;
            if (hasReserve)
                uniform2 = reserveReplaces == 1
                    ? new TeamUniform(primaryColor, reserveColor, discPatternId, keeperPatternId)
                    : new TeamUniform(reserveColor, secondaryColor, discPatternId, keeperPatternId);
            else uniform2 = new TeamUniform(secondaryColor, primaryColor, discPatternId, keeperPatternId);
            hasReserve = false;
            return true;
        }

        /// <summary>Cores parecidas (soma das diferenças de R, G e B menor que <see cref="ClashDistance"/>).</summary>
        public static bool Clash(Color x, Color y) =>
            Mathf.Abs(x.r - y.r) + Mathf.Abs(x.g - y.g) + Mathf.Abs(x.b - y.b) < ClashDistance;

        /// <summary>
        /// Uniforme que o time (da IA) veste contra um adversário de cor principal <paramref name="opponentPrimary"/>:
        /// o 1, a não ser que fique parecido e o 2 não.
        /// </summary>
        public int AutoUniform(Color opponentPrimary) =>
            Clash(PrimaryOf(0), opponentPrimary) && !Clash(PrimaryOf(1), opponentPrimary) ? 1 : 0;

        /// <summary>Uniforme 2 completo (Meu time).</summary>
        public void SetUniform2(TeamUniform uniform)
        {
            uniform2 = uniform ?? new TeamUniform();
            uniform2Set = true;
        }

        public void SetShirts(string discPattern, string keeperPattern)
        {
            discPatternId = discPattern ?? "";
            keeperPatternId = keeperPattern ?? "";
        }

        /// <summary>Cor para escrever o nome do time sobre fundo escuro (time preto usa a cor do anel).</summary>
        public Color TextColor
        {
            get
            {
                Color.RGBToHSV(primaryColor, out _, out _, out float value);
                return value < 0.35f ? secondaryColor : primaryColor;
            }
        }

        public void Set(string newName, string newAbbreviation, Color primary, Color secondary)
        {
            teamName = newName;
            abbreviation = newAbbreviation;
            primaryColor = primary;
            secondaryColor = secondary;
        }

        public static TeamData Create(string newName, string newAbbreviation, Color primary, Color secondary)
        {
            var team = CreateInstance<TeamData>();
            team.name = newName;
            team.Set(newName, newAbbreviation, primary, secondary);
            return team;
        }

        private void OnValidate()
        {
            if (abbreviation != null && abbreviation.Length > 3) abbreviation = abbreviation.Substring(0, 3);
            if (abbreviation != null) abbreviation = abbreviation.ToUpperInvariant();
        }
    }
}
