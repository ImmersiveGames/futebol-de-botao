using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>Um time: nome, abreviação do placar, cores dos botões e escudo.</summary>
    [CreateAssetMenu(menuName = "Futebol de Botão/Time", fileName = "Time")]
    public sealed class TeamData : ScriptableObject
    {
        /// <summary>Qual cor a cor reserva substitui.</summary>
        public enum ReserveSlot { Principal, SegundaCor }

        /// <summary>Cores principais mais perto que isto (soma das diferenças de R, G e B): times parecidos.</summary>
        public const float ClashDistance = 0.35f;

        [SerializeField] private string teamName = "Time";
        [Tooltip("3 letras, usadas no placar.")]
        [SerializeField] private string abbreviation = "TIM";
        [Tooltip("Cor do botão.")]
        [SerializeField] private Color primaryColor = Color.white;
        [Tooltip("Cor do anel do botão (diferencia times de cores parecidas).")]
        [SerializeField] private Color secondaryColor = Color.black;
        [Tooltip("Escudo. Vazio: a tela mostra um círculo nas cores do time.")]
        [SerializeField] private Sprite crest;
        [Header("Cor reserva (cores parecidas com o adversário)")]
        [Tooltip("O time tem cor reserva. Com as cores principais parecidas, o visitante joga com ela (sem reserva no visitante, o mandante).")]
        [SerializeField] private bool hasReserve;
        [SerializeField] private Color reserveColor = Color.white;
        [Tooltip("A cor que a reserva substitui.")]
        [SerializeField] private ReserveSlot reserveReplaces = ReserveSlot.Principal;
        [Header("Camisas (time da IA)")]
        [Tooltip("Padrão dos botões quando a IA joga com este time. Sortear: um diferente a cada partida.")]
        [ShirtPatternId(ShirtPattern.Kind.Botao)]
        [SerializeField] private string discPatternId = "";
        [Tooltip("Padrão do goleiro quando a IA joga com este time. Sortear: um diferente a cada partida.")]
        [ShirtPatternId(ShirtPattern.Kind.Goleiro)]
        [SerializeField] private string keeperPatternId = "";

        public string TeamName => teamName;
        public string Abbreviation => abbreviation;
        public Color PrimaryColor => primaryColor;
        public Color SecondaryColor => secondaryColor;
        public Sprite Crest => crest;
        public bool HasReserve => hasReserve;
        public Color ReserveColor => reserveColor;
        public ReserveSlot ReserveReplaces => reserveReplaces;

        /// <summary>Cor principal com ou sem a reserva.</summary>
        public Color PrimaryFor(bool reserve) => reserve && hasReserve && reserveReplaces == ReserveSlot.Principal ? reserveColor : primaryColor;

        /// <summary>A segunda cor é trocada pela reserva (com <paramref name="reserve"/>).</summary>
        public bool ReserveReplacesSecondary(bool reserve) => reserve && hasReserve && reserveReplaces == ReserveSlot.SegundaCor;

        public void SetReserve(bool has, Color color, ReserveSlot replaces)
        {
            hasReserve = has;
            reserveColor = color;
            reserveReplaces = replaces;
        }

        /// <summary>As cores principais dos dois times são parecidas.</summary>
        public static bool Clash(TeamData a, TeamData b)
        {
            if (a == null || b == null) return false;
            Color x = a.primaryColor, y = b.primaryColor;
            return Mathf.Abs(x.r - y.r) + Mathf.Abs(x.g - y.g) + Mathf.Abs(x.b - y.b) < ClashDistance;
        }

        /// <summary>
        /// O time joga de reserva contra o adversário? Só com as cores principais parecidas: o visitante, se tiver reserva;
        /// senão o mandante, se ele tiver.
        /// </summary>
        public static bool WearsReserve(TeamData team, TeamData opponent, bool visitor)
        {
            if (team == null || !team.hasReserve || !Clash(team, opponent)) return false;
            return visitor || !opponent.hasReserve;
        }
        /// <summary>Id do padrão dos botões do time (vazio: sorteado).</summary>
        public string DiscPatternId => discPatternId;
        /// <summary>Id do padrão do goleiro do time (vazio: sorteado).</summary>
        public string KeeperPatternId => keeperPatternId;

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
