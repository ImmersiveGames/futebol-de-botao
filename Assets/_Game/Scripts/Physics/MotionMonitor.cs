using System;
using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Observa todos os corpos da mesa e avisa quando tudo parou (fim do movimento do turno).
    /// </summary>
    public sealed class MotionMonitor : MonoBehaviour
    {
        [SerializeField] private PhysicsTuning tuning;

        private readonly List<Rigidbody2D> bodies = new();
        private float stillTimer;
        private bool watching;

        public bool IsMoving => watching;
        public event Action Settled;

        public void Configure(PhysicsTuning physicsTuning) => tuning = physicsTuning;

        private void Start()
        {
            bodies.Clear();
            foreach (var disc in FindObjectsByType<Disc>(FindObjectsSortMode.None)) bodies.Add(disc.GetComponent<Rigidbody2D>());
            foreach (var ball in FindObjectsByType<Ball>(FindObjectsSortMode.None)) bodies.Add(ball.GetComponent<Rigidbody2D>());
        }

        /// <summary>Chame logo depois de um peteleco.</summary>
        public void BeginWatching()
        {
            watching = true;
            stillTimer = 0f;
        }

        public void StopWatching() => watching = false;

        private void FixedUpdate()
        {
            if (!watching) return;

            float restSpeed = tuning != null ? tuning.restSpeed : 0.05f;
            float restTime = tuning != null ? tuning.restTime : 0.3f;

            bool anyMoving = false;
            foreach (var body in bodies)
            {
                if (body != null && body.linearVelocity.sqrMagnitude > restSpeed * restSpeed)
                {
                    anyMoving = true;
                    break;
                }
            }

            stillTimer = anyMoving ? 0f : stillTimer + Time.fixedDeltaTime;
            if (stillTimer < restTime) return;

            foreach (var body in bodies)
            {
                if (body == null) continue;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }

            watching = false;
            Settled?.Invoke();
        }
    }
}
