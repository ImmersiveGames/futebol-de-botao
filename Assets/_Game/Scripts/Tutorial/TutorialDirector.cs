using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FutebolDeBotao
{
    /// <summary>Como terminou uma jogada no tutorial (a partida avisa o <see cref="TutorialDirector"/>).</summary>
    public enum TutorialPlay
    {
        /// <summary>O botão não tocou na bola.</summary>
        Missed,
        /// <summary>O último toque na bola foi do adversário (botão ou goleiro).</summary>
        OpponentLast,
        /// <summary>A bola parou na área em volta de um goleiro.</summary>
        KeeperBall,
        /// <summary>O time da vez tocou na bola por último.</summary>
        Touched
    }

    /// <summary>
    /// Conduz as lições do <see cref="Tutorial"/> dentro da partida. Cada lição é uma lista de passos: um passo que explica
    /// (balão com Continuar, controles travados) ou um passo com objetivo (acertar a bola, dar 3 toques, fazer o gol com
    /// Pro Gol!, defender, bater o tiro livre, bater o pênalti). O passo monta a jogada pela <see cref="MatchController"/>;
    /// a partida avisa como cada jogada terminou e o passo decide: acertou (próximo passo), continua ou repete com um
    /// aviso. No fim da lição aparecem as moedas e os botões Próxima lição, Repetir e Menu (no HUD).
    /// </summary>
    public sealed class TutorialDirector : MonoBehaviour
    {
        private enum Goal
        {
            None,
            Hit,
            Far,
            ThreeTouches,
            Score,
            Defend,
            FreeKickPlace,
            FreeKickShot,
            Penalty
        }

        private sealed class Step
        {
            public Goal Goal;
            /// <summary>Instrução curta na faixa de cima, enquanto joga.</summary>
            public Func<TutorialInputKind, string> Short;
            /// <summary>Explicação no pop-up ao entrar no passo (nula = sem pop-up).</summary>
            public Func<TutorialInputKind, string> Popup;
            /// <summary>Monta a jogada do passo; nulo = segue com a mesa como está.</summary>
            public Action Setup;
        }

        private MatchController match;
        private List<Step>[] lessons;
        private List<Step> steps;
        private int current;
        private bool popupOpen;
        private int unlockFrame = -1;
        private bool completed;

        public void Configure(MatchController matchController)
        {
            match = matchController;
            lessons ??= BuildLessons();
        }

        // ---- O que o HUD lê ----

        /// <summary>Com o que o jogador está jogando (o texto muda conforme o aparelho).</summary>
        public TutorialInputKind Input { get; private set; } = DefaultInput();

        public int Lesson => Tutorial.Lesson;

        public bool IsLastLesson => Lesson >= Tutorial.LessonCount - 1;

        /// <summary>"Lição 2 de 6: Vez e toques".</summary>
        public string Header => TutorialTexts.Header(Lesson);

        /// <summary>Instrução curta do passo atual (faixa de cima).</summary>
        public string Instruction => HasStep ? steps[current].Short(Input) : string.Empty;

        /// <summary>Explicação do pop-up aberto.</summary>
        public string PopupText => HasStep && steps[current].Popup != null ? steps[current].Popup(Input) : string.Empty;

        /// <summary>O pop-up de explicação está aberto (fecha no Continuar).</summary>
        public bool WaitingContinue => popupOpen && !completed;

        /// <summary>O pop-up fechou neste quadro: o mesmo E/A não vale como Pronto na partida.</summary>
        public bool UnlockedThisFrame => unlockFrame == Time.frameCount;

        private bool HasStep => steps != null && current >= 0 && current < steps.Count;

        /// <summary>A lição terminou: a tela de lição concluída está aberta.</summary>
        public bool Completed => completed;

        /// <summary>Moedas que a lição pagou (0 se já tinha feito) e o bônus de completar tudo.</summary>
        public int CoinsEarned { get; private set; }
        public int BonusEarned { get; private set; }

        /// <summary>Ninguém joga: explicando ou com a lição concluída na tela.</summary>
        public bool LocksControls => popupOpen || completed;

        // ---- Começo e navegação ----

        /// <summary>A partida começou (ou foi reiniciada pela pausa): começa a lição atual do começo.</summary>
        public void Begin() => StartLesson(Tutorial.Lesson);

        private void StartLesson(int lesson)
        {
            Tutorial.Lesson = Mathf.Clamp(lesson, 0, Tutorial.LessonCount - 1);
            steps = lessons[Tutorial.Lesson];
            completed = false;
            CoinsEarned = BonusEarned = 0;
            EnterStep(0, null, true);
        }

        /// <summary>"Continuar" no pop-up de explicação: fecha e libera a jogada.</summary>
        public void Continue()
        {
            if (!WaitingContinue) return;
            popupOpen = false;
            unlockFrame = Time.frameCount;
            match.TutorialRefreshControls();
        }

        /// <summary>"Pular": vai para a próxima lição sem completar esta (na última, volta ao Menu).</summary>
        public void Skip()
        {
            if (IsLastLesson) GoToMenu();
            else StartLesson(Lesson + 1);
        }

        /// <summary>"Próxima lição" na tela de lição concluída (na última, volta ao Menu).</summary>
        public void NextLesson()
        {
            if (IsLastLesson) GoToMenu();
            else StartLesson(Lesson + 1);
        }

        /// <summary>"Repetir": a mesma lição do começo.</summary>
        public void Repeat() => StartLesson(Lesson);

        /// <summary>"Menu": sai do tutorial pelo mesmo caminho do Menu da pausa.</summary>
        public void GoToMenu()
        {
            var pause = FindAnyObjectByType<PauseMenu>(FindObjectsInactive.Include);
            if (pause != null) pause.BackToMenu();
            else Debug.LogWarning("[Tutorial] Sem PauseMenu na cena para voltar ao Menu.");
        }

        private void EnterStep(int index, string feedback, bool showPopup)
        {
            if (index >= steps.Count)
            {
                CompleteLesson(feedback);
                return;
            }
            current = index;
            var step = steps[index];
            // Trava antes de montar a jogada, para os controles já nascerem travados com o pop-up aberto.
            // Repetindo o passo, a explicação não aparece de novo.
            popupOpen = showPopup && step.Popup != null;
            step.Setup?.Invoke();
            match.TutorialRefreshControls();
            if (!string.IsNullOrEmpty(feedback)) match.TutorialMessage(feedback, true);
        }

        /// <summary>Errou: volta ao último passo que monta a jogada e repete, com o aviso.</summary>
        private void Retry(string feedback)
        {
            int index = current;
            while (index > 0 && steps[index].Setup == null) index--;
            EnterStep(index, feedback, false);
        }

        private void Succeed(string feedback) => EnterStep(current + 1, feedback, true);

        private void CompleteLesson(string feedback)
        {
            completed = true;
            popupOpen = false;
            (CoinsEarned, BonusEarned) = Tutorial.Complete(Lesson);
            match.TutorialMessage(feedback ?? string.Empty, false);
            match.TutorialRefreshControls();
        }

        private Goal CurrentGoal => HasStep ? steps[current].Goal : Goal.None;

        // ---- Avisos da partida ----

        /// <summary>A jogada terminou sem gol.</summary>
        public void OnPlayEnded(TutorialPlay play)
        {
            if (completed) return;
            switch (CurrentGoal)
            {
                case Goal.Hit:
                    if (play == TutorialPlay.Missed) Retry(TutorialTexts.Missed);
                    else Succeed(TutorialTexts.Nice);
                    break;

                case Goal.Far:
                    if (play == TutorialPlay.Missed) Retry(TutorialTexts.Missed);
                    else if (match.Ball.Body.position.y > 0f) Succeed(TutorialTexts.Nice);
                    else Retry(TutorialTexts.TooShort);
                    break;

                case Goal.ThreeTouches:
                    if (play == TutorialPlay.Touched)
                    {
                        if (match.TouchesLeft <= 0)
                        {
                            Succeed(TutorialTexts.Nice);
                            return;
                        }
                        match.TutorialContinue();
                        match.TutorialMessage(TutorialTexts.TouchesLeft(match.TouchesLeft), false);
                        return;
                    }
                    Retry(play switch
                    {
                        TutorialPlay.Missed => TutorialTexts.MissedTurn,
                        TutorialPlay.KeeperBall => TutorialTexts.KeeperBall,
                        _ => TutorialTexts.OpponentLast
                    });
                    break;

                case Goal.Score:
                    if (match.ShotWasCalled) Retry(TutorialTexts.Saved);
                    else if (play == TutorialPlay.Touched && match.TouchesLeft > 0) match.TutorialContinue();
                    else Retry(play switch
                    {
                        TutorialPlay.Missed => TutorialTexts.Missed,
                        TutorialPlay.KeeperBall => TutorialTexts.KeeperBall,
                        TutorialPlay.OpponentLast => TutorialTexts.OpponentLast,
                        _ => TutorialTexts.NoTouches
                    });
                    break;

                case Goal.Defend:
                    Succeed(TutorialTexts.Defended);
                    break;

                case Goal.FreeKickShot:
                    if (play == TutorialPlay.Missed) Retry(TutorialTexts.FreeKickMissed);
                    else Succeed(TutorialTexts.FreeKickDone);
                    break;

                case Goal.Penalty:
                    Retry(TutorialTexts.PenaltySaved);
                    break;

                default:
                    Retry(TutorialTexts.Missed);
                    break;
            }
        }

        /// <summary>A bola entrou num gol (<paramref name="valid"/> = o gol valeu).</summary>
        public void OnGoal(TeamSide scorer, bool valid)
        {
            if (completed) return;
            bool mine = scorer == TeamSide.Bottom;
            switch (CurrentGoal)
            {
                case Goal.Hit:
                case Goal.Far:
                    if (mine) Succeed(TutorialTexts.Nice);
                    else Retry(TutorialTexts.Missed);
                    break;

                case Goal.Score:
                    if (mine && valid) Succeed(TutorialTexts.Goal);
                    else Retry(!mine ? TutorialTexts.Saved : match.ShotWasCalled ? TutorialTexts.WallGoal : TutorialTexts.NotCalled);
                    break;

                case Goal.Defend:
                    if (!mine && valid) Retry(TutorialTexts.Conceded);
                    else Succeed(TutorialTexts.Defended);
                    break;

                case Goal.FreeKickShot:
                    if (mine) Succeed(valid ? TutorialTexts.Goal : TutorialTexts.FreeKickDone);
                    else Retry(TutorialTexts.FreeKickMissed);
                    break;

                case Goal.Penalty:
                    if (mine && valid) Succeed(TutorialTexts.PenaltyGoal);
                    else Retry(TutorialTexts.PenaltySaved);
                    break;

                default:
                    Retry(TutorialTexts.OpponentLast);
                    break;
            }
        }

        /// <summary>Falta de verdade no tutorial: não vira tiro livre, a jogada repete com o aviso.</summary>
        public void OnFoul(TeamSide offender)
        {
            if (completed) return;
            Retry(offender == TeamSide.Bottom ? TutorialTexts.Foul : TutorialTexts.OpponentFoul);
        }

        // ---- Quadro a quadro ----

        private void Update()
        {
            if (match == null || Time.timeScale <= 0f) return;
            DetectInput();

            // Teclado e controle: E/A no pop-up é o Continuar; na lição concluída, a Próxima lição.
            var input = MatchInput.For(TeamSide.Bottom);
            if (completed && input.SelectPressedThisFrame)
            {
                NextLesson();
                return;
            }
            if (WaitingContinue && !match.IsHolding && input.SelectPressedThisFrame)
            {
                Continue();
                return;
            }

            // Tiro livre: depois do Pronto (a vez começa normal), o passo seguinte é o chute.
            if (!completed && CurrentGoal == Goal.FreeKickPlace && match.State == MatchState.Aim && match.Turn == TeamSide.Bottom)
                EnterStep(current + 1, null, true);
        }

        private static TutorialInputKind DefaultInput()
        {
            if (Application.isMobilePlatform) return TutorialInputKind.Touch;
            return MatchSession.Player1Device switch
            {
                Gamepad => TutorialInputKind.Gamepad,
                Touchscreen => TutorialInputKind.Touch,
                _ => TutorialInputKind.Mouse
            };
        }

        /// <summary>O último aparelho usado decide o texto: toque, mouse, teclado ou controle.</summary>
        private void DetectInput()
        {
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
            {
                Input = TutorialInputKind.Touch;
                return;
            }
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !Application.isMobilePlatform)
            {
                Input = TutorialInputKind.Mouse;
                return;
            }
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame && !keyboard.escapeKey.wasPressedThisFrame)
            {
                Input = TutorialInputKind.Keyboard;
                return;
            }
            var gamepad = Gamepad.current;
            if (gamepad != null && (gamepad.buttonSouth.wasPressedThisFrame || gamepad.buttonWest.wasPressedThisFrame ||
                                    gamepad.buttonEast.wasPressedThisFrame || gamepad.buttonNorth.wasPressedThisFrame ||
                                    gamepad.leftStick.ReadValue().sqrMagnitude > 0.25f || gamepad.dpad.ReadValue().sqrMagnitude > 0.25f))
                Input = TutorialInputKind.Gamepad;
        }

        // ---- As lições ----

        // Campo 7 x 11 (centro em 0,0); o jogador fica embaixo e ataca para cima.
        private static readonly Vector2[] FarTop = { new(-2.4f, 3.6f), new(2.4f, 3.6f), new(0f, 4.2f) };

        private void Scene(TeamSide turn, Vector2 ball, Vector2[] bottom, Vector2[] top) =>
            match.TutorialSetup(turn, ball, bottom, top);

        private void HitScene() => Scene(TeamSide.Bottom, new Vector2(0f, -2f),
            new[] { new Vector2(0f, -3.4f), new Vector2(-2.2f, -4f), new Vector2(2.2f, -4f) }, FarTop);

        private void TouchesScene() => Scene(TeamSide.Bottom, new Vector2(0f, -1f),
            new[] { new Vector2(-1.6f, -2.6f), new Vector2(0f, -2.4f), new Vector2(1.6f, -2.6f) }, FarTop);

        private void ShotScene() => Scene(TeamSide.Bottom, new Vector2(0.4f, 2.6f),
            new[] { new Vector2(0.4f, 1.4f), new Vector2(-2.2f, -0.5f), new Vector2(2.2f, -0.5f) },
            new[] { new Vector2(-2.6f, 1.2f), new Vector2(2.6f, 1.2f), new Vector2(0f, -2.5f) });

        private void DefendScene(TeamSide turn) => Scene(turn, new Vector2(-0.4f, -2.6f),
            new[] { new Vector2(-2.6f, -1.2f), new Vector2(2.6f, -1.2f), new Vector2(0f, 2.5f) },
            new[] { new Vector2(-0.4f, -1.4f), new Vector2(2.2f, 0.5f), new Vector2(-2.2f, 0.8f) });

        private void FoulScene() => Scene(TeamSide.Bottom, new Vector2(0f, 0.6f),
            new[] { new Vector2(0f, 1.6f), new Vector2(-2.2f, -1.5f), new Vector2(2.2f, -1.5f) },
            new[] { new Vector2(0.5f, 2.4f), new Vector2(-2.4f, 3.6f), new Vector2(2.4f, 3.6f) });

        private void FreeKick()
        {
            FoulScene();
            match.TutorialFreeKick(match.TeamDiscs(TeamSide.Top)[0], match.TeamDiscs(TeamSide.Bottom)[0]);
        }

        private void PenaltyScene() => Scene(TeamSide.Bottom, FieldLayout.PenaltySpot(TeamSide.Top),
            new[] { new Vector2(0f, FieldLayout.PenaltySpot(TeamSide.Top).y - 1.2f), new Vector2(-2.2f, -1f), new Vector2(2.2f, -1f) },
            new[] { new Vector2(-2.2f, 0f), new Vector2(0f, 0.4f), new Vector2(2.2f, 0f) });

        private void Penalty()
        {
            PenaltyScene();
            match.TutorialPenalty(match.TeamDiscs(TeamSide.Top)[0], match.TeamDiscs(TeamSide.Bottom)[0]);
        }

        private static Step Do(Goal goal, Func<TutorialInputKind, string> popup, string shortText, Action setup) =>
            new() { Goal = goal, Popup = popup, Short = _ => shortText, Setup = setup };

        private static Func<TutorialInputKind, string> Text(string text) => _ => text;

        /// <summary>Explicação da regra e, embaixo, como fazer no aparelho do jogador.</summary>
        private static Func<TutorialInputKind, string> Explain(string rule, Func<TutorialInputKind, string> how) =>
            input => rule + "\n\n" + how(input);

        private List<Step>[] BuildLessons() => new[]
        {
            // 1. Peteleco: escolher o botão, puxar e soltar; depois com força até o campo do adversário.
            new List<Step>
            {
                Do(Goal.Hit, TutorialTexts.Hit, TutorialTexts.ShortHit, HitScene),
                Do(Goal.Far, TutorialTexts.Far, TutorialTexts.ShortFar, HitScene)
            },
            // 2. Vez e toques: até 3 toques seguidos enquanto o time toca na bola.
            new List<Step>
            {
                Do(Goal.ThreeTouches, Text(TutorialTexts.TouchesInfo + "\n\n" + TutorialTexts.ThreeTouches), TutorialTexts.ShortTouches, TouchesScene)
            },
            // 3. Pro Gol!: avisar e fazer o gol (o goleiro é o da IA no Fácil).
            new List<Step>
            {
                Do(Goal.Score, Explain(TutorialTexts.ShotInfo, TutorialTexts.Score), TutorialTexts.ShortScore, ShotScene)
            },
            // 4. Defender: a IA avisa o Pro Gol! e o jogador arruma o goleiro.
            new List<Step>
            {
                Do(Goal.Defend, Explain(TutorialTexts.DefendInfo, TutorialTexts.Defend), TutorialTexts.ShortDefend, () =>
                {
                    DefendScene(TeamSide.Top);
                    match.TutorialCallShot();
                })
            },
            // 5. Falta e tiro livre: o adversário fez falta; posicionar o batedor e chutar.
            new List<Step>
            {
                Do(Goal.FreeKickPlace, Explain(TutorialTexts.FoulInfo, TutorialTexts.FreeKickPlace), TutorialTexts.ShortFreeKickPlace, FreeKick),
                Do(Goal.FreeKickShot, null, TutorialTexts.ShortFreeKickShot, null)
            },
            // 6. Pênalti escondido: o goleiro da IA escolhe primeiro; o jogador escolhe o lado e chuta.
            new List<Step>
            {
                Do(Goal.Penalty, Explain(TutorialTexts.PenaltyInfo, TutorialTexts.Penalty), TutorialTexts.ShortPenalty, Penalty)
            }
        };
    }
}
