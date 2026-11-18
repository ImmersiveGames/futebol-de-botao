using System.Collections.Generic;
using Immersive.Framework.ActivityFlow;
using Immersive.Framework.GameFlow;
using Immersive.Framework.Pause;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FutebolDeBotao
{
    /// <summary>Disputa de pênaltis. Parte da <see cref="MatchController"/>.</summary>
    public sealed partial class MatchController
    {
        // ---- Disputa de pênaltis ----

        /// <summary>
        /// Começa a disputa: um time sorteado bate primeiro, cada um no seu gol de ataque, revezando os botões que
        /// estavam em campo. Em campo ficam só o batedor e o goleiro de quem defende.
        /// </summary>
        private void StartShootout()
        {
            period = Period.Shootout;
            EndShot();
            monitor.StopWatching();
            monitor.FreezeAll();
            for (int i = 0; i < 2; i++)
            {
                shootoutKicks[i].Clear();
                shootoutNext[i] = 0;
                shootoutTakers[i].Clear();
                foreach (var disc in discs[(TeamSide)i])
                    if (disc.isActiveAndEnabled) shootoutTakers[i].Add(disc);
            }
            shootoutFirst = Random.value < 0.5f ? TeamSide.Bottom : TeamSide.Top;
            Noticed?.Invoke(MatchNotice.FinalWhistle);
            NextShootoutKick();
            message = Txt.Get("Disputa de pênaltis!");
            Hold();
        }

        /// <summary>Prepara a próxima cobrança: bate quem cobrou menos (empatados, quem começou).</summary>
        private void NextShootoutKick()
        {
            int bottomKicks = shootoutKicks[(int)TeamSide.Bottom].Count;
            int topKicks = shootoutKicks[(int)TeamSide.Top].Count;
            var side = bottomKicks == topKicks ? shootoutFirst : bottomKicks < topKicks ? TeamSide.Bottom : TeamSide.Top;
            var defending = side.Opponent();

            var takers = shootoutTakers[(int)side];
            Disc kicker = takers.Count > 0 ? takers[shootoutNext[(int)side]++ % takers.Count] : null;
            foreach (var disc in allDiscs) disc.gameObject.SetActive(disc == kicker);
            foreach (var keeper in keepers)
            {
                keeper.gameObject.SetActive(keeper.Side == defending);
                if (keeper.Side == defending) keeper.ResetToCenter();
            }

            ball.ResetTo(FieldLayout.PenaltySpot(defending));
            GiveTurn(side);
            touchesLeft = 1;
            setupIsPenalty = true;
            freeKickDisc = kicker;
            if (kicker != null) PlaceKickerNearest(kicker, 0f);
            BeginPenaltyChoice(Txt.Format("Pênalti do {0}", TeamName(side)));
        }

        /// <summary>Fim de uma cobrança: marca no placar da disputa e para um pouco para todo mundo ver.</summary>
        private void RecordShootoutKick(bool scored)
        {
            shootoutKicks[(int)Turn].Add(scored);
            LastScorer = Turn;
            message = scored ? Txt.Get("GOOOOOL!") : Txt.Get("Perdeu!");
            Noticed?.Invoke(scored ? MatchNotice.Goal : MatchNotice.GoalAnnulled);
            pendingGoal = null;
            Enter(MatchState.Goal);
            if (options.noticeHoldSeconds > 0f) stateTimer = 0f;
            Hold();
        }

        private void ContinueShootout()
        {
            var winner = ShootoutWinner();
            if (winner == null)
            {
                NextShootoutKick();
                return;
            }

            message = Txt.Get("Fim de jogo!");
            MatchSession.LastResult = new MatchResult(Score(TeamSide.Bottom), Score(TeamSide.Top), false,
                ShootoutGoals(TeamSide.Bottom), ShootoutGoals(TeamSide.Top));
            RecordStats(walkover: false);
            Noticed?.Invoke(MatchNotice.FinalWhistle);
            Enter(MatchState.End);
        }

        /// <summary>
        /// Quem já ganhou a disputa: nas 5 cobranças, quando o outro não alcança mais; depois (morte súbita), quando
        /// os dois bateram o mesmo número de vezes e um fez mais. Nulo: continua.
        /// </summary>
        private TeamSide? ShootoutWinner()
        {
            int bottomKicks = shootoutKicks[(int)TeamSide.Bottom].Count, topKicks = shootoutKicks[(int)TeamSide.Top].Count;
            int bottomGoals = ShootoutGoals(TeamSide.Bottom), topGoals = ShootoutGoals(TeamSide.Top);
            if (bottomKicks <= ShootoutRounds && topKicks <= ShootoutRounds)
            {
                if (bottomGoals + (ShootoutRounds - bottomKicks) < topGoals) return TeamSide.Top;
                if (topGoals + (ShootoutRounds - topKicks) < bottomGoals) return TeamSide.Bottom;
            }
            if (bottomKicks == topKicks && bottomKicks >= ShootoutRounds && bottomGoals != topGoals)
                return bottomGoals > topGoals ? TeamSide.Bottom : TeamSide.Top;
            return null;
        }
    }
}
