using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Deixa o defensor mover o goleiro durante o "Vai chutar": segurar o mouse (ou o dedo) e arrastar para os lados.
    /// Setas ou A/D também movem.
    /// </summary>
    public sealed class GoalkeeperControl : MonoBehaviour
    {
        [SerializeField] private float keyboardSpeed = 4f;
        [Tooltip("Metade da largura do campo. Toques fora dela (no HUD) não mexem o goleiro.")]
        [SerializeField] private float fieldHalfWidth = 3.5f;

        private Goalkeeper[] keepers;
        private Camera worldCamera;

        /// <summary>Lado cujo goleiro está sendo controlado. Nulo desliga o controle.</summary>
        public TeamSide? ControlledSide { get; set; }

        private void Start()
        {
            keepers = FindObjectsByType<Goalkeeper>();
        }

        private void Update()
        {
            if (ControlledSide == null || keepers == null || Time.timeScale <= 0f) return;

            var keeper = FindKeeper(ControlledSide.Value);
            if (keeper == null) return;

            // Lê o técnico (Actor) do defensor, ou o mouse/teclado direto quando o time não tem técnico.
            var input = MatchInput.For(ControlledSide.Value);
            if (input.PointerHeld && input.TryGetPointer(out var screen) && !UiPointer.IsOverUi())
            {
                worldCamera = WorldCamera.Resolve(worldCamera);
                if (worldCamera != null)
                {
                    float x = WorldCamera.ScreenToWorld(worldCamera, screen).x;
                    if (Mathf.Abs(x) <= fieldHalfWidth)
                    {
                        keeper.SetTargetX(x);
                        return;
                    }
                }
            }

            float axis = input.KeeperAxis;
            if (axis != 0f) keeper.SetTargetX(keeper.transform.position.x + axis * keyboardSpeed * Time.deltaTime);
        }

        private Goalkeeper FindKeeper(TeamSide side)
        {
            foreach (var keeper in keepers)
                if (keeper != null && keeper.isActiveAndEnabled && keeper.Side == side) return keeper;
            return null;
        }
    }
}
