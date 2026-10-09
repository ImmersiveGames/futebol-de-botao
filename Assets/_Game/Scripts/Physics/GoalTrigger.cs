using System;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>Trigger atrás da linha do gol. Avisa quando a bola passa inteira da linha de fundo.</summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class GoalTrigger : MonoBehaviour
    {
        [Tooltip("Lado que defende este gol.")]
        [SerializeField] private TeamSide defendingSide;

        public TeamSide DefendingSide => defendingSide;

        /// <summary>Gol que entrou: (trigger, bola).</summary>
        public event Action<GoalTrigger, Ball> BallEntered;

        public void Configure(TeamSide defending)
        {
            defendingSide = defending;
        }

        private void Reset()
        {
            GetComponent<BoxCollider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other) => Check(other);

        // A bola encosta no trigger com metade dela ainda no campo: o gol só sai quando ela passa inteira da linha.
        private void OnTriggerStay2D(Collider2D other) => Check(other);

        private void Check(Collider2D other)
        {
            var ball = other.GetComponent<Ball>();
            if (ball == null || !FieldLayout.BallFullyInGoal(ball.Body.position, ball.Radius)) return;
            BallEntered?.Invoke(this, ball);
        }
    }
}
