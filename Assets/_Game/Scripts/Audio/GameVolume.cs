using System.Collections.Generic;
using Immersive.Audio.Contracts;
using Immersive.Audio.Unity.Services;
using UnityEngine;
using UnityEngine.Audio;

namespace FutebolDeBotao
{
    /// <summary>
    /// Volumes da tela Configurações (0 a <see cref="MaxLevel"/>), guardados no save (<see cref="SaveData.SettingsData"/>).
    /// O pacote de áudio não muda volumes em tempo de jogo, então: o Volume geral é o do <see cref="AudioListener"/>; a
    /// Música vai pelo grupo "Musica" do mixer Resources/Audio/Volumes (parâmetro exposto "VolumeMusica"), para onde as
    /// fontes do serviço de BGM são mandadas; os Efeitos multiplicam o volume de cada som na hora em que toca
    /// (<see cref="ApplyToEffect"/>). Sem o mixer, a Música só fica muda no 0.
    /// </summary>
    public sealed class GameVolume : MonoBehaviour
    {
        public const int MaxLevel = 10;
        private const string MixerPath = "Audio/Volumes";
        private const string MusicGroupName = "Musica";
        private const string MusicParameter = "VolumeMusica";

        private static GameVolume instance;
        private readonly List<AudioSource> sources = new();
        private AudioBgmService service;
        private AudioMixer mixer;
        private AudioMixerGroup musicGroup;
        private bool mixerWarned;

        private static SaveData.SettingsData Settings => GameSave.Data.settings;

        public static int Master
        {
            get => Mathf.Clamp(Settings.masterVolume, 0, MaxLevel);
            set => Settings.masterVolume = Mathf.Clamp(value, 0, MaxLevel);
        }

        public static int Music
        {
            get => Mathf.Clamp(Settings.musicVolume, 0, MaxLevel);
            set => Settings.musicVolume = Mathf.Clamp(value, 0, MaxLevel);
        }

        public static int Effects
        {
            get => Mathf.Clamp(Settings.effectsVolume, 0, MaxLevel);
            set => Settings.effectsVolume = Mathf.Clamp(value, 0, MaxLevel);
        }

        /// <summary>"70%".</summary>
        public static string Percent(int level) => $"{level * 100 / MaxLevel}%";

        /// <summary>Efeitos no 0 (ou Volume geral no 0): nem toca.</summary>
        public static bool EffectsMuted => Effects == 0 || Master == 0;

        /// <summary>Som que acabou de tocar pelo pacote de áudio: aplica o volume dos Efeitos na fonte dele.</summary>
        public static void ApplyToEffect(AudioPlaybackResult result)
        {
            if (!result.Succeeded || result.Handle is not Component handle || handle == null) return;
            if (handle.TryGetComponent(out AudioSource source)) source.volume *= Effects / (float)MaxLevel;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (instance != null) return;
            var go = new GameObject("Volumes") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            instance = go.AddComponent<GameVolume>();
        }

        private void Awake()
        {
            mixer = Resources.Load<AudioMixer>(MixerPath);
            if (mixer == null) return;
            var groups = mixer.FindMatchingGroups(MusicGroupName);
            musicGroup = groups.Length > 0 ? groups[0] : null;
        }

        // As fontes da música nascem quando a primeira toca; conferir a cada quadro custa pouco (são duas).
        private void LateUpdate()
        {
            AudioListener.volume = Master / (float)MaxLevel;

            bool hasMixer = mixer != null && musicGroup != null;
            if (hasMixer)
            {
                float linear = Music / (float)MaxLevel;
                mixer.SetFloat(MusicParameter, linear > 0.0001f ? 20f * Mathf.Log10(linear) : -80f);
            }
            else if (!mixerWarned)
            {
                mixerWarned = true;
                Debug.LogWarning($"[Som] Sem o mixer Resources/{MixerPath} (grupo '{MusicGroupName}', parâmetro '{MusicParameter}'): " +
                                 "o volume da música só liga e desliga.");
            }

            if (service == null) service = FindAnyObjectByType<AudioBgmService>(FindObjectsInactive.Include);
            if (service == null) return;
            service.GetComponents(sources);
            bool mute = Music == 0;
            foreach (var source in sources)
            {
                if (hasMixer && source.outputAudioMixerGroup != musicGroup) source.outputAudioMixerGroup = musicGroup;
                if (source.mute != mute) source.mute = mute;
            }
        }
    }
}
