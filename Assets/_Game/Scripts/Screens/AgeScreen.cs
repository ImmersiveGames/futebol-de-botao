using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>
    /// Tela de idade (primeira abertura, onde há anúncios): o jogador escolhe o ano de nascimento com setas (1 e 10 anos)
    /// e confirma. Começa sem ano nenhum, para não sugerir uma idade; Confirmar só acende depois de escolher. Não tem
    /// Voltar: a resposta é necessária para os anúncios e não é perguntada de novo (<see cref="AgeGate"/>).
    /// </summary>
    public sealed class AgeScreen : MonoBehaviour
    {
        /// <summary>Primeiro ano mostrado ao apertar uma seta (só um ponto de partida, perto do meio do público).</summary>
        private const int StartAge = 10;

        private static AgeScreen open;

        private Action onClose;
        private int year;
        private Text yearLabel;
        private Button confirm;
        private List<MenuNavigator> paused;

        public static bool IsOpen => open != null;

        /// <summary>Abre a pergunta (se ainda não foi respondida e não está aberta).</summary>
        public static void Open(Action closed = null)
        {
            if (open != null || !AgeGate.Needed)
            {
                if (open == null) closed?.Invoke();
                return;
            }
            var go = new GameObject("Idade", typeof(RectTransform));
            go.SetActive(false);
            open = go.AddComponent<AgeScreen>();
            open.onClose = closed;
            open.Build();
            go.SetActive(true);
        }

        private void OnDestroy()
        {
            if (open == this) open = null;
        }

        private void Change(int step)
        {
            int current = AgeGate.CurrentYear;
            year = year == 0 ? current - StartAge : Mathf.Clamp(year + step, AgeGate.OldestYear, current);
            Refresh();
        }

        private void Refresh()
        {
            yearLabel.text = year == 0 ? "----" : year.ToString();
            confirm.interactable = year > 0;
        }

        private void Confirm()
        {
            if (year <= 0) return;
            AgeGate.Set(year);
            Ads.Initialize();
            OverlayUi.ResumeNavigators(paused);
            if (open == this) open = null;
            var closed = onClose;
            Destroy(gameObject);
            closed?.Invoke();
        }

        private void Build()
        {
            var root = OverlayUi.CreateCanvas(gameObject, 600);
            var panel = OverlayUi.CreatePanel(root, new Vector2(660f, 640f));
            LocalizedText.Attach(OverlayUi.CreateText(panel, "Título", "Em que ano você nasceu?", 42, FontStyle.Bold, Color.white,
                new Vector2(0f, 220f), new Vector2(600f, 110f)));

            const float y = 40f;
            var minusTen = OverlayUi.CreateButton(panel, "-10", "<<", new Vector2(-250f, y), new Vector2(90f, 80f), () => Change(-10));
            var minus = OverlayUi.CreateButton(panel, "-1", "<", new Vector2(-145f, y), new Vector2(80f, 80f), () => Change(-1));
            var plus = OverlayUi.CreateButton(panel, "+1", ">", new Vector2(145f, y), new Vector2(80f, 80f), () => Change(1));
            var plusTen = OverlayUi.CreateButton(panel, "+10", ">>", new Vector2(250f, y), new Vector2(90f, 80f), () => Change(10));
            yearLabel = OverlayUi.CreateText(panel, "Ano", "----", 56, FontStyle.Bold, OverlayUi.HighlightColor, new Vector2(0f, y),
                new Vector2(190f, 80f));
            var row = OverlayUi.CreateRect(panel, "Linha do ano", Vector2.zero, Vector2.zero).gameObject.AddComponent<MenuEntry>();
            row.Configure(null, minus, plus, (RectTransform)minusTen.transform, (RectTransform)plusTen.transform);

            LocalizedText.Attach(OverlayUi.CreateText(panel, "Aviso", "Usamos só para mostrar anúncios certos para a sua idade. Fica guardado só neste aparelho.",
                24, FontStyle.Normal, OverlayUi.LabelColor, new Vector2(0f, -90f), new Vector2(580f, 100f)));

            confirm = OverlayUi.CreateButton(panel, "Confirmar", "Confirmar", new Vector2(0f, -225f), new Vector2(360f, 80f), Confirm,
                ButtonStyle.Yellow);
            LocalizedText.Attach(OverlayUi.Label(confirm));
            var confirmEntry = OverlayUi.Entry(confirm);

            paused = OverlayUi.PauseNavigators(gameObject);
            var navigator = gameObject.AddComponent<MenuNavigator>();
            navigator.Configure(new[] { row, confirmEntry }, null);
            Refresh();
        }
    }
}
