using UnityEngine;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>
    /// Tutorial no HUD: o balão da lição (nome da lição pequeno, instrução curta e grande, e Pular) no lugar das
    /// informações de cima no celular, ou no canto de cima à direita no PC; o pop-up de explicação no meio da tela
    /// (com Continuar); e a tela de lição concluída (moedas, Próxima lição, Repetir e Menu).
    /// </summary>
    public sealed partial class MatchHud
    {
        private static readonly Color TutorialHeaderColor = new(1f, 0.85f, 0.25f);

        private sealed class TutorialBalloon
        {
            public GameObject Root;
            public Text Header;
            public Text Instruction;
        }

        private TutorialBalloon tutorialStrip;
        private TutorialBalloon tutorialPc;
        private GameObject tutorialDoneRoot;
        private CanvasScaler tutorialDoneScaler;
        private Text tutorialDoneTitle;
        private Text tutorialDoneCoins;
        private Text tutorialDoneBonus;
        private Button tutorialNext;
        private Text tutorialNextLabel;
        private Button tutorialMenu;

        private void BuildTutorial()
        {
            // Celular: no lugar da faixa de informações de cima (contra a IA), em pé para o jogador.
            float width = ScreenFit.VisibleHalfWidth * 2f - 0.1f;
            const float height = ScreenFit.StripHeight;
            float centerY = ScreenFit.StripStart + height * 0.5f;
            float canvasWidth = width * PixelsPerUnit;
            var strip = CreateWorldCanvas("Balão do tutorial", stripsRoot.transform, new Vector2(0f, centerY), new Vector2(width, height), 0f);
            CreateFramedPanel(strip.transform, 1f);
            float textWidth = canvasWidth - 200f;
            float textX = -canvasWidth * 0.5f + 30f + textWidth * 0.5f;
            tutorialStrip = new TutorialBalloon
            {
                Root = strip.gameObject,
                Header = CreateText(strip.transform, "Lição", 22, FontStyle.Bold, new Vector2(textX, 48f), new Vector2(textWidth, 32f)),
                Instruction = CreateText(strip.transform, "Instrução", 40, FontStyle.Bold, new Vector2(textX, -14f), new Vector2(textWidth, 92f))
            };
            FitInBox(tutorialStrip.Header);
            FitInBox(tutorialStrip.Instruction);
            var skip = CreateButton(strip.transform, "Pular", TutorialTexts.Skip, 24, new Vector2(canvasWidth * 0.5f - 90f, 0f),
                new Vector2(140f, 70f), out _, ButtonStyle.Red);
            skip.onClick.AddListener(SkipLesson);
            strip.gameObject.SetActive(false);

            // PC: painel no canto de cima à direita (o painel do jogo fica à esquerda).
            var pc = new GameObject("Balão do tutorial (PC)", typeof(RectTransform));
            pc.transform.SetParent(transform, false);
            var canvas = pc.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 11;
            var scaler = pc.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 1f;
            pc.AddComponent<GraphicRaycaster>();
            var panel = CreateFramedPanel(pc.transform, PcPanelPixelScale).rectTransform;
            panel.anchorMin = panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(1f, 1f);
            panel.sizeDelta = new Vector2(320f, 300f);
            panel.anchoredPosition = new Vector2(-16f, -16f);
            tutorialPc = new TutorialBalloon
            {
                Root = pc,
                Header = CreateText(panel, "Lição", 20, FontStyle.Bold, new Vector2(0f, 112f), new Vector2(280f, 50f)),
                Instruction = CreateText(panel, "Instrução", 32, FontStyle.Bold, new Vector2(0f, 6f), new Vector2(280f, 150f))
            };
            FitInBox(tutorialPc.Header);
            FitInBox(tutorialPc.Instruction);
            var pcSkip = CreateButton(panel, "Pular", TutorialTexts.Skip, 22, new Vector2(0f, -110f), new Vector2(160f, 50f), out _, ButtonStyle.Red);
            pcSkip.onClick.AddListener(SkipLesson);
            pc.SetActive(false);

            BuildTutorialDone();
            BuildTutorialPopup();
        }

        private GameObject tutorialPopupRoot;
        private CanvasScaler tutorialPopupScaler;
        private Text tutorialPopupTitle;
        private Text tutorialPopupBody;

        /// <summary>Pop-up de explicação: por cima da mesa, com o fundo escurecido e o Continuar.</summary>
        private void BuildTutorialPopup()
        {
            tutorialPopupRoot = new GameObject("Explicação do tutorial", typeof(RectTransform));
            tutorialPopupRoot.transform.SetParent(transform, false);
            var canvas = tutorialPopupRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            tutorialPopupScaler = tutorialPopupRoot.AddComponent<CanvasScaler>();
            tutorialPopupScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            tutorialPopupScaler.referenceResolution = new Vector2(720f, 720f);
            tutorialPopupRoot.AddComponent<GraphicRaycaster>();

            var dim = CreatePanel(tutorialPopupRoot.transform, new Color(0f, 0f, 0f, 0.45f));
            dim.raycastTarget = true;

            var panel = CreateFramedPanel(tutorialPopupRoot.transform, 1f).rectTransform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(640f, 620f);
            panel.anchoredPosition = Vector2.zero;
            if (panelSprite == null) panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);

            tutorialPopupTitle = CreateText(panel, "Lição", 36, FontStyle.Bold, new Vector2(0f, 250f), new Vector2(580f, 60f));
            tutorialPopupTitle.color = TutorialHeaderColor;
            FitInBox(tutorialPopupTitle);
            tutorialPopupBody = CreateText(panel, "Explicação", 34, FontStyle.Normal, new Vector2(0f, 20f), new Vector2(580f, 380f));
            FitInBox(tutorialPopupBody);
            var ok = CreateButton(panel, "Continuar", TutorialTexts.Continue, 32, new Vector2(0f, -240f), new Vector2(400f, 90f),
                out _, ButtonStyle.Yellow);
            ok.onClick.AddListener(() => match.Tutorial?.Continue());
            tutorialPopupRoot.SetActive(false);
        }

        /// <summary>Lição concluída: por cima de tudo, com o fundo escurecido.</summary>
        private void BuildTutorialDone()
        {
            tutorialDoneRoot = new GameObject("Lição concluída", typeof(RectTransform));
            tutorialDoneRoot.transform.SetParent(transform, false);
            var canvas = tutorialDoneRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            tutorialDoneScaler = tutorialDoneRoot.AddComponent<CanvasScaler>();
            tutorialDoneScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            tutorialDoneScaler.referenceResolution = new Vector2(720f, 720f);
            tutorialDoneRoot.AddComponent<GraphicRaycaster>();

            var dim = CreatePanel(tutorialDoneRoot.transform, new Color(0f, 0f, 0f, 0.55f));
            dim.raycastTarget = true;

            var panel = CreateFramedPanel(tutorialDoneRoot.transform, 1f).rectTransform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(560f, 560f);
            panel.anchoredPosition = Vector2.zero;
            if (panelSprite == null) panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);

            tutorialDoneTitle = CreateText(panel, "Título", 44, FontStyle.Bold, new Vector2(0f, 205f), new Vector2(500f, 70f));
            FitInBox(tutorialDoneTitle);
            tutorialDoneCoins = CreateText(panel, "Moedas", 34, FontStyle.Bold, new Vector2(0f, 135f), new Vector2(500f, 50f));
            tutorialDoneCoins.color = TutorialHeaderColor;
            tutorialDoneBonus = CreateText(panel, "Bônus", 26, FontStyle.Normal, new Vector2(0f, 85f), new Vector2(500f, 44f));
            FitInBox(tutorialDoneBonus);

            tutorialNext = CreateButton(panel, "Próxima lição", TutorialTexts.NextLesson, 30, new Vector2(0f, -10f), new Vector2(400f, 84f),
                out tutorialNextLabel, ButtonStyle.Yellow);
            tutorialNext.onClick.AddListener(() => match.Tutorial?.NextLesson());
            var repeat = CreateButton(panel, "Repetir", TutorialTexts.Repeat, 30, new Vector2(0f, -110f), new Vector2(400f, 84f), out _, ButtonStyle.Green);
            repeat.onClick.AddListener(() => match.Tutorial?.Repeat());
            tutorialMenu = CreateButton(panel, "Menu", TutorialTexts.Menu, 30, new Vector2(0f, -210f), new Vector2(400f, 84f), out _, ButtonStyle.Red);
            tutorialMenu.onClick.AddListener(() => match.Tutorial?.GoToMenu());
            tutorialDoneRoot.SetActive(false);
        }

        private void SkipLesson() => match.Tutorial?.Skip();

        private void UpdateTutorial(bool visible, bool portrait)
        {
            var tutorial = match.Tutorial;
            bool on = visible && tutorial != null;
            bool done = on && tutorial.Completed;
            tutorialStrip.Root.SetActive(on && portrait && !done);
            tutorialPc.Root.SetActive(on && !portrait && !done);
            tutorialDoneRoot.SetActive(done);
            bool popup = on && !done && tutorial.WaitingContinue;
            tutorialPopupRoot.SetActive(popup);
            if (!on) return;
            if (popup)
            {
                tutorialPopupScaler.matchWidthOrHeight = portrait ? 0f : 1f;
                tutorialPopupTitle.text = tutorial.Header;
                tutorialPopupBody.text = tutorial.PopupText;
            }

            var balloon = portrait ? tutorialStrip : tutorialPc;
            balloon.Header.text = tutorial.Header;
            balloon.Header.color = TutorialHeaderColor;
            balloon.Instruction.text = tutorial.Instruction;

            if (!done) return;
            // Em pé, o painel encaixa na largura; deitado, na altura.
            tutorialDoneScaler.matchWidthOrHeight = portrait ? 0f : 1f;
            bool last = tutorial.IsLastLesson;
            tutorialDoneTitle.text = last && Tutorial.AllDone ? TutorialTexts.AllDone : TutorialTexts.LessonDone;
            tutorialDoneCoins.text = tutorial.CoinsEarned > 0 ? TutorialTexts.Coins(tutorial.CoinsEarned) : string.Empty;
            tutorialDoneBonus.text = tutorial.BonusEarned > 0 ? TutorialTexts.Bonus(tutorial.BonusEarned)
                : tutorial.CoinsEarned > 0 ? string.Empty
                : TutorialTexts.AlreadyDone;
            // Na última lição o primeiro botão já é o Menu.
            tutorialNextLabel.text = last ? TutorialTexts.Menu : TutorialTexts.NextLesson;
            tutorialMenu.gameObject.SetActive(!last);
        }
    }
}
