using Immersive.Audio.Authoring;
using Immersive.Audio.Unity.Hosts;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Som de clique dos botões. Fica em cada cena (o "Criar telas" põe) com o cue do clique; os botões tocam por
    /// <see cref="PlayClick"/> através do <see cref="ButtonClickSound"/> que o <see cref="ButtonSkin"/> coloca neles.
    /// Toca pelo <see cref="AudioRuntimeHost"/> do Persistent Content, como os sons da partida.
    /// </summary>
    public sealed class UiSounds : MonoBehaviour
    {
        [SerializeField] private AudioSfxCueAsset click;

        private static UiSounds current;
        private AudioRuntimeHost host;

        public void Configure(AudioSfxCueAsset clickCue) => click = clickCue;

        private void OnEnable() => current = this;

        private void OnDisable()
        {
            if (current == this) current = null;
        }

        public static void PlayClick()
        {
            if (current != null) current.Play(current.click);
        }

        private void Play(AudioSfxCueAsset cue)
        {
            if (cue == null) return;
            if (host == null) host = FindAnyObjectByType<AudioRuntimeHost>();
            if (host == null) return;
            var result = host.PlaySfx(cue);
            if (!result.Succeeded) Debug.LogWarning($"[Som] '{cue.name}' não tocou: {result.Status}.");
        }
    }
}
