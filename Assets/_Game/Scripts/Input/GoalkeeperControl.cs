using UnityEngine;
using UnityEngine.InputSystem;

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
            if (ControlledSide == null || keepers == null) return;

            var keeper = FindKeeper(ControlledSide.Value);
            if (keeper == null) return;

            var pointer = Pointer.current;
            if (pointer != null && pointer.press.isPressed)
            {
                worldCamera = WorldCamera.Resolve(worldCamera);
                if (worldCamera != null)
                {
                    float x = WorldCamera.ScreenToWorld(worldCamera, pointer.position.ReadValue()).x;
                    if (Mathf.Abs(x) <= fieldHalfWidth)
                    {
                        keeper.SetTargetX(x);
                        return;
                    }
                }
            }

            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            float axis = 0f;
            if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) axis -= 1f;
            if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) axis += 1f;
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
