using System.Collections;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Fogos de artifício nas pontas da faixa de mensagem: no gol de quem joga (contra a IA, só os do jogador) e na
    /// vitória de quem joga. Um em cada ponta (esquerda na 1ª cor do time, direita na 2ª) e dois no meio, defasados.
    /// A folha (Resources/Efeitos/Fogos_Sheet, quadros quadrados lado a lado, em branco/cinza) é recortada aqui; sem ela,
    /// nada aparece. Não roda no tutorial.
    /// </summary>
    public sealed class GoalFireworks : MonoBehaviour
    {
        private const string SheetPath = "Efeitos/Fogos_Sheet";
        private const float PixelsPerUnit = 32f;
        /// <summary>Altura (em pixels da arte, contando de cima) onde o fogo estoura: vira o pivô do quadro.</summary>
        private const float BurstPixelFromTop = 28f;
        private const float FrameSeconds = 0.07f;
        private const float SideX = 2.4f;
        /// <summary>Os dois do meio: mais perto do centro e estourando um pouco mais alto.</summary>
        private const float MiddleX = 0.8f;
        private const float MiddleRaise = 0.35f;
        /// <summary>O estouro fica acima da faixa (para quem lê a mensagem).</summary>
        private const float BurstAboveMessage = 1.3f;
        private const float SecondFireworkDelay = 0.25f;
        private const float RepeatDelay = 0.55f;
        private const int SortingOrder = 96;

        /// <summary>Um foguete saiu (o som de fogos toca aqui, em <see cref="MatchSounds"/>).</summary>
        public static event System.Action Launched;

        private MatchController match;
        private System.Func<TeamSide, Vector3> messagePosition;
        private Sprite[] frames;
        private bool loaded;
        private MatchState lastState;

        public void Configure(MatchController controller, System.Func<TeamSide, Vector3> messageAt)
        {
            match = controller;
            messagePosition = messageAt;
            if (isActiveAndEnabled) match.Noticed += OnNoticed;
        }

        private void OnEnable()
        {
            if (match != null) match.Noticed += OnNoticed;
        }

        private void OnDisable()
        {
            if (match != null) match.Noticed -= OnNoticed;
        }

        private void OnNoticed(MatchNotice notice)
        {
            if (notice == MatchNotice.Goal) Celebrate(match.LastScorer);
        }

        private void Update()
        {
            if (match == null) return;
            var state = match.State;
            if (state == MatchState.End && lastState != MatchState.End)
            {
                var winner = Winner();
                if (winner != null) Celebrate(winner.Value);
            }
            lastState = state;
        }

        private static TeamSide? Winner()
        {
            if (MatchSession.LastResult is not MatchResult result) return null;
            int bottom = result.Bottom, top = result.Top;
            if (bottom == top && result.HasPenalties)
            {
                bottom = result.PenaltiesBottom;
                top = result.PenaltiesTop;
            }
            if (bottom == top) return null;
            return bottom > top ? TeamSide.Bottom : TeamSide.Top;
        }

        private void Celebrate(TeamSide side)
        {
            if (match.Tutorial != null || match.IsAi(side) || !Load()) return;
            var primary = MatchSession.PrimaryColor(side);
            var secondary = MatchSession.SecondaryColor(side);
            // Pontas (esquerda na 1ª cor, direita na 2ª) e, entre elas, os dois do meio com as cores trocadas.
            StartCoroutine(Launch(side, -SideX, 0f, primary, 0f));
            StartCoroutine(Launch(side, SideX, 0f, secondary, SecondFireworkDelay));
            StartCoroutine(Launch(side, MiddleX, MiddleRaise, primary, SecondFireworkDelay * 0.5f));
            StartCoroutine(Launch(side, -MiddleX, MiddleRaise, secondary, SecondFireworkDelay * 1.5f));
            StartCoroutine(Launch(side, -SideX, 0f, primary, RepeatDelay + SecondFireworkDelay));
            StartCoroutine(Launch(side, SideX, 0f, secondary, RepeatDelay));
            StartCoroutine(Launch(side, -MiddleX, MiddleRaise, secondary, RepeatDelay + SecondFireworkDelay * 0.5f));
            StartCoroutine(Launch(side, MiddleX, MiddleRaise, primary, RepeatDelay + SecondFireworkDelay * 1.5f));
        }

        /// <summary>Um fogo em <paramref name="x"/> (negativo = esquerda de quem lê) acima da mensagem de quem comemora.</summary>
        private IEnumerator Launch(TeamSide side, float x, float raise, Color color, float delay)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            // O jogador de cima lê a mensagem girada: o fogo também (sobe para o lado dele).
            bool flipped = side == TeamSide.Top && messagePosition(TeamSide.Top).y > 0.01f;
            float up = flipped ? -1f : 1f;
            Vector3 message = messagePosition(side);
            var go = new GameObject("Fogo");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(x * up, message.y + (BurstAboveMessage + raise) * up, 0f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, flipped ? 180f : 0f);
            Launched?.Invoke();
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = SortingOrder;
            renderer.color = color;
            foreach (var frame in frames)
            {
                renderer.sprite = frame;
                yield return new WaitForSecondsRealtime(FrameSeconds);
            }
            Destroy(go);
        }

        private bool Load()
        {
            if (loaded) return frames != null && frames.Length > 0;
            loaded = true;
            var sheet = Resources.Load<Texture2D>(SheetPath);
            if (sheet == null) return false;
            sheet.filterMode = FilterMode.Point;
            int size = sheet.height;
            int count = size > 0 ? sheet.width / size : 0;
            frames = new Sprite[count];
            var pivot = new Vector2(0.5f, (size - BurstPixelFromTop) / size);
            for (int i = 0; i < count; i++)
            {
                frames[i] = Sprite.Create(sheet, new Rect(i * size, 0f, size, size), pivot, PixelsPerUnit);
                frames[i].hideFlags = HideFlags.DontSave;
            }
            return count > 0;
        }
    }
}
