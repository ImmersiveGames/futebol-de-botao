using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>
    /// Texto (ou ícone) de um botão vestido: fica no meio da face desenhada (um pouco acima do centro no botão solto, no centro
    /// quando apertado), escurece enquanto o dedo aperta e fica branco no botão desligado.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ButtonSkinText : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private const float PressedDarken = 0.75f;

        [SerializeField] private Button button;
        [SerializeField] private Graphic label;
        [SerializeField] private Color color = Color.white;
        [Tooltip("Quanto o texto sobe (unidades da UI) com o botão solto.")]
        [SerializeField] private float raise;
        [SerializeField, HideInInspector] private bool hasBase;
        [SerializeField, HideInInspector] private Vector2 basePosition;

        private bool pressed;

        public void Configure(Button target, Graphic text, Color textColor, float raiseUnits)
        {
            button = target;
            if (label != text) hasBase = false;
            label = text;
            color = textColor;
            raise = raiseUnits;
            if (label != null && !hasBase)
            {
                basePosition = label.rectTransform.anchoredPosition;
                hasBase = true;
            }
            Refresh();
        }

        public void OnPointerDown(PointerEventData eventData) => pressed = true;
        public void OnPointerUp(PointerEventData eventData) => pressed = false;
        public void OnPointerExit(PointerEventData eventData) => pressed = false;

        private void OnDisable() => pressed = false;

        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            if (label == null) return;
            bool enabledButton = button == null || button.IsInteractable();
            bool down = pressed && enabledButton;
            if (hasBase) label.rectTransform.anchoredPosition = basePosition + new Vector2(0f, down ? 0f : raise);
            label.color = !enabledButton ? Color.white
                : down ? new Color(color.r * PressedDarken, color.g * PressedDarken, color.b * PressedDarken, color.a)
                : color;
        }
    }
}
