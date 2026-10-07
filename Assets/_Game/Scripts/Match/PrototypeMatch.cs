using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FutebolDeBotao
{
    /// <summary>
    /// Partida mínima da fase 1: conta gols, anula gol depois de parede e reposiciona tudo.
    /// As regras de turno completas chegam na fase 2.
    /// </summary>
    public sealed class PrototypeMatch : MonoBehaviour
    {
        [SerializeField] private bool goalAfterWallIsValid;
        [SerializeField] private float resetDelay = 1.2f;

        private Ball ball;
        private Disc[] discs;
        private Goalkeeper[] goalkeepers;
        private int bottomScore;
        private int topScore;
        private string message = "Arraste um botão para trás e solte.";
        private bool resetting;

        private void Start()
        {
            ball = FindAnyObjectByType<Ball>();
            discs = FindObjectsByType<Disc>(FindObjectsSortMode.None);
            goalkeepers = FindObjectsByType<Goalkeeper>(FindObjectsSortMode.None);
            foreach (var goal in FindObjectsByType<GoalTrigger>(FindObjectsSortMode.None)) goal.BallEntered += OnBallEntered;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) ResetAll();
        }

        private void OnBallEntered(GoalTrigger goal, Ball enteredBall)
        {
            if (resetting) return;

            bool valid = goalAfterWallIsValid || !enteredBall.TouchedWallSinceShot;
            if (valid)
            {
                if (goal.DefendingSide == TeamSide.Bottom) topScore++;
                else bottomScore++;
                message = "GOL!";
                StartCoroutine(ResetAfterDelay(null));
            }
            else
            {
                message = "Gol anulado: a bola tocou a parede.";
                StartCoroutine(ResetAfterDelay(goal.DefendingSide));
            }
        }

        private IEnumerator ResetAfterDelay(TeamSide? annulledFor)
        {
            resetting = true;
            yield return new WaitForSeconds(resetDelay);

            if (annulledFor.HasValue)
            {
                // Gol anulado: bola com o goleiro de quem defendeu.
                var keeper = FindKeeper(annulledFor.Value);
                if (keeper != null)
                {
                    Vector2 towardCenter = annulledFor.Value == TeamSide.Bottom ? Vector2.up : Vector2.down;
                    ball.ResetTo((Vector2)keeper.transform.position + towardCenter * 0.8f);
                }
                else
                {
                    ball.ResetToStart();
                }
            }
            else
            {
                ResetAll();
            }

            resetting = false;
        }

        private void ResetAll()
        {
            foreach (var disc in discs) disc.ResetToStart();
            ball.ResetToStart();
            message = "Arraste um botão para trás e solte.";
        }

        private Goalkeeper FindKeeper(TeamSide side)
        {
            foreach (var keeper in goalkeepers)
                if (keeper.Side == side) return keeper;
            return null;
        }

        private void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(16, 12, 600, 32), $"Azul {bottomScore} x {topScore} Vermelho", style);
            style.fontSize = 16;
            style.fontStyle = FontStyle.Normal;
            GUI.Label(new Rect(16, 44, 600, 26), message, style);
            GUI.Label(new Rect(16, 68, 600, 26), "R: reiniciar  |  Botão direito: cancelar mira", style);
        }
    }
}
