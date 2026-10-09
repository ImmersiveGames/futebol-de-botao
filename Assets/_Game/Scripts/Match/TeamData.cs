using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>Um time: nome, abreviação do placar, cores dos botões e escudo.</summary>
    [CreateAssetMenu(menuName = "Futebol de Botão/Time", fileName = "Time")]
    public sealed class TeamData : ScriptableObject
    {
        [SerializeField] private string teamName = "Time";
        [Tooltip("3 letras, usadas no placar.")]
        [SerializeField] private string abbreviation = "TIM";
        [Tooltip("Cor do botão.")]
        [SerializeField] private Color primaryColor = Color.white;
        [Tooltip("Cor do anel do botão (diferencia times de cores parecidas).")]
        [SerializeField] private Color secondaryColor = Color.black;
        [Tooltip("Escudo. Vazio: a tela mostra um círculo nas cores do time.")]
        [SerializeField] private Sprite crest;

        public string TeamName => teamName;
        public string Abbreviation => abbreviation;
        public Color PrimaryColor => primaryColor;
        public Color SecondaryColor => secondaryColor;
        public Sprite Crest => crest;

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
