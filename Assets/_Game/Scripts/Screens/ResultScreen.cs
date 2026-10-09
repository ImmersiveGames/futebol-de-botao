using Immersive.Framework.GameFlow;
using UnityEngine;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>Resultado: placar final e vencedor; "Jogar de novo" volta para a Partida e "Menu" para o Menu.</summary>
    public sealed class ResultScreen : MonoBehaviour
    {
        [SerializeField] private Text scoreLabel;
        [SerializeField] private Text verdictLabel;
        [SerializeField] private RouteRequestTrigger matchRoute;
        [SerializeField] private RouteRequestTrigger menuRoute;

        public void Configure(Text score, Text verdict, RouteRequestTrigger match, RouteRequestTrigger menu)
        {
            scoreLabel = score;
            verdictLabel = verdict;
            matchRoute = match;
            menuRoute = menu;
        }

        private void Start()
        {
            var result = MatchSession.LastResult;
            if (result == null)
            {
                if (scoreLabel != null) scoreLabel.text = "Sem partida";
                if (verdictLabel != null) verdictLabel.text = string.Empty;
                return;
            }

            int bottom = result.Value.Bottom;
            int top = result.Value.Top;
            if (scoreLabel != null)
                scoreLabel.text = $"{MatchController.TeamName(TeamSide.Bottom)} {bottom} x {top} {MatchController.TeamName(TeamSide.Top)}";
            if (verdictLabel != null)
                verdictLabel.text = bottom == top
                    ? "Empate!"
                    : $"Vitória do {MatchController.TeamName(bottom > top ? TeamSide.Bottom : TeamSide.Top)}" +
                      $"{(result.Value.Walkover ? " por W.O." : string.Empty)}!";
        }

        public void PlayAgain()
        {
            if (matchRoute != null) matchRoute.RequestRoute();
        }

        public void BackToMenu()
        {
            if (menuRoute != null) menuRoute.RequestRoute();
        }
    }
}
