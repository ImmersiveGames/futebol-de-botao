using System.Collections;
using Immersive.Audio.Authoring;
using Immersive.Audio.Unity.Services;
using Immersive.Framework.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>
    /// Toca o rádio na partida (<see cref="Radio"/>). A Route da partida continua pedindo a música dela ao framework
    /// (assim, ao voltar ao menu, a música do menu sempre volta); quando o framework termina de tocá-la, o rádio troca pela
    /// escolhida direto no serviço de BGM, ou para (Desligada). Na Aleatória, ao fim de cada música sorteia a próxima;
    /// música fixa fica em loop. Ao trocar, mostra o nome por <see cref="NameSeconds"/> segundos.
    /// </summary>
    public sealed class MatchRadio : MonoBehaviour
    {
        private const float NameSeconds = 2f;
        private const float FadeSeconds = 0.3f;
        private const float RouteWaitSeconds = 3f;

        [Tooltip("Cue que a Route da partida toca (a Batucada). O rádio só troca depois que ele começar.")]
        [SerializeField] private AudioBgmCueAsset routeCue;

        private static MatchRadio instance;
        private AudioBgmService service;
        private AudioSource source;
        private RadioTrack current;
        private bool ready;
        private float lastTime;
        private Text nameText;
        private CanvasGroup nameGroup;
        private Coroutine nameRoutine;

        /// <summary>Música tocando agora (null = desligado ou antes de começar).</summary>
        public static RadioTrack Current => instance != null ? instance.current : null;

        public void Configure(AudioBgmCueAsset matchRouteCue) => routeCue = matchRouteCue;

        private void Awake() => instance = this;

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private IEnumerator Start()
        {
            BuildName();
            // Espera o framework tocar a música da Route (na primeira entrada); reiniciando a partida ela já está confirmada.
            float deadline = Time.unscaledTime + RouteWaitSeconds;
            FrameworkBgmDirector director = null;
            while (Time.unscaledTime < deadline)
            {
                if (director == null) director = FindAnyObjectByType<FrameworkBgmDirector>();
                if (service == null) service = FindAnyObjectByType<AudioBgmService>(FindObjectsInactive.Include);
                if (service != null && (routeCue == null || (director != null && director.ConfirmedBgm == routeCue))) break;
                yield return null;
            }
            if (service == null)
            {
                Debug.LogWarning("[Rádio] Sem o serviço de BGM: o rádio não toca.");
                yield break;
            }
            source = service.GetComponent<AudioSource>();
            ready = true;
            // Tocando a música da Route e ela é a escolhida: só fica (sem recomeçar).
            Apply(Radio.Pick());
        }

        /// <summary>Configurações mudaram o rádio com a partida aberta: aplica na hora.</summary>
        public static void SettingsChanged()
        {
            if (instance == null || !instance.ready) return;
            var wanted = Radio.Pick(instance.current);
            // Aleatória continua na música atual (se ela pode tocar); fixa ou desligada troca.
            if (Radio.IsRandom && instance.current != null && Radio.IsUnlocked(instance.current)) wanted = instance.current;
            instance.Apply(wanted);
        }

        /// <summary>"Próxima música" da pausa.</summary>
        public static void Next()
        {
            if (instance == null || !instance.ready || Radio.IsOff) return;
            instance.Apply(Radio.Next(instance.current));
        }

        private void Apply(RadioTrack track)
        {
            if (track == null || track.cue == null)
            {
                current = null;
                service.Stop();
                return;
            }
            // Já tocando essa (ou só ela está liberada na Próxima): continua de onde está.
            bool same = service.ActiveCue == track.cue && source != null && source.isPlaying;
            current = track;
            if (same)
            {
                lastTime = source.time;
                ShowName();
                return;
            }
            service.Play(track.cue);
            lastTime = 0f;
            ShowName();
        }

        // Aleatória: a música deu a volta (o tempo do clipe voltou para o começo) = acabou; sorteia outra.
        private void Update()
        {
            if (!ready || current == null || source == null || !Radio.IsRandom) return;
            if (service.ActiveCue != current.cue || source.clip != current.cue.Clip) return;
            float time = source.time;
            if (time + 1f < lastTime) Apply(Radio.Pick(current));
            else lastTime = time;
        }

        // ---- Nome da música ----

        private void BuildName()
        {
            var go = new GameObject("Nome da música", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720f, 1280f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            nameGroup = go.AddComponent<CanvasGroup>();
            nameGroup.alpha = 0f;
            nameGroup.blocksRaycasts = false;
            nameGroup.interactable = false;
            var box = UiKit.CreateRect(go.transform, "Faixa", Vector2.zero, new Vector2(440f, 56f));
            box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
            box.anchoredPosition = new Vector2(0f, 120f);
            var image = box.gameObject.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.6f);
            image.raycastTarget = false;
            nameText = UiKit.CreateText(box, "Texto", string.Empty, 28, FontStyle.Bold, Color.white, Vector2.zero, new Vector2(420f, 52f));
        }

        private void ShowName()
        {
            if (nameText == null || current == null) return;
            nameText.text = Txt.Format("Rádio: {0}", Radio.NameOf(current));
            if (nameRoutine != null) StopCoroutine(nameRoutine);
            nameRoutine = StartCoroutine(NameRoutine());
        }

        private IEnumerator NameRoutine()
        {
            for (float t = 0f; t < FadeSeconds; t += Time.unscaledDeltaTime)
            {
                nameGroup.alpha = t / FadeSeconds;
                yield return null;
            }
            nameGroup.alpha = 1f;
            yield return new WaitForSecondsRealtime(NameSeconds);
            for (float t = 0f; t < FadeSeconds; t += Time.unscaledDeltaTime)
            {
                nameGroup.alpha = 1f - t / FadeSeconds;
                yield return null;
            }
            nameGroup.alpha = 0f;
            nameRoutine = null;
        }
    }
}
