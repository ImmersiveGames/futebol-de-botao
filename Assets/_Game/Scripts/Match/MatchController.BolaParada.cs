using System.Collections.Generic;
using Immersive.Framework.ActivityFlow;
using Immersive.Framework.GameFlow;
using Immersive.Framework.Pause;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FutebolDeBotao
{
    /// <summary>Bola parada: tiro livre e pênalti (posicionar o batedor, pênalti escondido, lugar livre). Parte da <see cref="MatchController"/>.</summary>
    public sealed partial class MatchController
    {
        /// <summary>
        /// Tiro livre: a bola vai para <paramref name="foulSpot"/> e quem sofreu a falta vira o batedor, posicionado em
        /// volta da bola. Quem fez a falta volta para a formação e ninguém fica na roda em que o batedor gira.
        /// </summary>
        private void SetupFreeKick(Disc offender, Disc victim, Vector2 foulSpot)
        {
            float margin = ball.Radius + 0.02f;
            Vector2 spot = new(
                Mathf.Clamp(foulSpot.x, -FieldLayout.HalfWidth + margin, FieldLayout.HalfWidth - margin),
                Mathf.Clamp(foulSpot.y, -FieldLayout.HalfHeight + margin, FieldLayout.HalfHeight - margin));
            ball.ResetTo(spot);

            // A roda: o batedor gira a esta distância da bola, então os outros ficam do lado de fora dela.
            float clearance = KickerDistance(victim) + victim.Radius * 2f + 0.05f;

            int index = discs[offender.Side].IndexOf(offender);
            offender.PlaceAt(FindFreeSpot(FormationPosition(index, offender.Side), offender, victim, clearance));

            foreach (var disc in allDiscs)
            {
                if (disc == victim || disc == offender || !disc.isActiveAndEnabled) continue;
                Vector2 offset = disc.Body.position - spot;
                if (offset.sqrMagnitude >= clearance * clearance) continue;
                Vector2 away = offset.sqrMagnitude > 0.0001f ? offset.normalized : Vector2.right;
                disc.PlaceAt(FindFreeSpot(spot + away * clearance, disc, victim, clearance));
            }

            GiveTurn(victim.Side);
            freeKickDisc = victim;
            setupIsPenalty = false;
            PlaceKickerNearest(victim, 0f);

            message = Txt.Get("Falta!");
            Enter(MatchState.PenaltySetup);
        }

        /// <summary>
        /// Pênalti: falta dentro da área de quem defende. Bola na marca, quem sofreu bate; os outros atacantes voltam
        /// para a formação e os defensores ficam espalhados na linha do meio-campo.
        /// </summary>
        private void SetupPenalty(Disc offender, Disc victim)
        {
            var defending = offender.Side;
            var attacking = victim.Side;
            Vector2 spot = FieldLayout.PenaltySpot(defending);
            Vector2 attack = FieldLayout.AttackDirection(attacking);

            var attackers = discs[attacking];
            for (int i = 0; i < attackers.Count && i < options.discsPerTeam; i++)
                if (attackers[i] != victim) attackers[i].PlaceAt(FormationPosition(i, attacking));

            var defenders = new List<Disc>();
            foreach (var disc in discs[defending])
                if (disc.isActiveAndEnabled) defenders.Add(disc);
            float spacing = FieldLayout.Width / (defenders.Count + 1);
            for (int i = 0; i < defenders.Count; i++)
                defenders[i].PlaceAt(new Vector2(-FieldLayout.HalfWidth + spacing * (i + 1), 0f));

            foreach (var keeper in keepers)
                if (keeper.isActiveAndEnabled) keeper.ResetToCenter();
            ball.ResetTo(spot);
            setupIsPenalty = true;
            PlaceKickerNearest(victim, 0f);

            GiveTurn(attacking);
            freeKickDisc = victim;
            BeginPenaltyChoice(Txt.Get("Pênalti!"));
        }

        /// <summary>
        /// Começo do pênalti (de falta ou da disputa). Com goleiro, o primeiro a escolher fica escondido: contra a IA
        /// ela escolhe primeiro (o goleiro dela, com o jogador batendo); senão, o batedor.
        /// </summary>
        private void BeginPenaltyChoice(string kickerMessage)
        {
            kickerHidden = keeperHidden = false;
            penaltyKeeperFirst = false;
            if (HiddenPenalty && IsAi(Turn.Opponent()) && !IsAi(Turn))
            {
                penaltyKeeperFirst = true;
                keeperHidden = true;
                message = kickerMessage;
                Enter(MatchState.ShotCall);
                return;
            }
            kickerHidden = HiddenPenalty && IsAi(Turn);
            message = kickerMessage;
            Enter(MatchState.PenaltySetup);
        }

        /// <summary>Goleiro do pênalti pronto: tem que estar de um lado. Depois, o batedor escolhe ou a faixa sai e ele chuta.</summary>
        private void FinishPenaltyKeeper()
        {
            var keeper = DefendingKeeper();
            if (keeper != null && !KeeperSideChosen(keeper))
            {
                if (stateTimer > 0f && !IsAi(Turn.Opponent()))
                {
                    message = Txt.Get("Escolha um lado!");
                    return;
                }
                ForceKeeperSide(keeper);
            }
            if (penaltyKeeperFirst)
            {
                message = string.Empty;
                Enter(MatchState.PenaltySetup);
                return;
            }
            EnterShotAim();
        }

        private Goalkeeper DefendingKeeper()
        {
            foreach (var keeper in keepers)
                if (keeper != null && keeper.isActiveAndEnabled && keeper.Side == Turn.Opponent()) return keeper;
            return null;
        }

        private bool KickerSideChosen() =>
            freeKickDisc == null || Mathf.Abs(PenaltyAngle(freeKickDisc)) >= options.penaltyKickerMinDegrees - 0.5f;

        private bool KeeperSideChosen(Goalkeeper keeper) =>
            Mathf.Abs(keeper.SideFraction) >= options.penaltyKeeperMinSide - 0.01f;

        /// <summary>Tempo esgotado com o batedor no meio: vai para o lado mais perto (no meio exato, sorteado).</summary>
        private void ForceKickerSide()
        {
            var disc = freeKickDisc;
            if (disc == null) return;
            float angle = PenaltyAngle(disc);
            float sign = Mathf.Abs(angle) < 0.5f ? (Random.value < 0.5f ? -1f : 1f) : Mathf.Sign(angle);
            float min = options.penaltyKickerMinDegrees;
            if (!TryPlaceKicker(disc, sign * min)) TryPlaceKicker(disc, -sign * min);
        }

        /// <summary>Tempo esgotado com o goleiro no meio: vai para o lado mais perto (no meio exato, sorteado).</summary>
        private static void ForceKeeperSide(Goalkeeper keeper, float minSide)
        {
            float side = keeper.SideFraction;
            float sign = Mathf.Abs(side) < 0.01f ? (Random.value < 0.5f ? -1f : 1f) : Mathf.Sign(side);
            float center = (keeper.MinX + keeper.MaxX) * 0.5f;
            float half = (keeper.MaxX - keeper.MinX) * 0.5f;
            keeper.SnapTo(center + sign * half * minSide);
        }

        private void ForceKeeperSide(Goalkeeper keeper) => ForceKeeperSide(keeper, options.penaltyKeeperMinSide);

        /// <summary>
        /// Faixa preta de lado a lado: cobre o batedor e a bola (ou o goleiro e o gol) de quem já escolheu.
        /// </summary>
        private void UpdatePenaltyCover()
        {
            bool show = kickerHidden || keeperHidden;
            if (!show)
            {
                if (penaltyCover != null) penaltyCover.enabled = false;
                return;
            }

            float minY, maxY;
            if (kickerHidden)
            {
                Vector2 ballPosition = ball.Body.position;
                float up = FieldLayout.AttackDirection(Turn).y;
                float discRadius = freeKickDisc != null ? freeKickDisc.Radius : ball.Radius;
                float back = freeKickDisc != null ? KickerDistance(freeKickDisc) + discRadius : discRadius;
                float a = ballPosition.y - up * (back + PenaltyCoverMargin);
                float b = ballPosition.y + up * (ball.Radius + PenaltyCoverMargin);
                minY = Mathf.Min(a, b);
                maxY = Mathf.Max(a, b);
            }
            else
            {
                var keeper = DefendingKeeper();
                float goalY = FieldLayout.GoalLineY(Turn.Opponent());
                float sign = Mathf.Sign(goalY);
                float front = goalY - sign * 0.6f;
                if (keeper != null && keeper.TryGetComponent(out Collider2D box))
                    front = sign > 0f ? box.bounds.min.y : box.bounds.max.y;
                float a = goalY + sign * FieldLayout.GoalDepth;
                float b = front - sign * PenaltyCoverMargin;
                minY = Mathf.Min(a, b);
                maxY = Mathf.Max(a, b);
            }

            if (penaltyCover == null)
            {
                var go = new GameObject("Faixa do pênalti");
                go.transform.SetParent(transform, false);
                penaltyCover = go.AddComponent<SpriteRenderer>();
                var texture = Texture2D.whiteTexture;
                penaltyCover.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), texture.width);
                penaltyCover.color = Color.black;
                penaltyCover.sortingOrder = PenaltyCoverOrder;
            }
            penaltyCover.enabled = true;
            var t = penaltyCover.transform;
            t.position = new Vector3(0f, (minY + maxY) * 0.5f, 0f);
            Vector3 parentScale = transform.lossyScale;
            float width = FieldLayout.Width + 1f;
            t.localScale = new Vector3(width / Mathf.Max(parentScale.x, 0.0001f), (maxY - minY) / Mathf.Max(parentScale.y, 0.0001f), 1f);
        }

        private float KickerDistance(Disc disc) => disc.Radius + ball.Radius + options.penaltyDiscGap;

        /// <summary>
        /// Põe o batedor em volta da bola. <paramref name="angleDegrees"/> = 0 é bem atrás (para o lado do próprio gol),
        /// sinal = lado. No pênalti fica preso ao arco; no tiro livre dá a volta inteira, mas não entra em cima de
        /// outro botão, do goleiro ou fora do campo (aí ele fica onde estava e devolve false).
        /// </summary>
        private bool TryPlaceKicker(Disc disc, float angleDegrees)
        {
            float angle = setupIsPenalty
                ? Mathf.Clamp(angleDegrees, -options.penaltyArcDegrees, options.penaltyArcDegrees)
                : Mathf.DeltaAngle(0f, angleDegrees);
            Vector2 back = -FieldLayout.AttackDirection(disc.Side);
            Vector2 dir = (Vector2)(Quaternion.Euler(0f, 0f, angle) * back);
            Vector2 position = ball.Body.position + dir * KickerDistance(disc);
            if (!IsFreeSpot(position, disc, null, disc.Radius + ball.Radius + 0.05f)) return false;
            disc.PlaceAt(position);
            return true;
        }

        /// <summary>Batedor no ângulo livre mais perto de <paramref name="angleDegrees"/>; sem nenhum, no lugar livre mais perto.</summary>
        private void PlaceKickerNearest(Disc disc, float angleDegrees)
        {
            for (int step = 0; step <= 12; step++)
            {
                if (TryPlaceKicker(disc, angleDegrees + step * 15f)) return;
                if (step > 0 && step < 12 && TryPlaceKicker(disc, angleDegrees - step * 15f)) return;
            }
            Vector2 back = -FieldLayout.AttackDirection(disc.Side);
            disc.PlaceAt(FindFreeSpot(ball.Body.position + back * KickerDistance(disc), disc, null, disc.Radius + ball.Radius + 0.05f));
        }

        /// <summary>A IA posiciona o batedor (mesmas regras do jogador). Devolve false se o lugar não serve.</summary>
        public bool TryPlaceKicker(float angleDegrees) =>
            State == MatchState.PenaltySetup && freeKickDisc != null && TryPlaceKicker(freeKickDisc, angleDegrees);

        /// <summary>Ângulo do batedor para bater reto na bola na direção <paramref name="shotDirection"/>.</summary>
        public float KickerAngleFor(Vector2 shotDirection) =>
            Vector2.SignedAngle(-FieldLayout.AttackDirection(Turn), -shotDirection);

        private float PenaltyAngle(Disc disc)
        {
            Vector2 back = -FieldLayout.AttackDirection(disc.Side);
            return Vector2.SignedAngle(back, disc.Body.position - ball.Body.position);
        }

        /// <summary>Arrastar (ou A/D, setas, analógico, D-pad) gira o batedor em volta da bola (no pênalti, dentro do arco).</summary>
        private void UpdatePenaltySetup()
        {
            var disc = freeKickDisc;
            if (disc == null || IsAi(Turn)) return;

            var input = MatchInput.For(Turn);
            if (input.PointerHeld && input.TryGetPointer(out var screen) && !UiPointer.IsOverUi())
            {
                worldCamera = WorldCamera.Resolve(worldCamera);
                if (worldCamera != null)
                {
                    Vector2 world = WorldCamera.ScreenToWorld(worldCamera, screen);
                    Vector2 offset = world - ball.Body.position;
                    // Toques longe da bola (no HUD, por exemplo) não mexem o botão.
                    if (offset.sqrMagnitude > 0.01f && offset.magnitude < 3f)
                    {
                        Vector2 back = -FieldLayout.AttackDirection(disc.Side);
                        TryPlaceKicker(disc, Vector2.SignedAngle(back, offset));
                        return;
                    }
                }
            }

            // Direita leva o batedor (atrás da bola) para a direita de quem joga.
            float axis = input.SideAxis * MatchInput.TurnSign(Turn);
            if (axis != 0f) TryPlaceKicker(disc, PenaltyAngle(disc) + axis * 90f * Time.deltaTime);
        }

        private void FinishPenaltySetup()
        {
            if (HiddenPenalty)
            {
                if (!KickerSideChosen())
                {
                    if (stateTimer > 0f && !IsAi(Turn))
                    {
                        message = Txt.Get("Escolha um lado!");
                        return;
                    }
                    ForceKickerSide();
                }
                if (penaltyKeeperFirst)
                {
                    // O goleiro já escolheu: a faixa sai e o batedor chuta.
                    EnterShotAim();
                    return;
                }
                kickerHidden = true;
                message = Txt.Get("Escolha o canto!");
                Enter(MatchState.ShotCall);
            }
            else if (setupIsPenalty)
            {
                message = Txt.Get("Chute!");
                BeginShotCall();
            }
            else
            {
                // Tiro livre: a vez começa normal; no campo de ataque o "Pro Gol!" fica disponível como sempre.
                message = Txt.Get("Tiro livre!");
                Enter(MatchState.Aim);
            }
        }

        /// <summary>
        /// Lugar livre para um botão: dentro do campo, longe de <paramref name="ballClearance"/> do centro da bola,
        /// sem encostar em outro botão (menos <paramref name="ignore"/>) nem no goleiro.
        /// </summary>
        private bool IsFreeSpot(Vector2 position, Disc disc, Disc ignore, float ballClearance)
        {
            float radius = disc.Radius;
            if (Mathf.Abs(position.x) > FieldLayout.HalfWidth - radius - 0.02f) return false;
            if (Mathf.Abs(position.y) > FieldLayout.HalfHeight - radius - 0.02f) return false;
            if ((position - ball.Body.position).sqrMagnitude < ballClearance * ballClearance) return false;

            foreach (var other in allDiscs)
            {
                if (other == disc || other == ignore || !other.isActiveAndEnabled) continue;
                float minDistance = radius + other.Radius + 0.05f;
                if ((position - other.Body.position).sqrMagnitude < minDistance * minDistance) return false;
            }

            foreach (var keeper in keepers)
            {
                if (!keeper.isActiveAndEnabled || !keeper.TryGetComponent(out Collider2D keeperCollider)) continue;
                var bounds = keeperCollider.bounds;
                float gap = radius + 0.05f;
                if (bounds.SqrDistance(new Vector3(position.x, position.y, bounds.center.z)) < gap * gap) return false;
            }
            return true;
        }

        /// <summary>O lugar livre mais perto de <paramref name="desired"/>, procurando em anéis cada vez maiores.</summary>
        private Vector2 FindFreeSpot(Vector2 desired, Disc disc, Disc ignore, float ballClearance)
        {
            if (IsFreeSpot(desired, disc, ignore, ballClearance)) return desired;
            const int directions = 24;
            for (float distance = 0.15f; distance <= 8f; distance += 0.15f)
            for (int i = 0; i < directions; i++)
            {
                float angle = i * Mathf.PI * 2f / directions;
                Vector2 candidate = desired + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                if (IsFreeSpot(candidate, disc, ignore, ballClearance)) return candidate;
            }
            return desired;
        }
    }
}
