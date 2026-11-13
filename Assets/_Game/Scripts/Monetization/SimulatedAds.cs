using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>
    /// Anúncios de mentira para testar no editor (e em build de desenvolvimento sem o AdMob): uma tela por cima de tudo
    /// com uma contagem de <see cref="Seconds"/> segundos e Fechar. No anúncio com prêmio, "Pular" fecha sem prêmio (para
    /// testar quando o jogador desiste).
    /// </summary>
    public sealed class SimulatedAds : IAdsProvider
    {
        public const float Seconds = 3f;

        public void Initialize(AgeGroup group) => Debug.Log($"[Anúncios] Teste ligado (faixa de idade: {group}).");

        public void ShowInterstitial(Action closed) => SimulatedAdScreen.Open(false, _ => closed?.Invoke());

        public void ShowRewarded(Action<bool> done) => SimulatedAdScreen.Open(true, done);

        private GameObject banner;

        /// <summary>Faixa cinza "Banner de teste" no topo, do tamanho de um banner de 50 dp num celular de 360 dp de largura.</summary>
        public void ShowBanner()
        {
            if (banner == null) banner = SimulatedBanner.Create();
            banner.SetActive(true);
        }

        public void HideBanner()
        {
            if (banner != null) banner.SetActive(false);
        }

        public float BannerHeightPixels =>
            banner != null && banner.activeSelf ? SimulatedBanner.HeightDp * Screen.width / SimulatedBanner.WidthDp + Ads.TopInsetPixels : 0f;
    }

    /// <summary>O banner de teste: Canvas próprio que acompanha a largura da tela, preso no topo da área segura.</summary>
    internal static class SimulatedBanner
    {
        public const float WidthDp = 360f;
        public const float HeightDp = 50f;

        public static GameObject Create()
        {
            var go = new GameObject("Banner de teste", typeof(RectTransform));
            UnityEngine.Object.DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 800;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(WidthDp, 640f);
            scaler.matchWidthOrHeight = 0f;
            var root = (RectTransform)go.transform;
            var safe = OverlayUi.CreateRect(root, "Área segura", Vector2.zero, Vector2.zero);
            var area = Screen.safeArea;
            safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            safe.offsetMin = safe.offsetMax = Vector2.zero;
            var bar = OverlayUi.CreateRect(safe, "Faixa", Vector2.zero, Vector2.zero);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = Vector2.one;
            bar.pivot = new Vector2(0.5f, 1f);
            bar.sizeDelta = new Vector2(0f, HeightDp);
            var image = bar.gameObject.AddComponent<Image>();
            image.color = new Color(0.55f, 0.55f, 0.55f);
            image.raycastTarget = true;
            var text = OverlayUi.CreateText(bar, "Texto", "Banner de teste", 16, FontStyle.Bold, Color.white, Vector2.zero, Vector2.zero);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            return go;
        }
    }

    /// <summary>A tela do anúncio de teste.</summary>
    internal sealed class SimulatedAdScreen : MonoBehaviour
    {
        private Action<bool> done;
        private float remaining;
        private Text countdown;
        private Button close;
        private Button skip;
        private List<MenuNavigator> paused;
        private bool finished;

        public static void Open(bool rewarded, Action<bool> done)
        {
            var go = new GameObject("Anúncio de teste", typeof(RectTransform));
            DontDestroyOnLoad(go);
            var screen = go.AddComponent<SimulatedAdScreen>();
            screen.Build(rewarded, done);
        }

        private void Build(bool rewarded, Action<bool> callback)
        {
            done = callback;
            remaining = SimulatedAds.Seconds;
            var root = OverlayUi.CreateCanvas(gameObject, 900);
            var panel = OverlayUi.CreatePanel(root, new Vector2(620f, 520f));
            OverlayUi.CreateText(panel, "Título", rewarded ? "Anúncio de teste (com prêmio)" : "Anúncio de teste (entre partidas)", 34,
                FontStyle.Bold, Color.white, new Vector2(0f, 180f), new Vector2(560f, 90f));
            countdown = OverlayUi.CreateText(panel, "Contagem", string.Empty, 72, FontStyle.Bold, OverlayUi.HighlightColor,
                new Vector2(0f, 40f), new Vector2(300f, 110f));
            close = OverlayUi.CreateButton(panel, "Fechar", "Fechar", new Vector2(0f, -110f), new Vector2(360f, 80f), () => Finish(true),
                ButtonStyle.Yellow);
            close.gameObject.SetActive(false);
            var entries = new List<MenuEntry> { OverlayUi.Entry(close) };
            if (rewarded)
            {
                skip = OverlayUi.CreateButton(panel, "Pular", "Pular (sem prêmio)", new Vector2(0f, -205f), new Vector2(360f, 70f),
                    () => Finish(false), ButtonStyle.Red);
                entries.Add(OverlayUi.Entry(skip));
            }
            paused = OverlayUi.PauseNavigators(gameObject);
            var navigator = gameObject.AddComponent<MenuNavigator>();
            navigator.Configure(entries.ToArray(), skip != null ? skip : close);
        }

        private void Update()
        {
            if (finished || remaining <= 0f) return;
            remaining -= Time.unscaledDeltaTime;
            countdown.text = Mathf.CeilToInt(Mathf.Max(0f, remaining)).ToString();
            if (remaining > 0f) return;
            countdown.text = "OK";
            close.gameObject.SetActive(true);
        }

        private void Finish(bool watched)
        {
            if (finished) return;
            finished = true;
            OverlayUi.ResumeNavigators(paused);
            Destroy(gameObject);
            done?.Invoke(watched);
        }
    }
}
