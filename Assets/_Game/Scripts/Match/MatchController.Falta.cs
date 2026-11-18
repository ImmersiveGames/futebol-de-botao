using System.Collections.Generic;
using Immersive.Framework.ActivityFlow;
using Immersive.Framework.GameFlow;
using Immersive.Framework.Pause;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FutebolDeBotao
{
    /// <summary>Falta: quem fez, cartões e W.O. Parte da <see cref="MatchController"/>.</summary>
    public sealed partial class MatchController
    {
        // ---- Falta ----

        private void OnFouled(Disc offender, Disc victim, Vector2 victimPosition)
        {
            if (State != MatchState.Moving || offender != shooter || !options.fouls || IsShootout) return;
            CommitFoul(offender, victim, victimPosition);
        }

        /// <summary>
        /// Companheiro empurrado pelo botão do peteleco acertou um adversário antes de alguém tocar a bola: também é
        /// falta (não vale usar um botão para empurrar o outro em cima do adversário).
        /// </summary>
        private void OnHitOpponent(Disc disc, Disc victim, Vector2 victimPosition)
        {
            if (State != MatchState.Moving || shooter == null || !options.fouls || IsShootout) return;
            if (disc == shooter || disc.Side != shooter.Side || ball.LastTouchSide != null) return;
            CommitFoul(disc, victim, victimPosition);
        }

        /// <summary>
        /// Botão do peteleco (ou companheiro empurrado por ele) acertou o goleiro adversário antes de alguém tocar a
        /// bola: falta no goleiro, tiro de meta para o time dele.
        /// </summary>
        private void OnHitOpponentKeeper(Disc disc, Goalkeeper keeper, bool asShooter)
        {
            if (State != MatchState.Moving || shooter == null || !options.fouls || IsShootout) return;
            if (asShooter ? disc != shooter : disc == shooter || disc.Side != shooter.Side || ball.LastTouchSide != null) return;

#if UNITY_EDITOR
            Debug.Log($"[Falta] {disc.name} ({TeamName(disc.Side)}) acertou o goleiro do {TeamName(keeper.Side)}.");
#endif
            Count(disc.Side).fouls++;
            EndShot();
            monitor.StopWatching();
            monitor.FreezeAll();
            if (tutorial != null)
            {
                tutorial.OnFoul(disc.Side);
                return;
            }

            bool far = Vector2.Distance(keeper.transform.position, shotBallStart) > options.farFoulDistance;
            SetupGoalKick(keeper.Side);
            message = Txt.Get("Falta no goleiro!");
            Noticed?.Invoke(MatchNotice.Foul);
            if (far) GiveCard(disc);
            Hold();
        }

        private void CommitFoul(Disc offender, Disc victim, Vector2 victimPosition)
        {
            // Para investigar falta marcada errado: mostra o que o jogo sabia no momento (só no editor).
#if UNITY_EDITOR
            Debug.Log($"[Falta] {offender.name} ({TeamName(offender.Side)}) acertou {victim.name}. " +
                      $"Último toque na bola neste peteleco: {(ball.LastDiscTouch != null ? ball.LastDiscTouch.name : "ninguém")}.");
#endif

            Count(offender.Side).fouls++;
            EndShot();
            monitor.StopWatching();
            monitor.FreezeAll();
            if (tutorial != null)
            {
                tutorial.OnFoul(offender.Side);
                return;
            }

            // Falta longe do lance (para afastar a bola, por exemplo) vale cartão para o jogador, não para o botão.
            bool far = Vector2.Distance(victimPosition, shotBallStart) > options.farFoulDistance;

            if (FieldLayout.InArea(victimPosition, offender.Side)) SetupPenalty(offender, victim);
            else SetupFreeKick(offender, victim, victimPosition);

            Noticed?.Invoke(setupIsPenalty ? MatchNotice.Penalty : MatchNotice.Foul);
            if (far) GiveCard(offender);
            Hold();
        }

        /// <summary>1ª falta longe do lance: amarelo. Depois, cada uma é vermelho e o botão que fez a falta sai.</summary>
        private void GiveCard(Disc offender)
        {
            var side = offender.Side;
            int count = ++farFouls[(int)side];
            if (count == 1) Count(side).yellow++;
            else Count(side).red++;
            if (count == 1)
            {
                Noticed?.Invoke(MatchNotice.YellowCard);
                message += " " + Txt.Get("Cartão amarelo!");
                return;
            }

            int onField = 0;
            foreach (var disc in discs[side])
                if (disc.isActiveAndEnabled) onField++;
            offender.gameObject.SetActive(false);
            Noticed?.Invoke(MatchNotice.RedCard);
            if (onField > 1)
            {
                message += " " + Txt.Get("Cartão vermelho!");
                return;
            }
            Walkover(side);
        }

        /// <summary>W.O.: o time ficou sem botões. Quem foi expulso fica com 0 e o adversário ganha 3 gols a mais.</summary>
        private void Walkover(TeamSide loser)
        {
            var winner = loser.Opponent();
            score[(int)loser] = 0;
            score[(int)winner] += 3;
            EndShot();
            monitor.StopWatching();
            monitor.FreezeAll();
            message = "W.O.!";
            Noticed?.Invoke(MatchNotice.FinalWhistle);
            MatchSession.LastResult = new MatchResult(Score(TeamSide.Bottom), Score(TeamSide.Top), true);
            RecordStats(walkover: true);
            Enter(MatchState.End);
        }
    }
}
