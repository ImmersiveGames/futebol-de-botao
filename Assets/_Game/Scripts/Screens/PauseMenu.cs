using System;
using System.Collections;
using Immersive.Framework.ActivityRestart;
using Immersive.Framework.GameFlow;
using Immersive.Framework.Pause;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Botões da tela de pausa. Quem mostra e esconde a tela é o UnityPauseSurfaceAdapter do framework;
    /// este componente fica fora do painel escondido para as corrotinas não pararem.
    /// Reiniciar e Menu primeiro tiram a pausa e só depois fazem o pedido, para o jogo não voltar congelado.
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

        public void Resume()
        {
            if (pauseTrigger != null) pauseTrigger.RequestResume();
        }

        public void Restart()
        {
            if (restartTrigger != null) RunAfterResume(restartTrigger.RequestActivityRestart);
        }

        public void BackToMenu()
        {
            if (menuRoute != null) RunAfterResume(menuRoute.RequestRoute);
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
