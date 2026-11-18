using System.Collections.Generic;
using Immersive.Framework.ActivityFlow;
using Immersive.Framework.GameFlow;
using Immersive.Framework.Pause;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FutebolDeBotao
{
    /// <summary>Tiro de meta: posicionar a bola. Parte da <see cref="MatchController"/>.</summary>
    public sealed partial class MatchController
    {
        // ---- Tiro de meta: posicionar a bola ----

        /// <summary>Até onde a bola anda para cada lado no tiro de meta: a largura da área.</summary>
        public float GoalKickHalfRange => FieldLayout.AreaWidth * 0.5f - ball.Radius;

        /// <summary>Arrastar a bola (começando nela) leva ela para os lados; A/D, setas, analógico e D-pad também.</summary>
        private void UpdateGoalKickPlacement()
        {
            var input = MatchInput.For(Turn);
            if (!input.PointerHeld) draggingGoalKickBall = false;
            else if (input.TryGetPointer(out var screen))
            {
                worldCamera = WorldCamera.Resolve(worldCamera);
                if (worldCamera != null)
                {
                    Vector2 world = WorldCamera.ScreenToWorld(worldCamera, screen);
                    // Área de toque um pouco maior que a bola, que é pequena (igual à mira do tiro de meta).
                    if (input.PointerPressedThisFrame && !UiPointer.IsOverUi() &&
                        Vector2.Distance(world, ball.Body.position) <= ball.Radius + 0.3f)
                        draggingGoalKickBall = true;
                    if (draggingGoalKickBall)
                    {
                        MoveGoalKickBall(world.x);
                        return;
                    }
                }
            }

            float axis = input.SideAxis * MatchInput.ScreenSign(Turn);
            if (axis != 0f) MoveGoalKickBall(ball.Body.position.x + axis * 3f * Time.deltaTime);
        }

        /// <summary>
        /// Leva a bola do tiro de meta até <paramref name="targetX"/> (dentro da largura da área), parando antes de
        /// encostar num botão. Usada também pela IA.
        /// </summary>
        public void MoveGoalKickBall(float targetX)
        {
            if (!IsPlacingGoalKick) return;
            float range = GoalKickHalfRange;
            targetX = Mathf.Clamp(targetX, -range, range);
            Vector2 position = ball.Body.position;
            float step = Mathf.Sign(targetX - position.x) * 0.02f;
            while (Mathf.Abs(targetX - position.x) > 0.001f)
            {
                var next = new Vector2(Mathf.Abs(targetX - position.x) <= Mathf.Abs(step) ? targetX : position.x + step, position.y);
                if (!BallSpotFree(next)) break;
                position = next;
            }
            if (position != ball.Body.position) ball.ResetTo(position);
        }

        private bool BallSpotFree(Vector2 position)
        {
            foreach (var other in allDiscs)
            {
                if (!other.isActiveAndEnabled) continue;
                float minDistance = ball.Radius + other.Radius + 0.02f;
                if ((position - other.Body.position).sqrMagnitude < minDistance * minDistance) return false;
            }
            return true;
        }

        /// <summary>"Pronto" no tiro de meta: a bola fica onde está e começa a mira (no mesmo tempo).</summary>
        private void FinishGoalKickPlacement()
        {
            if (!IsPlacingGoalKick) return;
            goalKickPlacing = false;
            draggingGoalKickBall = false;
            message = Txt.Get("Tiro de meta!");
            StateVersion++;
            aim.Cancel();
            ApplyControls();
        }

        /// <summary>
        /// Na saída, os botões de quem não sai que caem dentro do círculo central vão para a linha dele (o centro do
        /// botão em cima da linha), na mesma direção. Quem dá a saída pode ficar dentro.
        /// </summary>
        private void KeepOutOfCenterCircle(TeamSide side)
        {
            if (!discs.TryGetValue(side, out var team)) return;
            float radius = FieldLayout.CenterCircleRadius;
            foreach (var disc in team)
            {
                if (!disc.isActiveAndEnabled) continue;
                Vector2 position = disc.Body.position;
                if (position.sqrMagnitude >= radius * radius) continue;
                // No meio exato, recua para o próprio campo.
                Vector2 direction = position.sqrMagnitude > 0.0001f ? position.normalized : -FieldLayout.AttackDirection(side);
                Vector2 target = direction * radius;
                disc.PlaceAt(IsFreeSpot(target, disc, null, disc.Radius + ball.Radius + 0.05f)
                    ? target
                    : FindFreeSpot(direction * (radius + disc.Radius), disc, null, disc.Radius + ball.Radius + 0.05f));
            }
        }

        /// <summary>Todos os botões na formação inicial; quem cair em cima da bola vai para o lugar livre mais perto.</summary>
        private void ResetFormation()
        {
            foreach (var pair in discs)
                for (int i = 0; i < pair.Value.Count && i < options.discsPerTeam; i++)
                    pair.Value[i].PlaceAt(FormationPosition(i, pair.Key));

            foreach (var disc in allDiscs)
            {
                if (!disc.isActiveAndEnabled) continue;
                float clearance = disc.Radius + ball.Radius + 0.3f;
                if ((disc.Body.position - ball.Body.position).sqrMagnitude >= clearance * clearance) continue;
                disc.PlaceAt(FindFreeSpot(disc.Body.position, disc, null, clearance));
            }
        }

        /// <summary>
        /// Fim do tempo. Empatado: prorrogação (se ligada e ainda não jogada); depois, disputa de pênaltis (se ligada e
        /// com goleiro). Senão termina como está.
        /// </summary>
        private void EndMatch()
        {
            if (Score(TeamSide.Bottom) == Score(TeamSide.Top))
            {
                if (period == Period.Regular && options.ExtraTimeSeconds > 0f)
                {
                    StartExtraTime();
                    return;
                }
                if (period != Period.Shootout && options.PenaltiesEnabled)
                {
                    StartShootout();
                    return;
                }
            }
            FinishMatch();
        }

        private void StartExtraTime()
        {
            period = Period.ExtraTime;
            clock = options.ExtraTimeSeconds;
            KickOff(Random.value < 0.5f ? TeamSide.Bottom : TeamSide.Top);
            message = Txt.Get("Prorrogação!");
            Hold();
        }
    }
}
