using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Brilho de quem é a vez: um halo pulsando atrás dos botões que podem ser usados agora
    /// (mais forte, e ainda pulsando, no botão sendo mirado ou escolhido pelo toque; no teclado/controle só no botão do cursor e no toque só no escolhido), atrás da bola no tiro de meta e atrás do goleiro no "Vai chutar".
    /// Nos botões, com a arte Selected_UI (folha de UI, guardada pelo "Criar telas"), o halo vira o anel de seleção girando
    /// por cima do botão, no tamanho dos pixels do jogo; sem a arte, o brilho com o desenho do botão.
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
        private const float SelectedMinAlpha = 0.5f;
        private const float SelectedMaxAlpha = 0.95f;
        private const string SelectionArtName = "Selected_UI";
        private const float ArtPixelsPerUnit = 32f;
        // Anel de seleção: por cima do botão dentro do Sorting Group dele.
        private const int SelectionOrder = 100;
        /// <summary>Graus por segundo do anel de seleção (o escolhido gira mais rápido).</summary>
        private const float SpinSpeed = 90f;
        private const float SelectedSpinSpeed = 180f;
        private static readonly Color HaloColor = new(1f, 0.95f, 0.55f);

        [SerializeField] private MatchController match;

        private readonly Dictionary<Disc, SpriteRenderer> discHalos = new();
        private readonly Dictionary<Goalkeeper, SpriteRenderer> keeperHalos = new();
        private SpriteRenderer ballHalo;
        private AimController aim;
        private Sprite selectionArt;
        private float spin;
        private float selectedSpin;
        private bool built;

        public void Configure(MatchController matchController) => match = matchController;

        private void Update()
        {
            if (match == null) return;
            if (!built) Build();

            var state = match.State;
            bool paused = Time.timeScale <= 0f;
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * PulseSpeed);
            float pulse = Mathf.Lerp(MinAlpha, MaxAlpha, wave);
            // O escolhido continua piscando, mais forte que os outros.
            float selectedPulse = Mathf.Lerp(SelectedMinAlpha, SelectedMaxAlpha, wave);
            if (!paused)
            {
                spin = Mathf.Repeat(spin - SpinSpeed * Time.unscaledDeltaTime, 360f);
                selectedSpin = Mathf.Repeat(selectedSpin - SelectedSpinSpeed * Time.unscaledDeltaTime, 360f);
            }
            // Mirando (dando força), o brilho some para não brigar com a faixa de força; volta se o chute for cancelado.
            bool aiming = aim != null && aim.IsAiming;
            bool discsActive = !paused && !aiming && state is MatchState.Aim or MatchState.ShotAim or MatchState.PenaltySetup;
            var selected = aim == null ? null
                : aim.IsAiming ? (aim.IsBallKick ? null : aim.SelectedDisc)
                // No toque, o botão escolhido (que o arrasto fora dos botões mira) já brilha mais forte.
                : AimController.TouchMode && !AimController.PadMode && aim.InputEnabled ? aim.TouchSelectedDisc
                : null;
            // Teclado/controle: só o botão do cursor brilha. No toque, só o escolhido (sem escolha, todos os que podem jogar).
            // Com mouse brilham todos os que podem jogar.
            bool aimPhase = aim != null && aim.InputEnabled && state is MatchState.Aim or MatchState.ShotAim;
            var cursor = !aimPhase ? null
                : AimController.PadMode ? aim.CursorDisc
                : AimController.TouchMode ? (aim.IsAiming ? selected : aim.TouchSelectedDisc)
                : null;

            foreach (var pair in discHalos)
            {
                bool on = discsActive && pair.Key != null && match.CanUse(pair.Key) && (cursor == null || pair.Key == cursor);
                bool isSelected = on && pair.Key == selected;
                if (selectionArt != null) ShowSelection(pair.Value, on, isSelected ? selectedPulse : pulse, isSelected ? selectedSpin : spin);
                else Show(pair.Value, on, isSelected ? selectedPulse : pulse, isSelected ? SelectedScale : HaloScale);
            }

            bool ballOn = !paused && !aiming && state == MatchState.GoalKick;
            bool ballSelected = ballOn && aim != null && aim.IsAiming && aim.IsBallKick;
            Show(ballHalo, ballOn, ballSelected ? selectedPulse : pulse, ballSelected ? SelectedScale : HaloScale);

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
            selectionArt = InsetSprite(ButtonArt.Find(SelectionArtName));
            foreach (var disc in match.AllDiscs)
            {
                var halo = CreateHalo(disc.transform);
                if (halo != null && selectionArt != null)
                {
                    halo.sprite = selectionArt;
                    halo.sortingOrder = SelectionOrder;
                }
                discHalos[disc] = halo;
            }
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
            halo.color = new Color(HaloColor.r, HaloColor.g, HaloColor.b, alpha);
            halo.transform.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>
        /// Cópia do recorte meio pixel para dentro de cada lado. A arte do anel encosta nas bordas da célula e os vizinhos
        /// da folha são cheios: girando, a borda do quadrado pegava a coluna do vizinho e aparecia uma reta.
        /// </summary>
        private static Sprite InsetSprite(Sprite art)
        {
            if (art == null || art.packed) return art;
            var rect = art.textureRect;
            var inset = new Rect(rect.x + 0.5f, rect.y + 0.5f, rect.width - 1f, rect.height - 1f);
            var pivot = new Vector2((art.pivot.x - 0.5f) / inset.width, (art.pivot.y - 0.5f) / inset.height);
            var sprite = Sprite.Create(art.texture, inset, pivot, art.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = art.name;
            return sprite;
        }

        /// <summary>Anel de seleção na cor da arte, girando, no tamanho dos pixels do jogo qualquer que seja a escala do botão.</summary>
        private void ShowSelection(SpriteRenderer halo, bool on, float alpha, float angle)
        {
            if (halo == null) return;
            halo.enabled = on;
            if (!on) return;
            halo.color = new Color(1f, 1f, 1f, alpha);
            var t = halo.transform;
            t.rotation = Quaternion.Euler(0f, 0f, angle);
            var parent = t.parent.lossyScale;
            float size = selectionArt.pixelsPerUnit / ArtPixelsPerUnit;
            t.localScale = new Vector3(size / Mathf.Max(0.0001f, parent.x), size / Mathf.Max(0.0001f, parent.y), 1f);
        }
    }
}
