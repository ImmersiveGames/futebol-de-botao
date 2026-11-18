using System.Collections.Generic;
using Immersive.Framework.ActivityFlow;
using Immersive.Framework.GameFlow;
using Immersive.Framework.Pause;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FutebolDeBotao
{
    /// <summary>Movimento: peteleco, bola e botões andando, fim da jogada e troca de vez. Parte da <see cref="MatchController"/>.</summary>
    public sealed partial class MatchController
    {
        // ---- Movimento ----

        private void OnBallKicked(Vector2 impulse)
        {
            if (State != MatchState.GoalKick) return;
            ball.MarkTouchedBy(Turn);
            Count(Turn).touches++;
            shotWasCalled = false;
            shotFromAttackHalf = InAttackHalf(ball.Body.position, Turn);
            shooter = null;
            touchesLeft--;
            message = string.Empty;
            Enter(MatchState.Moving);
        }

        private void OnFlicked(Disc disc, Vector2 impulse)
        {
            if (State != MatchState.Aim && State != MatchState.ShotAim) return;
            shotWasCalled = State == MatchState.ShotAim;
            if (shotWasCalled) Count(Turn).shots++;
            touchPending = true;
            touchSide = Turn;
            shooter = disc;
            shotBallStart = ball.Body.position;
            shotFromAttackHalf = InAttackHalf(shotBallStart, Turn);
            shooter.BeginShot();
            freeKickDisc = null;
            touchesLeft--;
            message = string.Empty;
            Enter(MatchState.Moving);
        }

        private void OnSettled()
        {
            if (State != MatchState.Moving) return;
            EndShot();

            // Disputa de pênaltis: a bola parou sem entrar.
            if (IsShootout)
            {
                monitor.FreezeAll();
                RecordShootoutKick(false);
                return;
            }
            if (tutorial != null)
            {
                // Tutorial: a lição decide se acertou, continua ou repete.
                if (TryKeeperZoneGoalKick()) return;
                tutorial.OnPlayEnded(ball.LastTouchSide == null ? TutorialPlay.Missed
                    : ball.LastTouchSide != Turn ? TutorialPlay.OpponentLast
                    : TutorialPlay.Touched);
                return;
            }
            if (clock <= 0f) EndMatch();
            else if (TryKeeperZoneGoalKick()) return;
            else if (ball.LastTouchSide == null) PassTurn("Errou!");
            else if (ball.LastTouchSide != Turn)
                // Sem toque sobrando a vez passaria de qualquer jeito: não precisa dizer que o adversário tocou por último.
                PassTurn(shotWasCalled ? "Defendeu!" : touchesLeft > 0 ? "Posse do adversário!" : null);
            else if (shotWasCalled) PassTurn("Pra fora!");
            else if (touchesLeft > 0) Enter(MatchState.Aim);
            else PassTurn(null);
        }

        /// <summary>Bola do goleiro (parada na área em volta dele) vira tiro de meta para o time dele, não importa quem jogou.</summary>
        private bool TryKeeperZoneGoalKick()
        {
            if (IsShootout) return false;
            var owner = KeeperBall.Owner(ball.Body.position, ball.Radius, keepers, options.hasGoalkeeper);
            if (owner == null) return false;
            if (tutorial != null)
            {
                monitor.FreezeAll();
                Enter(MatchState.Moving);
                tutorial.OnPlayEnded(TutorialPlay.KeeperBall);
                return true;
            }
            message = string.Empty;
            SetupGoalKick(owner.Value);
            Hold();
            return true;
        }

        private void EndShot()
        {
            if (shooter != null) shooter.EndShot();
            shooter = null;
            CountTouch();
        }

        private SaveData.MatchNumbers Count(TeamSide side) => numbers[(int)side];

        /// <summary>Fim do peteleco: algum botão do time tocou na bola (toque) ou nenhum (toque errado).</summary>
        private void CountTouch()
        {
            if (!touchPending) return;
            touchPending = false;
            bool hit = false;
            foreach (var touch in ball.Touches)
                if (touch.Side == touchSide && touch.What != "goleiro") hit = true;
            if (hit) Count(touchSide).touches++;
            else Count(touchSide).misses++;
        }

        /// <summary>Passa a vez para <paramref name="side"/> com os toques cheios.</summary>
        private void GiveTurn(TeamSide side)
        {
            Turn = side;
            touchesLeft = options.touchesPerTurn;
            freeKickDisc = null;
        }

        private void PassTurn(string reason)
        {
            if (tutorial != null)
            {
                tutorial.OnPlayEnded(TutorialPlay.Missed);
                return;
            }
            // Na disputa de pênaltis, perder a vez (tempo de mira esgotado) é pênalti perdido.
            if (IsShootout)
            {
                RecordShootoutKick(false);
                return;
            }
            GiveTurn(Turn.Opponent());
            message = string.IsNullOrEmpty(reason) ? Txt.Format("Vez do {0}", TeamName(Turn)) : Txt.Get(reason);
            Enter(MatchState.Aim);
        }
    }
}
