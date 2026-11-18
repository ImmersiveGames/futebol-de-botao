using System.Collections.Generic;
using Immersive.Framework.ActivityFlow;
using Immersive.Framework.GameFlow;
using Immersive.Framework.Pause;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FutebolDeBotao
{
    /// <summary>Pro Gol!: aviso de chute a gol. Parte da <see cref="MatchController"/>.</summary>
    public sealed partial class MatchController
    {
        // ---- Pro Gol! ----

        public bool CanCallShot
        {
            get
            {
                // Sem goleiro não existe "Pro Gol!": entrou, é gol.
                // Disponível quando a bola está no campo de ataque do time da vez; qualquer botão pode chutar.
                return !IsHolding && !TutorialLocked && State == MatchState.Aim && !aim.IsAiming && options.hasGoalkeeper &&
                       InAttackHalf(ball.Body.position, Turn);
            }
        }

        /// <summary>"Pronto": encerra o ajuste do goleiro ou o posicionamento do batedor do pênalti.</summary>
        public void ConfirmReady()
        {
            if (IsHolding || TutorialLocked) return;
            if (State == MatchState.ShotCall) FinishShotCall();
            else if (State == MatchState.PenaltySetup) FinishPenaltySetup();
            else if (IsPlacingGoalKick) FinishGoalKickPlacement();
        }

        /// <summary>Botão de pausa do HUD: o menu fica com o dono do mouse/toque que clicou.</summary>
        public void RequestPause()
        {
            DeviceChoice.PausedBy = DeviceChoice.PointerOwner(Pointer.current);
            if (pauseTrigger != null) pauseTrigger.RequestPause();
        }

        public void TryCallShot()
        {
            if (!CanCallShot) return;
            message = Txt.Get("Pro Gol!");
            BeginShotCall();
        }

        /// <summary>
        /// Chute anunciado (falta no ataque, pênalti ou "Pro Gol!"): o defensor ganha os segundos para ajustar o goleiro.
        /// Sem goleiro vira um chute normal de quem vai bater.
        /// </summary>
        private void BeginShotCall()
        {
            if (!options.hasGoalkeeper)
            {
                Enter(MatchState.Aim);
                return;
            }

            Enter(MatchState.ShotCall);
        }

        /// <summary>
        /// Goleiro pronto: com a bola encostada nele ou atrás dele (área do goleiro), é bola do goleiro, porque o atacante
        /// só chegaria nela fazendo falta no goleiro. Senão, o atacante chuta.
        /// </summary>
        private void FinishShotCall()
        {
            if (HiddenPenalty)
            {
                FinishPenaltyKeeper();
                return;
            }
            if (TryKeeperZoneGoalKick()) return;
            EnterShotAim();
        }

        private void EnterShotAim()
        {
            message = Txt.Get("Chute!");
            Enter(MatchState.ShotAim);
        }

        /// <summary>
        /// Um gol nesta jogada valeria: num chute anunciado ou, sem goleiro, com a bola no campo de ataque do time da vez.
        /// </summary>
        public bool GoalWouldCount => State == MatchState.ShotAim ||
                                      (!options.hasGoalkeeper && InAttackHalf(ball.Body.position, Turn));

        private static bool InAttackHalf(Vector2 position, TeamSide side) =>
            side == TeamSide.Bottom ? position.y > 0f : position.y < 0f;

        /// <summary>Botão do time da vez; no tiro livre, só quem sofreu a falta.</summary>
        public bool CanUse(Disc disc)
        {
            if (disc == null || !disc.isActiveAndEnabled || disc.Side != Turn) return false;
            return freeKickDisc == null || disc == freeKickDisc;
        }
    }
}
