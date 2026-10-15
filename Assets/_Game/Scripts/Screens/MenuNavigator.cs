using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>
    /// Navegação de um painel de menu pelo teclado/controle (qualquer dispositivo): cima/baixo anda entre os itens na
    /// ordem da lista; esquerda/direita muda o valor numa linha de opção, ou anda entre botões; E/A confirma; Q/B (e o
    /// Voltar do Android) aperta o "Voltar" do painel. O item escolhido ganha uma moldura pulsando, só enquanto se usa teclado/controle.
    /// Com um dono (Jogador 1 ou 2), só o dispositivo dele navega, e o mouse/toque de outro jogador não clica no painel.
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
        [Tooltip("Dono do painel: 0 = qualquer dispositivo, 1 = Jogador 1, 2 = Jogador 2 (dispositivos da tela Controles).")]
        [SerializeField] private int player;
        [SerializeField] private Color frameColor = FrameColor;
        [Tooltip("Menu de pausa: o dono é quem pausou (DeviceChoice.PausedBy).")]
        [SerializeField] private bool followPauser;

        /// <summary>Quando definido, o Q/B chama isto no lugar do botão Voltar.</summary>
        public System.Action BackAction { get; set; }

        private InputActionAsset controls;
        private InputAction navigate, confirm, backAction;
        private CanvasGroup group;
        private Image blocker;

        private int index;
        private int enabledFrame;
        private Vector2Int heldDirection;
        private float repeatAt;
        private RectTransform frame;
        private Image[] bars;

        public void Configure(MenuEntry[] menuItems, Button backButton, int start = 0, int owner = 0)
        {
            items = menuItems ?? new MenuEntry[0];
            back = backButton;
            startIndex = start;
            player = owner;
            // Criado já ligado (AddComponent em tempo de execução): o OnEnable rodou antes da lista existir.
            if (isActiveAndEnabled) ResetSelection();
        }

        public void SetFrameColor(Color color) => frameColor = color;

        public void FollowPauser() => followPauser = true;

        /// <summary>Põe o cursor no item (por exemplo, no Pronto clicado com o mouse).</summary>
        public void Select(MenuEntry entry)
        {
            int i = System.Array.IndexOf(items, entry);
            if (i >= 0) index = i;
        }

        private void OnEnable()
        {
            EnsureControls();
            if (followPauser) player = DeviceChoice.PausedBy;
            var owned = DeviceChoice.DevicesOf(player);
            if (owned != null) controls.devices = owned;
            else controls.devices = null;
            controls.Enable();
            ResetSelection();
        }

        private void OnDisable()
        {
            if (controls != null) controls.Disable();
            if (frame != null) frame.gameObject.SetActive(false);
            if (group != null) group.blocksRaycasts = true;
            if (blocker != null) blocker.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (controls != null) Destroy(controls);
        }

        private void ResetSelection()
        {
            index = Mathf.Clamp(startIndex, 0, Mathf.Max(0, items.Length - 1));
            if (!IsAvailable(index)) Step(1);
            heldDirection = Vector2Int.zero;
            // A tecla que abriu este painel (no painel anterior) não vale aqui no mesmo quadro.
            enabledFrame = Time.frameCount;
        }

        private void EnsureControls()
        {
            if (controls != null) return;
            controls = ControlsDefinition.CreateMenu();
            var map = controls.FindActionMap(ControlsDefinition.MenuMap);
            navigate = map.FindAction(ControlsDefinition.MenuNavigate);
            confirm = map.FindAction(ControlsDefinition.MenuConfirm);
            backAction = map.FindAction(ControlsDefinition.MenuBack);
        }

        private void Update()
        {
            EnsureControls();

            // Botão clicado com o mouse fica "selecionado" no EventSystem e o Enter/A apertaria de novo: a navegação é daqui.
            var events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject != null) events.SetSelectedGameObject(null);

            var pointer = Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame) PadMode.Active = false;
            UpdatePointerBlock(pointer);

            if (Time.frameCount != enabledFrame) ReadInput();
            UpdateFrame();
        }

        private void PressBack()
        {
            if (BackAction != null) BackAction();
            else if (back != null && back.isActiveAndEnabled && back.interactable) back.onClick.Invoke();
        }

        private void ReadInput()
        {
            // Voltar do Android: aperta o Voltar do painel na hora (sem o passo de mostrar o destaque).
            // O painel do Jogador 2 não usa (o celular é do Jogador 1), nem painel sem Voltar.
            if (player != 2 && (BackAction != null || back != null) && AndroidBack.TryUse())
            {
                PressBack();
                return;
            }

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
                PressBack();
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

        /// <summary>O mouse/toque de outro jogador não clica neste painel.</summary>
        private void UpdatePointerBlock(Pointer pointer)
        {
            if (player == 0) return;
            int owner = DeviceChoice.PointerOwner(pointer);
            bool blocked = owner != 0 && owner != player;
            if (group == null && blocker == null)
            {
                if (!blocked) return;
                // Um CanvasGroup que já existe é de outro (o da pausa é do framework): aí uma tampa invisível segura os cliques.
                if (GetComponent<CanvasGroup>() == null) group = gameObject.AddComponent<CanvasGroup>();
                else blocker = CreateBlocker();
            }
            if (group != null) group.blocksRaycasts = !blocked;
            if (blocker != null)
            {
                blocker.gameObject.SetActive(blocked);
                blocker.transform.SetAsLastSibling();
            }
        }

        private Image CreateBlocker()
        {
            var image = new GameObject("Bloqueio de clique", typeof(RectTransform)).AddComponent<Image>();
            image.transform.SetParent(transform, false);
            image.color = Color.clear;
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return image;
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
            var color = frameColor;
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
