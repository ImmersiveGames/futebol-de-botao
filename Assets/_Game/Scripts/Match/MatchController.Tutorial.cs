using System.Collections.Generic;
using Immersive.Framework.ActivityFlow;
using Immersive.Framework.GameFlow;
using Immersive.Framework.Pause;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FutebolDeBotao
{
    /// <summary>Ganchos do tutorial. Parte da <see cref="MatchController"/>.</summary>
    public sealed partial class MatchController
    {
        // ---- Tutorial ----

        /// <summary>Botões do time (inclusive os desligados), na ordem do nome.</summary>
        public IReadOnlyList<Disc> TeamDiscs(TeamSide side) => discs[side];

        /// <summary>
        /// Tutorial: monta a jogada da lição. Botões nas posições dadas (os que faltarem, na formação), goleiros no centro,
        /// a bola no lugar, a vez de <paramref name="turn"/> com os toques cheios e o placar zerado.
        /// </summary>
        public void TutorialSetup(TeamSide turn, Vector2 ballPosition, Vector2[] bottom, Vector2[] top)
        {
            EndShot();
            monitor.StopWatching();
            monitor.FreezeAll();
            holdTimer = 0f;
            restoreControls = false;
            score[0] = score[1] = 0;
            playerTrailed = false;
            farFouls[0] = farFouls[1] = 0;
            kickerHidden = keeperHidden = false;
            penaltyKeeperFirst = false;
            setupIsPenalty = false;
            goalKickPlacing = false;
            shotWasCalled = false;
            pendingGoal = null;

            foreach (var pair in discs)
            {
                var positions = pair.Key == TeamSide.Bottom ? bottom : top;
                for (int i = 0; i < pair.Value.Count && i < options.discsPerTeam; i++)
                {
                    pair.Value[i].gameObject.SetActive(true);
                    pair.Value[i].PlaceAt(positions != null && i < positions.Length ? positions[i] : FormationPosition(i, pair.Key));
                }
            }
            foreach (var keeper in keepers)
                if (keeper.isActiveAndEnabled) keeper.ResetToCenter();
            ball.ResetTo(ballPosition);

            GiveTurn(turn);
            message = string.Empty;
            Enter(MatchState.Aim);
        }

        /// <summary>Tutorial: segue a vez com os toques que sobraram.</summary>
        public void TutorialContinue()
        {
            Enter(MatchState.Aim);
        }

        /// <summary>Tutorial: o time da vez avisa "Pro Gol!" (a IA, na lição de defender).</summary>
        public void TutorialCallShot()
        {
            message = Txt.Get("Pro Gol!");
            BeginShotCall();
        }

        /// <summary>Tutorial: falta de <paramref name="offender"/> em <paramref name="victim"/>, tiro livre onde a vítima está.</summary>
        public void TutorialFreeKick(Disc offender, Disc victim)
        {
            SetupFreeKick(offender, victim, victim.Body.position);
            Noticed?.Invoke(MatchNotice.Foul);
        }

        /// <summary>Tutorial: pênalti para o time de <paramref name="victim"/>.</summary>
        public void TutorialPenalty(Disc offender, Disc victim)
        {
            SetupPenalty(offender, victim);
            Noticed?.Invoke(MatchNotice.Penalty);
        }

        /// <summary>Tutorial: mostra um aviso no meio do campo; com <paramref name="hold"/>, para o jogo enquanto ele aparece.</summary>
        public void TutorialMessage(string text, bool hold)
        {
            message = text;
            if (hold) Hold();
        }

        /// <summary>Tutorial: o balão travou ou destravou os controles.</summary>
        public void TutorialRefreshControls()
        {
            aim.Cancel();
            ApplyControls();
        }
    }
}
