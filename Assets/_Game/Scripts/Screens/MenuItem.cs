using UnityEngine;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>
    /// Um item que o teclado/controle alcança num menu (MenuNavigator): um botão (Confirmar aperta) ou uma linha de opção
    /// com setas (esquerda/direita apertam "&lt;" e "&gt;"; Confirmar aperta "&gt;").
    /// </summary>
    public sealed class MenuItem : MonoBehaviour
    {
        [Tooltip("Botão apertado pelo Confirmar. Vazio numa linha de opção: Confirmar avança o valor.")]
        [SerializeField] private Button confirm;
        [Tooltip("Seta \"<\" da linha (esquerda).")]
        [SerializeField] private Button previous;
        [Tooltip("Seta \">\" da linha (direita).")]
        [SerializeField] private Button next;
        [Tooltip("Retângulos que o destaque contorna. Vazio: o próprio item.")]
        [SerializeField] private RectTransform[] area = new RectTransform[0];

        public void Configure(Button confirmButton, Button previousButton = null, Button nextButton = null, params RectTransform[] highlightArea)
        {
            confirm = confirmButton;
            previous = previousButton;
            next = nextButton;
            area = highlightArea ?? new RectTransform[0];
        }

        /// <summary>Esquerda/direita mudam o valor (linha de opção); senão andam entre os itens.</summary>
        public bool ChangesValue => previous != null || next != null;

        public bool IsAvailable => isActiveAndEnabled;

        public void Confirm()
        {
            if (confirm != null) Press(confirm);
            else if (next != null) Press(next);
        }

        public void Change(int step) => Press(step < 0 ? previous : next);

        private static void Press(Button button)
        {
            if (button != null && button.isActiveAndEnabled && button.interactable) button.onClick.Invoke();
        }

        /// <summary>Cantos do retângulo que envolve o item, no espaço local de <paramref name="space"/>.</summary>
        public bool TryGetBounds(RectTransform space, out Rect bounds)
        {
            bounds = default;
            bool any = false;
            var corners = new Vector3[4];
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;

            void Add(RectTransform rect)
            {
                if (rect == null || !rect.gameObject.activeInHierarchy) return;
                rect.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    Vector2 local = space.InverseTransformPoint(corner);
                    min = Vector2.Min(min, local);
                    max = Vector2.Max(max, local);
                }
                any = true;
            }

            if (area != null && area.Length > 0)
                foreach (var rect in area) Add(rect);
            else Add((RectTransform)transform);

            if (any) bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return any;
        }
    }
}
