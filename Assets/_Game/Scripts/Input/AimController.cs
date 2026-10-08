using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FutebolDeBotao
{
    /// <summary>
    /// Mira por arrasto, igual para mouse e toque: toca no botão, arrasta para trás e solta.
    /// A distância do arrasto define a força. No modo chute na bola (tiro de meta) o arrasto é feito na própria bola.
    /// </summary>
    public sealed class AimController : MonoBehaviour
    {
        [SerializeField] private PhysicsTuning tuning;
        [SerializeField] private MotionMonitor motionMonitor;
        [SerializeField] private Camera worldCamera;

        private Ball ball;
        private Vector2 pointerWorld;
        private bool scripted;

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
        /// <summary>Filtro de quais botões podem ser escolhidos agora (ex.: só o time da vez). Nulo libera todos.</summary>
        public Func<Disc, bool> CanSelect { get; set; }
        /// <summary>Ligado, só a bola pode ser chutada (tiro de meta); botões ficam bloqueados.</summary>
        public bool BallKickMode { get; set; }

        public PhysicsTuning Tuning => tuning;

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

            var pointer = Pointer.current;
            worldCamera = WorldCamera.Resolve(worldCamera);
            if (pointer == null || worldCamera == null || tuning == null) return;

            // Na pausa do framework (Time.timeScale = 0) a mira não lê o ponteiro.
            if (!InputEnabled || Time.timeScale <= 0f)
            {
                Cancel();
                return;
            }

            if (motionMonitor != null && motionMonitor.IsMoving)
            {
                Cancel();
                return;
            }

            pointerWorld = WorldCamera.ScreenToWorld(worldCamera, pointer.position.ReadValue());

            if (pointer.press.wasPressedThisFrame && !UiPointer.IsOverUi()) TryBegin();

            if (!IsAiming) return;

            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                Cancel();
                return;
            }

            UpdateAim();

            if (pointer.press.wasReleasedThisFrame) Release();
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
            scripted = false;
            IsAiming = false;
            IsBallKick = false;
            SelectedDisc = null;
            Direction = Vector2.zero;
            Power01 = 0f;
        }
    }
}
