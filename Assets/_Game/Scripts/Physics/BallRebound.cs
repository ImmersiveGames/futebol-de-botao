using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// A bola rebate nos botões parados como numa parede: o botão não sai do lugar e a bola volta com o quique
    /// (bounciness). Não vale para o botão do peteleco, que empurra a bola normalmente, nem para botão em movimento.
    /// Usado no jogo (Ball) e na mesa simulada da IA (ShotSimulator).
    /// </summary>
    public static class BallRebound
    {
        /// <summary>Acima dessa velocidade o botão conta como em movimento e a batida fica com a física normal.</summary>
        public const float RestingDiscSpeed = 0.3f;

        /// <summary>
        /// Refaz a batida: <paramref name="ballBefore"/> e <paramref name="discBefore"/> são as velocidades antes do
        /// passo de física. Devolve false se o botão estava andando ou a bola não vinha na direção dele.
        /// </summary>
        public static bool Apply(Rigidbody2D ball, Vector2 ballBefore, Rigidbody2D disc, Vector2 discBefore, float bounciness)
        {
            if (discBefore.sqrMagnitude > RestingDiscSpeed * RestingDiscSpeed) return false;

            Vector2 offset = ball.position - disc.position;
            if (offset.sqrMagnitude < 0.000001f) return false;
            Vector2 normal = offset.normalized;

            float approach = Vector2.Dot(ballBefore, normal);
            if (approach >= 0f) return false;

            ball.linearVelocity = ballBefore - normal * ((1f + bounciness) * approach);
            disc.linearVelocity = discBefore;
            disc.angularVelocity = 0f;
            return true;
        }
    }
}
