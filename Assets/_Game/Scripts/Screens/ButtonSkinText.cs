using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>Cor do texto de um botão vestido: escurece enquanto o dedo aperta; no botão desligado fica branca.</summary>
    [DisallowMultipleComponent]
    public sealed class ButtonSkinText : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private const float PressedDarken = 0.75f;

        [SerializeField] private Button button;
        [SerializeField] private Text label;
        [SerializeField] private Color color = Color.white;

        private bool pressed;

        public void Configure(Button target, Text text, Color textColor)
        {
            button = target;
            label = text;
            color = textColor;
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
            label.color = !enabledButton ? Color.white
                : pressed ? new Color(color.r * PressedDarken, color.g * PressedDarken, color.b * PressedDarken, color.a)
                : color;
        }
    }
}
