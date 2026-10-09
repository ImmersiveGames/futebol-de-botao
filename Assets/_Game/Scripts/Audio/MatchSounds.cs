using System.Collections.Generic;
using Immersive.Audio.Authoring;
using Immersive.Audio.Unity.Hosts;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Sons da partida: peteleco, batidas na mesa e os lances (apito, gol, cartão).
    /// Toca pelo <see cref="AudioRuntimeHost"/> do Persistent Content (com.immersive.audio), que também toca a música.
    /// O pacote não aplica o limite de repetição do cue, então o intervalo mínimo entre dois sons iguais fica aqui.
    /// </summary>
    public sealed class MatchSounds : MonoBehaviour
    {
        [SerializeField] private MatchController match;

        [Header("Mesa")]
        [SerializeField] private AudioSfxCueAsset flick;
        [SerializeField] private AudioSfxCueAsset softHit;
        [SerializeField] private AudioSfxCueAsset hardHit;
        [SerializeField] private AudioSfxCueAsset wallHit;

        [Header("Lances")]
        [SerializeField] private AudioSfxCueAsset whistle;
        [SerializeField] private AudioSfxCueAsset finalWhistle;
        [SerializeField] private AudioSfxCueAsset goal;
        [SerializeField] private AudioSfxCueAsset card;

        [Tooltip("Batidas mais fracas que isto (velocidade relativa) não fazem som.")]
        [SerializeField, Min(0f)] private float minImpactSpeed = 0.4f;
        [Tooltip("A partir daqui a batida usa o som forte.")]
        [SerializeField, Min(0f)] private float hardImpactSpeed = 4f;
        [Tooltip("Intervalo mínimo entre dois sons iguais, para várias batidas juntas não virarem chiado.")]
        [SerializeField, Min(0f)] private float minRepeatSeconds = 0.06f;

        private readonly Dictionary<AudioSfxCueAsset, float> lastPlayed = new();
        private AimController aim;
        private AudioRuntimeHost host;
        private bool missingHostLogged;

        public void Configure(MatchController matchController, AudioSfxCueAsset flickCue, AudioSfxCueAsset softCue,
            AudioSfxCueAsset hardCue, AudioSfxCueAsset wallCue, AudioSfxCueAsset whistleCue, AudioSfxCueAsset finalWhistleCue,
            AudioSfxCueAsset goalCue, AudioSfxCueAsset cardCue)
        {
            match = matchController;
            flick = flickCue;
            softHit = softCue;
            hardHit = hardCue;
            wallHit = wallCue;
            whistle = whistleCue;
            finalWhistle = finalWhistleCue;
            goal = goalCue;
            card = cardCue;
        }

        private void OnEnable()
        {
            if (match == null) match = FindAnyObjectByType<MatchController>();
            aim = FindAnyObjectByType<AimController>();
            if (match != null) match.Noticed += OnNoticed;
            if (aim != null)
            {
                aim.Flicked += OnFlicked;
                aim.BallKicked += OnBallKicked;
            }
            TableImpacts.Hit += OnImpact;
        }

        private void OnDisable()
        {
            if (match != null) match.Noticed -= OnNoticed;
            if (aim != null)
            {
                aim.Flicked -= OnFlicked;
                aim.BallKicked -= OnBallKicked;
            }
            TableImpacts.Hit -= OnImpact;
        }

        private void OnFlicked(Disc disc, Vector2 impulse) => Play(flick);

        private void OnBallKicked(Vector2 impulse) => Play(flick);

        private void OnImpact(TableImpactKind kind, float speed)
        {
            if (speed < minImpactSpeed) return;
            if (kind == TableImpactKind.Wall) Play(wallHit);
            else Play(speed >= hardImpactSpeed ? hardHit : softHit);
        }

        private void OnNoticed(MatchNotice notice)
        {
            switch (notice)
            {
                case MatchNotice.KickOff:
                case MatchNotice.Foul:
                case MatchNotice.Penalty:
                case MatchNotice.GoalAnnulled:
                    Play(whistle);
                    break;
                case MatchNotice.YellowCard:
                case MatchNotice.RedCard:
                    Play(card);
                    break;
                case MatchNotice.Goal:
                    Play(goal);
                    break;
                case MatchNotice.FinalWhistle:
                    Play(finalWhistle);
                    break;
            }
        }

        private void Play(AudioSfxCueAsset cue)
        {
            if (cue == null || !TryGetHost()) return;
            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(cue, out float last) && now - last < minRepeatSeconds) return;
            lastPlayed[cue] = now;

            var result = host.PlaySfx(cue);
            if (!result.Succeeded) Debug.LogWarning($"[Som] '{cue.name}' não tocou: {result.Status}.");
        }

        /// <summary>O host fica no Persistent Content, que carrega antes da partida.</summary>
        private bool TryGetHost()
        {
            if (host != null) return true;
            host = FindAnyObjectByType<AudioRuntimeHost>();
            if (host != null) return true;
            if (!missingHostLogged)
            {
                missingHostLogged = true;
                Debug.LogWarning("[Som] Nenhum AudioRuntimeHost carregado. Rode \"Futebol de Botão/Criar áudio\".");
            }
            return false;
        }
    }
}
