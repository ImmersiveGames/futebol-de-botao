using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Brilho de quem é a vez: um halo pulsando atrás dos botões que podem ser usados agora
    /// (mais forte no botão sendo mirado), atrás da bola no tiro de meta e atrás do goleiro no "Vai chutar".
    /// </summary>
    public sealed class TurnHighlight : MonoBehaviour
    {
        private const string HaloName = "Destaque";
        private const int HaloOrder = 9;
        private const float HaloScale = 1.45f;
        private const float SelectedScale = 1.6f;
        private const float PulseSpeed = 4f;
        private const float MinAlpha = 0.25f;
        private const float MaxAlpha = 0.55f;
        private const float SelectedAlpha = 0.85f;

        [SerializeField] private MatchController match;

        private readonly Dictionary<Disc, SpriteRenderer> discHalos = new();
        private readonly Dictionary<Goalkeeper, SpriteRenderer> keeperHalos = new();
        private SpriteRenderer ballHalo;
        private AimController aim;
        private bool built;

        public void Configure(MatchController matchController) => match = matchController;

        private void Update()
        {
            if (match == null) return;
            if (!built) Build();

            var state = match.State;
            bool paused = Time.timeScale <= 0f;
            float pulse = Mathf.Lerp(MinAlpha, MaxAlpha, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * PulseSpeed));
            bool discsActive = !paused && state is MatchState.Aim or MatchState.ShotAim or MatchState.PenaltySetup;
            var selected = aim != null && aim.IsAiming && !aim.IsBallKick ? aim.SelectedDisc : null;

            foreach (var pair in discHalos)
            {
                bool on = discsActive && pair.Key != null && match.CanUse(pair.Key);
                bool isSelected = on && pair.Key == selected;
                Show(pair.Value, on, isSelected ? SelectedAlpha : pulse, isSelected ? SelectedScale : HaloScale);
            }

            bool ballOn = !paused && state == MatchState.GoalKick;
            bool ballSelected = ballOn && aim != null && aim.IsAiming && aim.IsBallKick;
            Show(ballHalo, ballOn, ballSelected ? SelectedAlpha : pulse, ballSelected ? SelectedScale : HaloScale);

            // No "Vai chutar" quem age é o defensor, ajustando o goleiro.
            foreach (var pair in keeperHalos)
            {
                bool on = !paused && state == MatchState.ShotCall && pair.Key != null && pair.Key.Side == match.ActingSide;
                Show(pair.Value, on, pulse, 1.3f);
            }
        }

        private void Build()
        {
            // A partida ainda não juntou os botões.
            if (match.AllDiscs.Count == 0) return;
            built = true;
            aim = FindAnyObjectByType<AimController>();
            foreach (var disc in match.AllDiscs)
                discHalos[disc] = CreateHalo(disc.transform);
            foreach (var keeper in match.Keepers)
                keeperHalos[keeper] = CreateHalo(keeper.transform);
            var ball = FindAnyObjectByType<Ball>();
            if (ball != null) ballHalo = CreateHalo(ball.transform);
        }

        private static SpriteRenderer CreateHalo(Transform owner)
        {
            var source = owner.GetComponent<SpriteRenderer>();
            if (source == null) return null;

            var existing = owner.Find(HaloName);
            var go = existing != null ? existing.gameObject : new GameObject(HaloName);
            go.transform.SetParent(owner, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            var halo = go.GetComponent<SpriteRenderer>();
            if (halo == null) halo = go.AddComponent<SpriteRenderer>();
            halo.sprite = source.sprite;
            halo.sortingLayerID = source.sortingLayerID;
            halo.sortingOrder = Mathf.Min(HaloOrder, source.sortingOrder - 1);
            halo.enabled = false;
            return halo;
        }

        private static void Show(SpriteRenderer halo, bool on, float alpha, float scale)
        {
            if (halo == null) return;
            halo.enabled = on;
            if (!on) return;
            halo.color = new Color(1f, 0.95f, 0.55f, alpha);
            halo.transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
