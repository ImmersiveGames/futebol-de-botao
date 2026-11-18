using System.Collections.Generic;
using Immersive.Framework.ActivityFlow;
using Immersive.Framework.GameFlow;
using Immersive.Framework.Pause;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FutebolDeBotao
{
    /// <summary>Gol: bola na rede, placar e recomeço. Parte da <see cref="MatchController"/>.</summary>
    public sealed partial class MatchController
    {
        // ---- Gol ----

        private void OnBallEntered(GoalTrigger goal, Ball enteredBall)
        {
            if (State != MatchState.Moving) return;

            EndShot();
            monitor.StopWatching();
            enteredBall.StopInGoal();

            // Disputa de pênaltis: entrou no gol de quem defende = gol.
            if (IsShootout)
            {
                RecordShootoutKick(goal.DefendingSide == Turn.Opponent());
                return;
            }
            pendingGoal = goal;

            // Gol contra: a jogada era de quem defende este gol (o chute saiu de um botão dele), mesmo que a bola tenha
            // batido num adversário. Chute do atacante que desvia num defensor é gol normal do atacante.
            // Gol normal só vale num "Pro Gol!" (sem goleiro: chute do campo de ataque). Com "Gol após parede: Anulado",
            // a bola tocar a parede lateral anula qualquer gol, contra ou não.
            bool ownGoal = Turn == goal.DefendingSide;
            bool wallOk = options.goalAfterWallIsValid || !enteredBall.TouchedWallSinceShot;
            bool called = options.hasGoalkeeper ? shotWasCalled : shotFromAttackHalf;
            pendingGoalValid = wallOk && (ownGoal || called);

            var scorer = goal.DefendingSide.Opponent();
            LastScorer = scorer;
            if (pendingGoalValid)
            {
                score[(int)scorer]++;
                if (score[(int)TeamSide.Bottom] < score[(int)TeamSide.Top]) playerTrailed = true;
                Count(scorer).goals++;
                Count(goal.DefendingSide).conceded++;
                if (ownGoal) Count(Turn).ownGoals++;
                else if (shotWasCalled) Count(scorer).shotGoals++;
                message = ownGoal ? Txt.Get("Gol contra!") : Txt.Get("GOOOOOL!");
            }
            else
            {
                message = Txt.Get(!wallOk ? "Gol anulado: a bola tocou a parede lateral."
                    : options.hasGoalkeeper ? "Gol anulado: não avisou o \"Pro Gol!\"."
                    : "Gol anulado: chute do campo de defesa.");
            }

            Noticed?.Invoke(pendingGoalValid ? MatchNotice.Goal : MatchNotice.GoalAnnulled);
            Enter(MatchState.Goal);
            // O aviso já é a pausa do gol: acabou, segue direto para a saída ou o tiro de meta.
            if (options.noticeHoldSeconds > 0f) stateTimer = 0f;
            Hold();
        }

        private void ResolveGoal()
        {
            if (IsShootout)
            {
                ContinueShootout();
                return;
            }
            var defending = pendingGoal.DefendingSide;
            pendingGoal = null;

            if (tutorial != null)
            {
                // Sai do estado de gol (senão o gol seria resolvido de novo a cada quadro); a lição monta a próxima jogada.
                Enter(MatchState.Moving);
                tutorial.OnGoal(defending.Opponent(), pendingGoalValid);
                return;
            }

            if (clock <= 0f)
            {
                EndMatch();
                return;
            }

            if (pendingGoalValid)
            {
                // Quem levou o gol dá a saída.
                KickOff(defending);
                return;
            }

            SetupGoalKick(defending);
        }

        /// <summary>
        /// Tiro de meta: todos os botões voltam para a formação, os goleiros para o centro e a bola vai para a frente do
        /// goleiro de quem defendeu. Ele chuta direto na bola.
        /// </summary>
        private void SetupGoalKick(TeamSide defending)
        {
            monitor.FreezeAll();
            Vector2 towardCenter = FieldLayout.AttackDirection(defending);
            foreach (var other in keepers)
                if (other.isActiveAndEnabled) other.ResetToCenter();
            var keeper = FindKeeper(defending);
            Vector2 spot = keeper != null
                ? (Vector2)keeper.transform.position + towardCenter * 0.8f
                : new Vector2(0f, FieldLayout.GoalLineY(defending)) + towardCenter * 1f;
            ball.ResetTo(spot);
            ResetFormation();

            GiveTurn(defending);
            message = string.IsNullOrEmpty(message) ? Txt.Get("Tiro de meta!") : $"{message} {Txt.Get("Tiro de meta!")}";
            goalKickPlacing = true;
            draggingGoalKickBall = false;
            Enter(MatchState.GoalKick);
        }
    }
}
