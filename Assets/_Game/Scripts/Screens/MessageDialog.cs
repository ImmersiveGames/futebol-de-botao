using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>
    /// Pop-up por cima de tudo: título, explicação e um ou dois botões (confirmar e cancelar). Os textos chegam já
    /// traduzidos. Sem <c>cancel</c>, mostra só o botão de confirmar.
    /// </summary>
    public sealed class MessageDialog : MonoBehaviour
    {
        private List<MenuNavigator> paused;
        private bool closed;

        public static void Open(string title, string message, string confirm, Action onConfirm, string cancel = null, Action onCancel = null)
        {
            var go = new GameObject("Pop-up", typeof(RectTransform));
            var dialog = go.AddComponent<MessageDialog>();
            dialog.Build(title, message, confirm, onConfirm, cancel, onCancel);
        }

        private void Build(string title, string message, string confirmLabel, Action onConfirm, string cancelLabel, Action onCancel)
        {
            var root = OverlayUi.CreateCanvas(gameObject, 700);
            var panel = OverlayUi.CreatePanel(root, new Vector2(640f, 520f));
            OverlayUi.CreateText(panel, "Título", title, 44, FontStyle.Bold, OverlayUi.HighlightColor, new Vector2(0f, 180f), new Vector2(580f, 80f));
            OverlayUi.CreateText(panel, "Mensagem", message, 30, FontStyle.Normal, Color.white, new Vector2(0f, 30f), new Vector2(560f, 210f));

            bool two = !string.IsNullOrEmpty(cancelLabel);
            var confirm = OverlayUi.CreateButton(panel, "Confirmar", confirmLabel, new Vector2(two ? 140f : 0f, -175f), new Vector2(250f, 80f),
                () => Close(onConfirm), ButtonStyle.Yellow);
            var entries = new List<MenuEntry>();
            Button back = confirm;
            if (two)
            {
                back = OverlayUi.CreateButton(panel, "Cancelar", cancelLabel, new Vector2(-140f, -175f), new Vector2(250f, 80f),
                    () => Close(onCancel), ButtonStyle.Red);
                entries.Add(OverlayUi.Entry(back));
            }
            entries.Add(OverlayUi.Entry(confirm));

            paused = OverlayUi.PauseNavigators(gameObject);
            var navigator = gameObject.AddComponent<MenuNavigator>();
            navigator.Configure(entries.ToArray(), back, entries.Count - 1);
        }

        private void Close(Action then)
        {
            if (closed) return;
            closed = true;
            OverlayUi.ResumeNavigators(paused);
            Destroy(gameObject);
            then?.Invoke();
        }
    }
}
