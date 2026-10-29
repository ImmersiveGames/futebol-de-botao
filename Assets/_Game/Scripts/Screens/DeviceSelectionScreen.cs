using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>
    /// Tela "Controles", entre o Um/Dois jogadores e as Opções da partida: Jogador 1 na esquerda, os dispositivos
    /// detectados no meio e Jogador 2 na direita. Cada dispositivo leva o próprio cartão para um lado (A/D, setas,
    /// analógico, D-pad; no mouse/toque, as setinhas do cartão), E/A confirma (clicar/tocar no cartão também) e Q/B
    /// desfaz; no meio o cartão fica desligado (não confirma) e Q/B volta para o Menu.
    /// Um jogador: só a coluna do Jogador 1. Dois jogadores com um lado vazio: confirmar de novo = os dois no mesmo
    /// dispositivo, revezando. Com um dispositivo só a tela é pulada. Monta a UI sozinha (como a Seleção de times).
    /// </summary>
    public sealed class DeviceSelectionScreen : MonoBehaviour
    {
        // Cartão com as setas nas colunas dos lados: 225 + 85 + 4 + 36 = 350, dentro dos 360 da metade da largura (720).
        private const float ColumnX = 225f;
        private const float FirstCardY = 230f;
        private const float CardSpacing = 130f;
        private static readonly Vector2 CardSize = new(170f, 100f);
        private static readonly Vector2 ArrowSize = new(36f, 100f);
        /// <summary>Ícone do dispositivo no cartão: 2 unidades da UI por pixel da arte de 32 px.</summary>
        private const float IconSize = 64f;
        private static readonly Color CardColor = new(0.95f, 0.95f, 0.9f);
        private static readonly Color ConfirmedColor = new(0.55f, 0.9f, 0.55f);
        private static readonly Color TextColor = new(0.1f, 0.1f, 0.1f);

        [SerializeField] private MenuScreen menu;

        private sealed class Entry
        {
            public InputDevice Device;
            /// <summary>-1 Jogador 1, 0 no meio, 1 Jogador 2.</summary>
            public int Position;
            public bool Confirmed;
            /// <summary>Confirmou com o outro lado vazio: mais uma confirmação = mesmo dispositivo para os dois.</summary>
            public bool AskedShared;
            public int HeldDirection;
            public RectTransform Card;
            public Button Button;
            public Image Image;
            public Text Label;
        }

        private readonly List<Entry> entries = new();
        private readonly List<InputDevice> devices = new();
        private readonly List<InputDevice> lastDevices = new();
        private Font font;
        private RectTransform cards;
        private Text instructions;
        private Text secondHeader;
        private Text status;
        private int openFrame;
        private bool built;

        public void Configure(MenuScreen menuScreen) => menu = menuScreen;

        private static bool TwoPlayers => MatchSession.VsAi != true;

        /// <summary>
        /// Abre a tela para o modo escolhido. Devolve false (e já guarda a escolha) quando não há o que escolher:
        /// um dispositivo só (ou nenhum).
        /// </summary>
        public bool Open()
        {
            DeviceChoice.Collect(devices);
            MatchSession.Player2Device = null;
            if (devices.Count <= 1)
            {
                MatchSession.Player1Device = devices.Count == 1 ? devices[0] : null;
                MatchSession.SharedDevice = TwoPlayers;
                return false;
            }

            Build();
            entries.Clear();
            lastDevices.Clear();
            Rebuild();
            openFrame = Time.frameCount;
            instructions.text = TwoPlayers
                ? "Cada um no seu dispositivo: esquerda para o Jogador 1, direita para o Jogador 2. E/A confirma, Q/B desfaz."
                : "No dispositivo com que vai jogar: esquerda para o Jogador 1 e E/A confirma. Q/B volta.";
            secondHeader.gameObject.SetActive(TwoPlayers);
            status.text = string.Empty;
            return true;
        }

        private void Update()
        {
            if (!built) return;

            // Cartão clicado fica selecionado no EventSystem e o Enter clicaria de novo: os comandos são daqui.
            var events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject != null) events.SetSelectedGameObject(null);

            // Controle conectado ou desconectado com a tela aberta.
            DeviceChoice.Collect(devices);
            if (!SameDevices()) Rebuild();

            if (Time.frameCount == openFrame) return;
            // Voltar do Android: volta para a escolha do modo.
            if (AndroidBack.TryUse())
            {
                if (menu != null) menu.ShowMode();
                return;
            }
            foreach (var entry in entries.ToArray()) ReadDevice(entry);
        }

        // ---- Comandos ----

        private void ReadDevice(Entry entry)
        {
            switch (entry.Device)
            {
                case Keyboard keyboard:
                    if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame) Move(entry, -1);
                    if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame) Move(entry, 1);
                    if (keyboard.eKey.wasPressedThisFrame) Confirm(entry);
                    if (keyboard.qKey.wasPressedThisFrame) Back(entry);
                    break;
                case Gamepad gamepad:
                    float x = gamepad.leftStick.x.ReadValue();
                    int direction = gamepad.dpad.left.isPressed || x <= -0.5f ? -1 : gamepad.dpad.right.isPressed || x >= 0.5f ? 1 : 0;
                    if (direction != 0 && direction != entry.HeldDirection) Move(entry, direction);
                    entry.HeldDirection = direction;
                    if (gamepad.buttonSouth.wasPressedThisFrame) Confirm(entry);
                    if (gamepad.buttonEast.wasPressedThisFrame) Back(entry);
                    break;
                // Toque: só pelas setinhas e pelo próprio cartão.
            }
        }

        private void Move(Entry entry, int step)
        {
            if (entry.Confirmed) return;
            int target = Mathf.Clamp(entry.Position + step, -1, TwoPlayers ? 1 : 0);
            if (target == entry.Position || (target != 0 && Occupant(target) != null)) return;
            entry.Position = target;
            entry.AskedShared = false;
            status.text = string.Empty;
            Refresh();
        }

        private void Confirm(Entry entry)
        {
            // No meio o cartão fica desligado: primeiro leva para um lado.
            if (entry.Position == 0) return;

            if (entry.Confirmed)
            {
                // Dois jogadores, o outro lado vazio: segunda confirmação = os dois no mesmo dispositivo.
                if (entry.AskedShared && Occupant(-entry.Position) == null) Finish(shared: entry);
                return;
            }

            entry.Confirmed = true;
            Refresh();
            Evaluate(entry);
        }

        private void Back(Entry entry)
        {
            if (entry.Confirmed)
            {
                entry.Confirmed = false;
                entry.AskedShared = false;
                status.text = string.Empty;
            }
            else if (entry.Position != 0) entry.Position = 0;
            else
            {
                if (menu != null) menu.ShowMode();
                return;
            }
            Refresh();
        }

        private void Evaluate(Entry confirmed)
        {
            var first = Occupant(-1);
            if (!TwoPlayers)
            {
                if (first != null && first.Confirmed) Finish(shared: null);
                return;
            }

            var second = Occupant(1);
            if (first != null && first.Confirmed && second != null && second.Confirmed)
            {
                Finish(shared: null);
                return;
            }

            if (Occupant(-confirmed.Position) == null)
            {
                confirmed.AskedShared = true;
                string other = confirmed.Position < 0 ? "Jogador 2" : "Jogador 1";
                status.text = $"{other}: escolha um dispositivo, ou confirme de novo para os dois jogarem neste, revezando.";
            }
        }

        private void Finish(Entry shared)
        {
            if (shared != null)
            {
                MatchSession.Player1Device = shared.Device;
                MatchSession.Player2Device = null;
                MatchSession.SharedDevice = true;
            }
            else
            {
                MatchSession.Player1Device = Occupant(-1)?.Device;
                MatchSession.Player2Device = TwoPlayers ? Occupant(1)?.Device : null;
                MatchSession.SharedDevice = false;
            }
            if (menu != null) menu.ShowOptionsFromDevices();
        }

        private Entry Occupant(int position)
        {
            foreach (var entry in entries)
                if (entry.Position == position) return entry;
            return null;
        }

        // ---- Lista de dispositivos ----

        private bool SameDevices()
        {
            if (devices.Count != lastDevices.Count) return false;
            for (int i = 0; i < devices.Count; i++)
                if (devices[i] != lastDevices[i]) return false;
            return true;
        }

        /// <summary>Recria os cartões com a lista atual, guardando lado e confirmação de quem continua conectado.</summary>
        private void Rebuild()
        {
            var old = new Dictionary<InputDevice, Entry>();
            foreach (var entry in entries) old[entry.Device] = entry;
            entries.Clear();
            for (int i = cards.childCount - 1; i >= 0; i--) Destroy(cards.GetChild(i).gameObject);

            foreach (var device in devices)
            {
                var entry = new Entry { Device = device };
                if (old.TryGetValue(device, out var previous))
                {
                    entry.Position = previous.Position;
                    entry.Confirmed = previous.Confirmed;
                }
                BuildCard(entry, DeviceChoice.Label(device, devices));
                entries.Add(entry);
            }
            lastDevices.Clear();
            lastDevices.AddRange(devices);
            Refresh();
        }

        private void Refresh()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                entry.Card.anchoredPosition = new Vector2(entry.Position * ColumnX, FirstCardY - i * CardSpacing);
                // Confirmado: amarelo; num lado, ainda escolhendo: verde; no meio: desligado (cinza).
                bool idle = entry.Position == 0 && !entry.Confirmed;
                var style = entry.Confirmed ? ButtonStyle.Yellow : idle ? ButtonStyle.Gray : ButtonStyle.Green;
                if (!ButtonSkin.Apply(entry.Button, style))
                    entry.Image.color = entry.Confirmed ? ConfirmedColor : CardColor;
            }
        }

        // ---- UI ----

        private void Build()
        {
            if (built) return;
            built = true;
            font = GameFont.Get();
            var root = (RectTransform)transform;

            CreateText(root, "Título", "Controles", 52, FontStyle.Bold, Color.white, new Vector2(0f, 520f), new Vector2(680f, 90f));
            instructions = CreateText(root, "Instruções", string.Empty, 24, FontStyle.Normal, Color.white, new Vector2(0f, 420f), new Vector2(660f, 110f));
            CreateText(root, "Jogador 1", "Jogador 1", 30, FontStyle.Bold, Color.white, new Vector2(-ColumnX, 330f), new Vector2(220f, 50f));
            CreateText(root, "Dispositivos", "Dispositivos", 24, FontStyle.Normal, Color.white, new Vector2(0f, 330f), new Vector2(220f, 50f));
            secondHeader = CreateText(root, "Jogador 2", "Jogador 2", 30, FontStyle.Bold, Color.white, new Vector2(ColumnX, 330f), new Vector2(220f, 50f));
            status = CreateText(root, "Aviso", string.Empty, 26, FontStyle.Bold, new Color(1f, 0.85f, 0.25f), new Vector2(0f, -330f), new Vector2(660f, 110f));

            cards = CreateRect(root, "Dispositivos", Vector2.zero, Vector2.zero);
            CreateButton(root, "Voltar", "Voltar", new Vector2(0f, -450f), new Vector2(240f, 70f), () =>
            {
                if (menu != null) menu.ShowMode();
            }, ButtonStyle.Red);
        }

        private void BuildCard(Entry entry, string label)
        {
            entry.Card = CreateRect(cards, label, Vector2.zero, CardSize);
            var card = CreateButton(entry.Card, "Cartão", label, Vector2.zero, CardSize, () => Confirm(entry));
            entry.Button = card;
            entry.Image = (Image)card.targetGraphic;
            entry.Label = card.GetComponentInChildren<Text>();
            entry.Label.fontSize = 24;
            // Com a arte, o ícone troca o nome; com dois controles ou mais, um número pequeno no canto separa um do outro.
            if (ButtonSkin.SetIcon(card, DeviceChoice.Icon(entry.Device), IconSize))
            {
                int number = DeviceChoice.GamepadNumber(entry.Device, devices);
                if (number > 0)
                {
                    var tag = CreateText(card.transform, "Número", number.ToString(), 22, FontStyle.Bold, Color.white,
                        new Vector2(CardSize.x * 0.5f - 22f, CardSize.y * 0.5f - 22f), new Vector2(30f, 30f));
                    tag.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.6f);
                }
            }
            float arrowX = CardSize.x * 0.5f + ArrowSize.x * 0.5f + 4f;
            CreateButton(entry.Card, "Esquerda", "<", new Vector2(-arrowX, 0f), ArrowSize, () => Move(entry, -1));
            CreateButton(entry.Card, "Direita", ">", new Vector2(arrowX, 0f), ArrowSize, () => Move(entry, 1));
        }

        private static RectTransform CreateRect(Transform parent, string objectName, Vector2 position, Vector2 size)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private Text CreateText(Transform parent, string objectName, string content, int size, FontStyle style, Color color,
            Vector2 position, Vector2 box)
        {
            var text = CreateRect(parent, objectName, position, box).gameObject.AddComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private Button CreateButton(Transform parent, string objectName, string label, Vector2 position, Vector2 size, UnityAction action,
            ButtonStyle style = ButtonStyle.Green)
        {
            var rect = CreateRect(parent, objectName, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = CardColor;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(action);
            var text = CreateText(rect, "Texto", label, 30, FontStyle.Bold, TextColor, Vector2.zero, size);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            ButtonSkin.Apply(button, style);
            return button;
        }
    }
}
