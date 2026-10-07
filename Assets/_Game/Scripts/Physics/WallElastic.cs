using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Paredes como elástico: devolvem bola e botões para o campo com um pouco de ganho.
    /// Vale para corpos redondos (bola e botões) e ignora as redes do gol.
    /// Quem fica encostado é empurrado de volta pelo WallRepulsion.
    /// </summary>
    public static class WallElastic
    {
        public static void OnEnter(Rigidbody2D body, Collision2D collision, PhysicsTuning tuning)
        {
            if (tuning == null || !TryGetNormal(body, collision, out var normal)) return;

            var velocity = body.linearVelocity;
            float outward = Vector2.Dot(velocity, normal);
            float desired = Mathf.Max(outward * tuning.wallBounceBoost, tuning.wallMinBounceSpeed);
            if (desired <= outward) return;

            body.linearVelocity = velocity + normal * (desired - outward);
        }

        private static bool TryGetNormal(Rigidbody2D body, Collision2D collision, out Vector2 normal)
        {
            normal = Vector2.zero;
            var wall = collision.collider.GetComponent<Wall>();
            if (wall == null || !wall.PushesBack || collision.contactCount == 0) return false;

            // Para um corpo redondo, a normal aponta do ponto de contato para o centro.
            var away = body.position - collision.GetContact(0).point;
            if (away.sqrMagnitude < 0.0001f) return false;

            normal = away.normalized;
            return true;
        }
    }
}
