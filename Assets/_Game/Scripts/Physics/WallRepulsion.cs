using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Empurra bola e botões de volta para o campo quando chegam perto de uma parede, para nada ficar
    /// preso ou parado encostado nela. Só age com a mesa em movimento. O quique forte fica no WallElastic.
    /// No canto (a bola perto de duas paredes que não são paralelas), o empurrão alcança mais longe e é mais forte
    /// (cornerPushMultiplier), como um rebatedor invisível e suave.
    /// </summary>
    [RequireComponent(typeof(MotionMonitor))]
    public sealed class WallRepulsion : MonoBehaviour
    {
        [SerializeField] private PhysicsTuning tuning;

        private readonly List<Collider2D> walls = new();
        private readonly List<Collider2D> bodies = new();
        private readonly List<(Vector2 away, float distance)> near = new();
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

            float corner = Mathf.Max(1f, tuning.cornerPushMultiplier);
            foreach (var bodyCollider in bodies)
            {
                if (bodyCollider == null) continue;
                var body = bodyCollider.attachedRigidbody;
                bool isBall = bodyCollider.TryGetComponent<Ball>(out _);
                float reach = isBall ? range * corner : range;

                near.Clear();
                foreach (var wall in walls)
                {
                    var distance = Physics2D.Distance(bodyCollider, wall);
                    if (!distance.isValid || distance.distance >= reach) continue;

                    // Do ponto mais próximo da parede para o centro do corpo: sempre aponta para fora da parede.
                    Vector2 away = body.worldCenterOfMass - distance.pointB;
                    if (away.sqrMagnitude < 0.0001f) continue;
                    near.Add((away.normalized, Mathf.Max(distance.distance, 0f)));
                }

                // Canto: só a bola, com duas paredes de direções diferentes por perto.
                bool inCorner = false;
                if (isBall)
                    for (int i = 0; i < near.Count && !inCorner; i++)
                    for (int j = i + 1; j < near.Count; j++)
                        if (Vector2.Dot(near[i].away, near[j].away) < 0.5f) { inCorner = true; break; }

                float usedRange = inCorner ? range * corner : range;
                Vector2 push = Vector2.zero;
                foreach (var (away, distance) in near)
                    if (distance < usedRange) push += away * (1f - distance / usedRange);

                if (push != Vector2.zero) body.AddForce(push * ((inCorner ? acceleration * corner : acceleration) * body.mass));
            }
        }
    }
}
