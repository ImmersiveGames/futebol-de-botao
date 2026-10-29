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
        [Tooltip("O que sobe com a face: o texto, ou o ícone (que pode ter mais de uma imagem).")]
        [SerializeField] private RectTransform content;
        [SerializeField] private Graphic[] graphics = new Graphic[0];
        [SerializeField] private Color color = Color.white;
        [Tooltip("Quanto o texto sobe (unidades da UI) com o botão solto.")]
        [SerializeField] private float raise;
        [SerializeField, HideInInspector] private bool hasBase;
        [SerializeField, HideInInspector] private Vector2 basePosition;

        private bool pressed;

        public void Configure(Button target, RectTransform shown, Color textColor, float raiseUnits)
        {
            button = target;
            if (content != shown) hasBase = false;
            content = shown;
            graphics = shown != null ? shown.GetComponentsInChildren<Graphic>(true) : new Graphic[0];
            color = textColor;
            raise = raiseUnits;
            if (content != null && !hasBase)
            {
                basePosition = content.anchoredPosition;
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
            if (content == null) return;
            bool enabledButton = button == null || button.IsInteractable();
            bool down = pressed && enabledButton;
            if (hasBase) content.anchoredPosition = basePosition + new Vector2(0f, down ? 0f : raise);
            var tint = !enabledButton ? Color.white
                : down ? new Color(color.r * PressedDarken, color.g * PressedDarken, color.b * PressedDarken, color.a)
                : color;
            foreach (var graphic in graphics)
                if (graphic != null) graphic.color = tint;
        }
    }
}
