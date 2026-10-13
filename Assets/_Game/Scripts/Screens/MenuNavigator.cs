using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>
    /// Navegação de um painel de menu pelo teclado/controle (qualquer dispositivo): cima/baixo anda entre os itens na
    /// ordem da lista; esquerda/direita muda o valor numa linha de opção, ou anda entre botões; E/A confirma; Q/B aperta
    /// o "Voltar" do painel. O item escolhido ganha uma moldura amarela pulsando, só enquanto se usa teclado/controle.
    /// </summary>
    public sealed class MenuNavigator : MonoBehaviour
    {
        private const float RepeatDelay = 0.35f;
        private const float RepeatRate = 0.15f;
        private const float Threshold = 0.5f;
        private const float FramePadding = 10f;
        private const float FrameThickness = 6f;
        private const float PulseSpeed = 5f;
        private static readonly Color FrameColor = new(1f, 0.85f, 0.25f, 1f);

        [Tooltip("Itens na ordem de navegação (de cima para baixo).")]
        [SerializeField] private MenuEntry[] items = new MenuEntry[0];
        [Tooltip("Item escolhido ao abrir o painel.")]
        [SerializeField] private int startIndex;
        [Tooltip("Botão apertado pelo Q/B. Vazio: Voltar não faz nada.")]
        [SerializeField] private Button back;

        private static InputActionAsset controls;
        private static InputAction navigate, confirm, backAction;

        private int index;
        private int enabledFrame;
        private Vector2Int heldDirection;
        private float repeatAt;
        private RectTransform frame;
        private Image[] bars;

        public void Configure(MenuEntry[] menuItems, Button backButton, int start = 0)
        {
            items = menuItems ?? new MenuEntry[0];
            back = backButton;
            startIndex = start;
            // Criado já ligado (AddComponent em tempo de execução): o OnEnable rodou antes da lista existir.
            if (isActiveAndEnabled) ResetSelection();
        }

        private void OnEnable()
        {
            EnsureControls();
            ResetSelection();
        }

        private void ResetSelection()
        {
            index = Mathf.Clamp(startIndex, 0, Mathf.Max(0, items.Length - 1));
            if (!IsAvailable(index)) Step(1);
            heldDirection = Vector2Int.zero;
            // A tecla que abriu este painel (no painel anterior) não vale aqui no mesmo quadro.
            enabledFrame = Time.frameCount;
        }

        private static void EnsureControls()
        {
            if (controls != null) return;
            controls = ControlsDefinition.CreateMenu();
            var map = controls.FindActionMap(ControlsDefinition.MenuMap);
            navigate = map.FindAction(ControlsDefinition.MenuNavigate);
            confirm = map.FindAction(ControlsDefinition.MenuConfirm);
            backAction = map.FindAction(ControlsDefinition.MenuBack);
            map.Enable();
        }

        private void Update()
        {
            EnsureControls();

            // Botão clicado com o mouse fica "selecionado" no EventSystem e o Enter/A apertaria de novo: a navegação é daqui.
            var events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject != null) events.SetSelectedGameObject(null);

            var pointer = Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame) PadMode.Active = false;

            if (Time.frameCount != enabledFrame) ReadInput();
            UpdateFrame();
        }

        private void ReadInput()
        {
            Vector2 axis = navigate.ReadValue<Vector2>();
            var direction = new Vector2Int(
                axis.x >= Threshold ? 1 : axis.x <= -Threshold ? -1 : 0,
                axis.y >= Threshold ? 1 : axis.y <= -Threshold ? -1 : 0);
            // Diagonal do analógico: vale o eixo mais forte.
            if (direction.x != 0 && direction.y != 0)
            {
                if (Mathf.Abs(axis.x) > Mathf.Abs(axis.y)) direction.y = 0;
                else direction.x = 0;
            }

            bool move = false;
            if (direction == Vector2Int.zero) heldDirection = Vector2Int.zero;
            else if (direction != heldDirection || Time.unscaledTime >= repeatAt)
            {
                repeatAt = Time.unscaledTime + (direction != heldDirection ? RepeatDelay : RepeatRate);
                heldDirection = direction;
                move = true;
            }

            bool confirmed = confirm.WasPressedThisFrame();
            bool backed = backAction.WasPressedThisFrame();
            if (!move && !confirmed && !backed) return;

            // O primeiro comando depois do mouse/toque só mostra o destaque.
            if (!PadMode.Active)
            {
                PadMode.Active = true;
                return;
            }

            if (backed)
            {
                if (back != null && back.isActiveAndEnabled && back.interactable) back.onClick.Invoke();
                return;
            }

            if (!IsAvailable(index)) Step(1);
            if (!IsAvailable(index)) return;
            var item = items[index];

            if (confirmed)
            {
                item.Confirm();
                return;
            }

            if (direction.y != 0) Step(-direction.y);
            else if (item.ChangesValue) item.Change(direction.x);
            else Step(direction.x);
        }

        /// <summary>Próximo item disponível na direção (1 = para baixo/direita), dando a volta no fim da lista.</summary>
        private void Step(int step)
        {
            if (items.Length == 0) return;
            int start = index;
            for (int i = 0; i < items.Length; i++)
            {
                index = ((index + step) % items.Length + items.Length) % items.Length;
                if (IsAvailable(index)) return;
            }
            index = start;
        }

        private bool IsAvailable(int i) => i >= 0 && i < items.Length && items[i] != null && items[i].IsAvailable;

        // ---- Destaque ----

        private void UpdateFrame()
        {
            bool show = PadMode.Active && IsAvailable(index);
            if (!show)
            {
                if (frame != null) frame.gameObject.SetActive(false);
                return;
            }

            var space = (RectTransform)transform;
            if (!items[index].TryGetBounds(space, out var bounds))
            {
                if (frame != null) frame.gameObject.SetActive(false);
                return;
            }

            EnsureFrame();
            frame.gameObject.SetActive(true);
            frame.SetAsLastSibling();
            bounds.min -= Vector2.one * FramePadding;
            bounds.max += Vector2.one * FramePadding;
            frame.anchoredPosition = bounds.center - space.rect.center;
            frame.sizeDelta = bounds.size;

            float alpha = Mathf.Lerp(0.45f, 1f, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * PulseSpeed));
            var color = FrameColor;
            color.a = alpha;
            foreach (var bar in bars) bar.color = color;
        }

        private void EnsureFrame()
        {
            if (frame != null) return;
            var go = new GameObject("Destaque", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            frame = (RectTransform)go.transform;
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0.5f, 0.5f);

            // Quatro barras nas bordas: o item continua visível por dentro.
            bars = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var bar = new GameObject("Borda", typeof(RectTransform)).AddComponent<Image>();
                bar.transform.SetParent(frame, false);
                bar.raycastTarget = false;
                var rect = bar.rectTransform;
                bool horizontal = i < 2;
                rect.anchorMin = horizontal ? new Vector2(0f, i == 0 ? 1f : 0f) : new Vector2(i == 2 ? 0f : 1f, 0f);
                rect.anchorMax = horizontal ? new Vector2(1f, i == 0 ? 1f : 0f) : new Vector2(i == 2 ? 0f : 1f, 1f);
                rect.pivot = horizontal ? new Vector2(0.5f, i == 0 ? 1f : 0f) : new Vector2(i == 2 ? 0f : 1f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = horizontal ? new Vector2(0f, FrameThickness) : new Vector2(FrameThickness, 0f);
                bars[i] = bar;
            }
        }
    }
}
