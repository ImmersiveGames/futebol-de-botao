using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Empurra bola e botões de volta para o campo quando chegam perto de uma parede, para nada ficar
    /// preso ou parado encostado nela. Só age com a mesa em movimento. O quique forte fica no WallElastic.
    /// </summary>
    [RequireComponent(typeof(MotionMonitor))]
    public sealed class WallRepulsion : MonoBehaviour
    {
        [SerializeField] private PhysicsTuning tuning;

        private readonly List<Collider2D> walls = new();
        private readonly List<Collider2D> bodies = new();
        private MotionMonitor monitor;

        public void Configure(PhysicsTuning physicsTuning) => tuning = physicsTuning;

        private void Start()
        {
            monitor = GetComponent<MotionMonitor>();
            RefreshWalls();

            bodies.Clear();
            foreach (var disc in FindObjectsByType<Disc>()) bodies.Add(disc.GetComponent<Collider2D>());
            foreach (var ball in FindObjectsByType<Ball>()) bodies.Add(ball.GetComponent<Collider2D>());
        }

        /// <summary>Relê as paredes ligadas (as traves do gol sem goleiro entram e saem com a opção).</summary>
        public void RefreshWalls()
        {
            walls.Clear();
            foreach (var wall in FindObjectsByType<Wall>())
                if (wall.PushesBack && wall.TryGetComponent(out Collider2D wallCollider)) walls.Add(wallCollider);
        }

        private void FixedUpdate()
        {
            if (tuning == null || !monitor.IsMoving) return;

            float range = tuning.wallPushDistance;
            float acceleration = tuning.wallPushAcceleration;
            if (range <= 0f || acceleration <= 0f) return;

            foreach (var bodyCollider in bodies)
            {
                if (bodyCollider == null) continue;
                var body = bodyCollider.attachedRigidbody;
                Vector2 push = Vector2.zero;

                foreach (var wall in walls)
                {
                    var distance = Physics2D.Distance(bodyCollider, wall);
                    if (!distance.isValid || distance.distance >= range) continue;

                    // Do ponto mais próximo da parede para o centro do corpo: sempre aponta para fora da parede.
                    Vector2 away = body.worldCenterOfMass - distance.pointB;
                    if (away.sqrMagnitude < 0.0001f) continue;

                    float strength = 1f - Mathf.Max(distance.distance, 0f) / range;
                    push += away.normalized * strength;
                }

                if (push != Vector2.zero) body.AddForce(push * (acceleration * body.mass));
            }
        }
    }
}
