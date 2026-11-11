using System;
using System.Collections;
using Immersive.Framework.ActivityRestart;
using Immersive.Framework.GameFlow;
using Immersive.Framework.Pause;
using UnityEngine;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>
    /// Botões da tela de pausa. Quem mostra e esconde a tela é o UnityPauseSurfaceAdapter do framework;
    /// este componente fica fora do painel escondido para as corrotinas não pararem.
    /// Reiniciar e Menu primeiro tiram a pausa e só depois fazem o pedido, para o jogo não voltar congelado.
    /// O botão Configurações abre a tela de volumes por cima da pausa (só quem pausou navega).
    /// Reiniciar e Menu contam a partida como abandonada nas estatísticas. No torneio, Reiniciar some e sair abandona o torneio.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        private const float ResumeTimeoutSeconds = 1f;

        [SerializeField] private PauseRequestTrigger pauseTrigger;
        [SerializeField] private ActivityRestartTrigger restartTrigger;
        [SerializeField] private RouteRequestTrigger menuRoute;

        private bool busy;

        public void Configure(PauseRequestTrigger pause, ActivityRestartTrigger restart, RouteRequestTrigger menu)
        {
            pauseTrigger = pause;
            restartTrigger = restart;
            menuRoute = menu;
        }

        private void Start()
        {
            // Torneio: não dá para reiniciar a partida, e sair dela abandona o torneio.
            if (!MatchSession.InTournamentMatch) return;
            var restart = transform.Find("Painel/Reiniciar");
            if (restart != null) restart.gameObject.SetActive(false);
            var menu = transform.Find("Painel/Menu");
            var label = menu != null ? menu.GetComponentInChildren<Text>(true) : null;
            if (label != null) label.text = Txt.Get("Abandonar torneio");
        }

        public void OpenSettings() => SettingsScreen.Open(true);

        public void Resume()
        {
            if (pauseTrigger != null) pauseTrigger.RequestResume();
        }

        public void Restart()
        {
            if (restartTrigger != null) RunAfterResume(() =>
            {
                PlayerStats.Abandon();
                MatchPlayers.ReleaseBeforeLeaving("reiniciar");
                restartTrigger.RequestActivityRestart();
            });
        }

        public void BackToMenu()
        {
            if (menuRoute == null) return;
            void Leave()
            {
                PlayerStats.Abandon();
                MatchPlayers.ReleaseBeforeLeaving("sair para o menu");
                menuRoute.RequestRoute();
            }
            // Sem pausa (Menu na tela de lição concluída do tutorial) vai direto.
            if (busy) return;
            if (Time.timeScale > 0f)
            {
                busy = true;
                Leave();
            }
            else RunAfterResume(Leave);
        }

        private void RunAfterResume(Action action)
        {
            if (busy) return;
            StartCoroutine(AfterResume(action));
        }

        private IEnumerator AfterResume(Action action)
        {
            busy = true;
            Resume();
            float deadline = Time.unscaledTime + ResumeTimeoutSeconds;
            while (Time.timeScale <= 0f && Time.unscaledTime < deadline) yield return null;
            busy = false;
            action();
        }
    }
}
