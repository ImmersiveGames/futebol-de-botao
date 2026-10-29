using System.Collections.Generic;
using Immersive.Audio.Unity.Services;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Música Ligado/Desligado (Opções da partida e menu de pausa), guardada no save do jogo (<see cref="GameSave"/>). O pacote de áudio não muda o
    /// volume da música em tempo de jogo, então a música desligada deixa mudas as fontes do serviço de BGM; ela continua
    /// tocando por baixo e volta no mesmo ponto ao ligar. Os outros sons não mudam.
    /// </summary>
    public sealed class MusicSetting : MonoBehaviour
    {
        private const string PrefsKey = "musica_ligada";

        private static bool? enabled;
        private readonly List<AudioSource> sources = new();
        private AudioBgmService service;

        public static bool Enabled
        {
            get
            {
                if (enabled == null)
                {
                    // Sem save ainda: vale o que ficou guardado antes dele (PlayerPrefs).
                    if (GameSave.Existed) enabled = GameSave.Data.settings.music;
                    else
                    {
                        try { enabled = PlayerPrefs.GetInt(PrefsKey, 1) != 0; }
                        catch { enabled = true; }
                    }
                }
                return enabled.Value;
            }
            set
            {
                enabled = value;
                GameSave.Save("musica");
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            enabled = null;
            var go = new GameObject("Música ligada/desligada") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            go.AddComponent<MusicSetting>();
        }

        // As fontes da música nascem quando a primeira toca; conferir a cada quadro custa pouco (são duas).
        private void LateUpdate()
        {
            if (service == null) service = FindAnyObjectByType<AudioBgmService>(FindObjectsInactive.Include);
            if (service == null) return;
            bool mute = !Enabled;
            service.GetComponents(sources);
            foreach (var source in sources)
                if (source.mute != mute) source.mute = mute;
        }
    }
}
