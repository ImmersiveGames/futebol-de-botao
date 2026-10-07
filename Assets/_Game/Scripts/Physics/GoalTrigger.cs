using System;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>Trigger atrás da linha do gol. Avisa quando a bola entra.</summary>
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

        private void OnTriggerEnter2D(Collider2D other)
        {
            var ball = other.GetComponent<Ball>();
            if (ball != null) BallEntered?.Invoke(this, ball);
        }
    }
}
