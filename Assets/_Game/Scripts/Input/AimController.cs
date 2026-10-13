using System;
using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Mira por arrasto, igual para mouse e toque: toca no botão, arrasta para trás e solta.
    /// A distância do arrasto define a força. No modo chute na bola (tiro de meta) o arrasto é feito na própria bola.
    /// Teclado e controle: um cursor anda entre os botões (começa no mais perto da bola), Escolher abre a mira apontando
    /// para a bola com força média, girar/apontar e a força ajustam, Chutar solta e Voltar volta um passo.
    /// </summary>
    public sealed class AimController : MonoBehaviour
    {
        [SerializeField] private PhysicsTuning tuning;
        [SerializeField] private MotionMonitor motionMonitor;
        [SerializeField] private Camera worldCamera;

        // Teclado/controle.
        private const float CursorThreshold = 0.5f;
        private const float CursorRepeatDelay = 0.35f;
        private const float CursorRepeatRate = 0.18f;
        private const float StickThreshold = 0.5f;
        private const float RotateSlowSpeed = 25f;
        private const float RotateFastSpeed = 140f;
        private const float RotateRampSeconds = 0.8f;
        private const float PowerSpeed = 0.8f;
        private const float StartPower = 0.5f;

        private Ball ball;
        private Vector2 pointerWorld;
        private bool scripted;

        private readonly List<Disc> candidates = new();
        private bool ready;
        private bool padAiming;
        private int cursorDirection;
        private float cursorRepeatAt;
        private float rotateHeld;

        public bool IsAiming { get; private set; }
        public Disc SelectedDisc { get; private set; }
        /// <summary>A mira atual é na bola (tiro de meta), não num botão.</summary>
        public bool IsBallKick { get; private set; }
        /// <summary>Centro e raio de quem vai receber o peteleco (botão ou bola).</summary>
        public Vector2 AimOrigin => IsBallKick ? ball.Body.position : SelectedDisc.Body.position;
        public float AimRadius => IsBallKick ? ball.Radius : SelectedDisc.Radius;
        /// <summary>Direção em que o botão vai sair (normalizada).</summary>
        public Vector2 Direction { get; private set; }
        /// <summary>Força de 0 a 1.</summary>
        public float Power01 { get; private set; }
        public bool HasValidAim => IsAiming && Power01 > 0f;

        /// <summary>Desligado, a mira ignora o ponteiro (fora da vez de mirar).</summary>
        public bool InputEnabled { get; set; } = true;
        /// <summary>Time cujo input a mira lê (o da vez).</summary>
        public TeamSide InputSide { get; set; }
        /// <summary>Filtro de quais botões podem ser escolhidos agora (ex.: só o time da vez). Nulo libera todos.</summary>
        public Func<Disc, bool> CanSelect { get; set; }
        /// <summary>Ligado, só a bola pode ser chutada (tiro de meta); botões ficam bloqueados.</summary>
        public bool BallKickMode { get; set; }

        public PhysicsTuning Tuning => tuning;

        /// <summary>Último comando veio do teclado/controle (e não do mouse/toque): o brilho mostra só o cursor.</summary>
        public static bool PadMode
        {
            get => FutebolDeBotao.PadMode.Active;
            private set => FutebolDeBotao.PadMode.Active = value;
        }
        /// <summary>Botão onde está o cursor do teclado/controle (nulo fora da vez).</summary>
        public Disc CursorDisc { get; private set; }

        /// <summary>Peteleco disparado: (botão, impulso).</summary>
        public event Action<Disc, Vector2> Flicked;
        /// <summary>Chute direto na bola disparado: (impulso).</summary>
        public event Action<Vector2> BallKicked;

        public void Configure(PhysicsTuning physicsTuning, MotionMonitor monitor)
        {
            tuning = physicsTuning;
            motionMonitor = monitor;
        }

        private void Start()
        {
            ball = FindAnyObjectByType<Ball>();
        }

        private void Update()
        {
            // A IA está mirando: o ponteiro não interfere.
            if (scripted) return;

            worldCamera = WorldCamera.Resolve(worldCamera);
            if (worldCamera == null || tuning == null) return;

            // Na pausa do framework (Time.timeScale = 0) a mira não lê o ponteiro.
            if (!InputEnabled || Time.timeScale <= 0f)
            {
                Cancel();
                ready = false;
                return;
            }

            if (motionMonitor != null && motionMonitor.IsMoving)
            {
                Cancel();
                ready = false;
                return;
            }

            // Lê o técnico (Actor) do time da vez, ou os dispositivos direto quando o time não tem técnico.
            var input = MatchInput.For(InputSide);

            // Começo da vez: o cursor vai para o botão mais perto da bola. Nada mais é lido neste quadro, para a tecla
            // que acabou de fechar a fase anterior (Pronto, por exemplo) não escolher um botão sem querer.
            if (!ready)
            {
                ready = true;
                ResetCursor();
                return;
            }

            if (input.TryGetPointer(out var screen))
            {
                pointerWorld = WorldCamera.ScreenToWorld(worldCamera, screen);
                if (input.PointerPressedThisFrame && !UiPointer.IsOverUi())
                {
                    PadMode = false;
                    if (padAiming) Cancel();
                    TryBegin();
                }
            }

            if (IsAiming && !padAiming)
            {
                if (input.BackPressedThisFrame)
                {
                    Cancel();
                    return;
                }

                UpdateAim();

                if (input.PointerReleasedThisFrame) Release();
                return;
            }

            UpdatePad(input);
        }

        // ---- Teclado e controle ----

        private void UpdatePad(ITeamInput input)
        {
            if (!padAiming && BallKickMode)
            {
                // Tiro de meta: só existe a bola, então a mira já abre nela (qualquer comando do teclado/controle liga).
                bool anyPad = input.SelectPressedThisFrame || input.AimStick.magnitude >= StickThreshold ||
                              Mathf.Abs(input.AimRotateAxis) > 0.1f || Mathf.Abs(input.PowerAxis) > 0.1f;
                if (anyPad) PadMode = true;
                if (!PadMode) return;
                BeginPad();
                return;
            }

            if (!padAiming)
            {
                UpdateCursor(input);
                if (input.SelectPressedThisFrame)
                {
                    PadMode = true;
                    BeginPad();
                }
                return;
            }

            if (input.BackPressedThisFrame && !IsBallKick)
            {
                // Volta um passo: a mira fecha e o cursor fica no botão que estava escolhido.
                // No tiro de meta não há outra coisa para escolher, então Voltar não fecha a mira.
                PadMode = true;
                Cancel();
                return;
            }

            float sign = MatchInput.ScreenSign(InputSide);
            Vector2 stick = input.AimStick;
            if (stick.magnitude >= StickThreshold)
            {
                // O analógico aponta e a mira fica onde parou quando ele volta ao centro.
                PadMode = true;
                Direction = (stick * sign).normalized;
            }

            float rotate = input.AimRotateAxis;
            if (Mathf.Abs(rotate) > 0.1f)
            {
                // Começa devagar (ajuste fino) e acelera segurando. Direita leva a ponta da mira para a direita de quem joga.
                PadMode = true;
                rotateHeld += Time.deltaTime;
                float speed = Mathf.Lerp(RotateSlowSpeed, RotateFastSpeed, rotateHeld / RotateRampSeconds);
                float degrees = -rotate * speed * MatchInput.TurnSign(InputSide) * Time.deltaTime;
                Direction = Rotate(Direction, degrees);
            }
            else rotateHeld = 0f;

            float power = input.PowerAxis;
            if (Mathf.Abs(power) > 0.1f)
            {
                PadMode = true;
                Power01 = Mathf.Clamp(Power01 + power * PowerSpeed * Time.deltaTime, MinPadPower, 1f);
            }

            if (input.KickPressedThisFrame)
            {
                PadMode = true;
                Release();
            }
        }

        /// <summary>Força mínima do teclado/controle: a mesma do menor arrasto que conta como chute.</summary>
        private float MinPadPower => tuning.maxDragDistance > 0f ? tuning.minDragDistance / tuning.maxDragDistance : 0.05f;

        /// <summary>Abre a mira no botão do cursor (ou na bola, no tiro de meta), apontando para a bola, com força média.</summary>
        private void BeginPad()
        {
            if (BallKickMode)
            {
                if (ball == null) return;
                IsBallKick = true;
                Direction = FieldLayout.AttackDirection(InputSide);
            }
            else
            {
                if (CursorDisc == null || !IsSelectable(CursorDisc)) ResetCursor();
                if (CursorDisc == null) return;
                SelectedDisc = CursorDisc;
                Vector2 toBall = ball != null ? ball.Body.position - SelectedDisc.Body.position : Vector2.zero;
                Direction = toBall.sqrMagnitude > 0.0001f ? toBall.normalized : FieldLayout.AttackDirection(InputSide);
            }

            IsAiming = true;
            padAiming = true;
            Power01 = StartPower;
            rotateHeld = 0f;
        }

        private void UpdateCursor(ITeamInput input)
        {
            float axis = input.SideAxis;
            int direction = axis >= CursorThreshold ? 1 : axis <= -CursorThreshold ? -1 : 0;
            if (direction == 0)
            {
                cursorDirection = 0;
                return;
            }

            bool step = direction != cursorDirection || Time.unscaledTime >= cursorRepeatAt;
            if (!step) return;
            cursorRepeatAt = Time.unscaledTime + (direction != cursorDirection ? CursorRepeatDelay : CursorRepeatRate);
            cursorDirection = direction;

            // O primeiro toque depois de usar o mouse só mostra o cursor.
            if (!PadMode || CursorDisc == null || !IsSelectable(CursorDisc))
            {
                PadMode = true;
                if (CursorDisc == null || !IsSelectable(CursorDisc)) ResetCursor();
                return;
            }

            MoveCursor(direction);
        }

        /// <summary>Próximo botão para a direita (1) ou esquerda (-1) na tela de quem joga; no fim da fila volta para o outro lado.</summary>
        private void MoveCursor(int direction)
        {
            CollectCandidates();
            if (candidates.Count == 0) return;
            float sign = MatchInput.ScreenSign(InputSide);
            candidates.Sort((a, b) =>
            {
                int byX = (a.Body.position.x * sign).CompareTo(b.Body.position.x * sign);
                return byX != 0 ? byX : (a.Body.position.y * sign).CompareTo(b.Body.position.y * sign);
            });
            int index = candidates.IndexOf(CursorDisc);
            index = index < 0 ? 0 : (index + direction + candidates.Count) % candidates.Count;
            CursorDisc = candidates[index];
        }

        /// <summary>Cursor no botão mais perto da bola entre os que podem jogar agora.</summary>
        private void ResetCursor()
        {
            CursorDisc = null;
            if (BallKickMode) return;
            CollectCandidates();
            float best = float.MaxValue;
            Vector2 target = ball != null ? ball.Body.position : Vector2.zero;
            foreach (var disc in candidates)
            {
                float distance = (disc.Body.position - target).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    CursorDisc = disc;
                }
            }
        }

        private void CollectCandidates()
        {
            candidates.Clear();
            foreach (var disc in FindObjectsByType<Disc>())
                if (IsSelectable(disc)) candidates.Add(disc);
        }

        private bool IsSelectable(Disc disc) =>
            disc != null && disc.isActiveAndEnabled && (CanSelect == null || CanSelect(disc));

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        private void TryBegin()
        {
            if (BallKickMode)
            {
                // Área de toque um pouco maior que a bola, que é pequena.
                if (ball == null || Vector2.Distance(pointerWorld, ball.Body.position) > ball.Radius + 0.3f) return;
                IsBallKick = true;
                IsAiming = true;
                UpdateAim();
                return;
            }

            var hit = Physics2D.OverlapPoint(pointerWorld);
            var disc = hit != null ? hit.GetComponent<Disc>() : null;
            if (disc == null) return;
            if (CanSelect != null && !CanSelect(disc)) return;

            SelectedDisc = disc;
            IsAiming = true;
            UpdateAim();
        }

        private void UpdateAim()
        {
            // Puxar para trás: o botão sai no sentido oposto ao arrasto.
            Vector2 pull = AimOrigin - pointerWorld;
            float distance = Mathf.Min(pull.magnitude, tuning.maxDragDistance);

            if (distance < tuning.minDragDistance)
            {
                Power01 = 0f;
                Direction = Vector2.zero;
                return;
            }

            Direction = pull.normalized;
            Power01 = distance / tuning.maxDragDistance;
        }

        private void Release()
        {
            if (!HasValidAim)
            {
                Cancel();
                return;
            }

            if (IsBallKick)
            {
                var kick = Direction * (Power01 * tuning.ballKickMaxImpulse);
                ball.BeginShot();
                ball.Body.AddForce(kick, ForceMode2D.Impulse);
                if (motionMonitor != null) motionMonitor.BeginWatching();
                Cancel();
                BallKicked?.Invoke(kick);
                return;
            }

            var disc = SelectedDisc;
            var impulse = Direction * (Power01 * tuning.maxImpulse);

            if (ball != null) ball.BeginShot();
            disc.Flick(impulse);
            if (motionMonitor != null) motionMonitor.BeginWatching();

            Cancel();
            Flicked?.Invoke(disc, impulse);
        }

        // ---- Mira da IA ----

        /// <summary>A IA começa a mirar com <paramref name="disc"/> (nulo = chute direto na bola). A mira aparece igual à do jogador.</summary>
        public void BeginScripted(Disc disc)
        {
            Cancel();
            if (disc == null && ball == null) return;
            scripted = true;
            IsAiming = true;
            IsBallKick = disc == null;
            SelectedDisc = disc;
        }

        /// <summary>Atualiza a mira da IA: direção de saída e força de 0 a 1.</summary>
        public void SetScripted(Vector2 direction, float power01)
        {
            if (!scripted) return;
            Direction = direction.normalized;
            Power01 = Mathf.Clamp01(power01);
        }

        /// <summary>A IA solta: o peteleco sai pelo mesmo caminho do jogador.</summary>
        public void ReleaseScripted()
        {
            if (!scripted) return;
            scripted = false;
            Release();
        }

        public void Cancel()
        {
            // Depois de mirar pelo teclado/controle, o cursor fica no botão que estava escolhido.
            if (padAiming && SelectedDisc != null) CursorDisc = SelectedDisc;
            padAiming = false;
            scripted = false;
            IsAiming = false;
            IsBallKick = false;
            SelectedDisc = null;
            Direction = Vector2.zero;
            Power01 = 0f;
        }
    }
}
