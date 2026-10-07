using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FutebolDeBotao
{
    /// <summary>
    /// Mira por arrasto, igual para mouse e toque: toca no botão, arrasta para trás e solta.
    /// A distância do arrasto define a força.
    /// </summary>
    public sealed class AimController : MonoBehaviour
    {
        [SerializeField] private PhysicsTuning tuning;
        [SerializeField] private MotionMonitor motionMonitor;
        [SerializeField] private Camera worldCamera;

        private Ball ball;
        private Vector2 pointerWorld;

        public bool IsAiming { get; private set; }
        public Disc SelectedDisc { get; private set; }
        /// <summary>Direção em que o botão vai sair (normalizada).</summary>
        public Vector2 Direction { get; private set; }
        /// <summary>Força de 0 a 1.</summary>
        public float Power01 { get; private set; }
        public bool HasValidAim => IsAiming && Power01 > 0f;

        /// <summary>Desligado, a mira ignora o ponteiro (fora da vez de mirar).</summary>
        public bool InputEnabled { get; set; } = true;
        /// <summary>Filtro de quais botões podem ser escolhidos agora (ex.: só o time da vez). Nulo libera todos.</summary>
        public Func<Disc, bool> CanSelect { get; set; }

        /// <summary>Peteleco disparado: (botão, impulso).</summary>
        public event Action<Disc, Vector2> Flicked;

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
            var pointer = Pointer.current;
            worldCamera = WorldCamera.Resolve(worldCamera);
            if (pointer == null || worldCamera == null || tuning == null) return;

            if (!InputEnabled)
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

            if (pointer.press.wasPressedThisFrame) TryBegin();

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
            Vector2 pull = SelectedDisc.Body.position - pointerWorld;
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

            var disc = SelectedDisc;
            var impulse = Direction * (Power01 * tuning.maxImpulse);

            if (ball != null) ball.BeginShot();
            disc.Flick(impulse);
            if (motionMonitor != null) motionMonitor.BeginWatching();

            Cancel();
            Flicked?.Invoke(disc, impulse);
        }

        public void Cancel()
        {
            IsAiming = false;
            SelectedDisc = null;
            Direction = Vector2.zero;
            Power01 = 0f;
        }
    }
}
