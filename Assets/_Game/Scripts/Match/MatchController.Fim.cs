using System.Collections.Generic;
using Immersive.Framework.ActivityFlow;
using Immersive.Framework.GameFlow;
using Immersive.Framework.Pause;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FutebolDeBotao
{
    /// <summary>Fim da partida: resultado, estatísticas, moedas e torneio. Parte da <see cref="MatchController"/>.</summary>
    public sealed partial class MatchController
    {
        // ---- Fim ----

        /// <summary>Menu de teste: zera o relógio (a partida acaba no próximo lance, como no fim do tempo).</summary>
        public void DebugEndTime()
        {
            if (IsShootout || State is MatchState.Waiting or MatchState.End) return;
            clock = 0f;
        }

        /// <summary>Menu de teste: W.O. para o jogador de baixo (+3 gols, a IA fica com 0) e a partida acaba na hora.</summary>
        public void DebugWalkover()
        {
            if (State is MatchState.Waiting or MatchState.End) return;
            Walkover(TeamSide.Top);
        }

        /// <summary>Menu de teste: iguala o placar (o time com menos gols fica com os do outro).</summary>
        public void DebugTieScore()
        {
            int most = Mathf.Max(score[0], score[1]);
            score[0] = score[1] = most;
        }

        private void FinishMatch()
        {
            int bottom = Score(TeamSide.Bottom);
            int top = Score(TeamSide.Top);
            message = Txt.Get("Fim de jogo!");
            MatchSession.LastResult = new MatchResult(bottom, top);
            RecordStats(walkover: false);
            Noticed?.Invoke(MatchNotice.FinalWhistle);
            Enter(MatchState.End);
        }

        /// <summary>O jogador (time de baixo) ficou atrás no placar em algum momento (conquista Virada).</summary>
        private bool playerTrailed;

        /// <summary>Fim da partida: os números dos dois times vão para o pós-jogo; os do jogador (baixo), para o save.</summary>
        private void RecordStats(bool walkover)
        {
            MatchSession.LastMatchNumbers = new[] { numbers[0], numbers[1] };
            int bottomPenalties = IsShootout ? ShootoutGoals(TeamSide.Bottom) : -1;
            int topPenalties = IsShootout ? ShootoutGoals(TeamSide.Top) : -1;
            bool counted = PlayerStats.Finish(numbers[(int)TeamSide.Bottom], Score(TeamSide.Bottom), Score(TeamSide.Top), walkover,
                bottomPenalties, topPenalties);
            if (counted)
                Achievements.MatchFinished(Score(TeamSide.Bottom), Score(TeamSide.Top), walkover,
                    IsShootout && Score(TeamSide.Bottom) == Score(TeamSide.Top) && bottomPenalties > topPenalties,
                    period == Period.ExtraTime && Score(TeamSide.Bottom) > Score(TeamSide.Top), playerTrailed);
            if (analyticsMatchOpen)
                GameAnalytics.MatchEnd(Score(TeamSide.Bottom), Score(TeamSide.Top), walkover, bottomPenalties, topPenalties,
                    period != Period.Regular, Time.realtimeSinceStartup - analyticsStartTime,
                    numbers[(int)TeamSide.Bottom], numbers[(int)TeamSide.Top]);
            analyticsMatchOpen = false;
            if (MatchSession.InTournamentMatch)
                Tournament.RecordPlayerMatch(Score(TeamSide.Bottom), Score(TeamSide.Top), bottomPenalties, topPenalties, walkover,
                    PlayerStats.CoinsEarned);
        }

        /// <summary>Analytics do abandono: parte da partida, minuto jogado nela e placar.</summary>
        private void LogAbandon()
        {
            float total = period == Period.ExtraTime ? options.ExtraTimeSeconds : options.DurationSeconds;
            int minute = period == Period.Shootout ? 0 : Mathf.FloorToInt(Mathf.Max(0f, total - clock) / 60f);
            string part = period == Period.ExtraTime ? "extra_time" : period == Period.Shootout ? "shootout" : "regular";
            GameAnalytics.MatchAbandon(part, minute, Score(TeamSide.Bottom), Score(TeamSide.Top));
        }

        private void RequestResult()
        {
            resultRequested = true;
            MatchPlayers.ReleaseBeforeLeaving("fim de jogo");
            if (resultRoute != null) resultRoute.RequestRoute();
            else Debug.LogWarning("[Futebol de Botão] MatchController sem Route Request Trigger do Resultado. Rode \"Futebol de Botão/Criar telas\".");
        }
    }
}
