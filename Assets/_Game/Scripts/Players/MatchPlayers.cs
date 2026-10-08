using System.Collections.Generic;
using Immersive.Framework.PlayerParticipation;
using Immersive.Framework.PlayerSlots;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Na Partida, deixa jogar só o jogador da vez: os outros ficam bloqueados pelo framework
    /// (bloqueio de disponibilidade, IF-ADR-044) e o leitor de input deles não devolve nada.
    /// Na vez da IA o Jogador 1 fica bloqueado. No editor, se a partida abriu direto (sem o Menu), faz o Join do Jogador 1 para teste.
    /// </summary>
    public sealed class MatchPlayers : MonoBehaviour
    {
        private const string Source = "futebol.partida";
        private const float RetrySeconds = 0.5f;

        [Tooltip("Player Session Observer da Partida (escopo Activity).")]
        [SerializeField] private PlayerSessionObserver session;
        [SerializeField] private MatchController match;

        private readonly Dictionary<PlayerSlotId, PlayerGameplayAvailabilityBlockToken> blocks = new();
        private readonly Dictionary<PlayerSlotId, float> retryAt = new();
        private int occurrence = -1;
#if UNITY_EDITOR
        private bool testJoinTried;
#endif

        public void Configure(PlayerSessionObserver observer, MatchController matchController)
        {
            session = observer;
            match = matchController;
        }

        private void Update()
        {
            if (session == null || match == null) return;
            if (!session.TryGetAccess(out var access, out _)) return;
            if (!access.TryGetObservation(out var observation) || observation == null || !observation.IsAvailable) return;

            // Bloqueios valem só para a ocorrência da Activity em que foram pedidos (o Reiniciar cria outra).
            if (observation.ActivityOccurrence != occurrence)
            {
                occurrence = observation.ActivityOccurrence;
                blocks.Clear();
                retryAt.Clear();
            }

#if UNITY_EDITOR
            TryTestJoin(observation);
#endif

            foreach (var slot in observation.Slots)
            {
                if (!slot.IsJoined) continue;
                var id = slot.Slot.PlayerSlotId;
                bool blocked = blocks.TryGetValue(id, out var token) && token.IsValid;
                bool allow = CanPlay(id);
                if (allow == !blocked) continue;
                if (retryAt.TryGetValue(id, out float at) && Time.unscaledTime < at) continue;

                if (allow) Release(access, id, token);
                else Block(access, id);
            }
        }

        /// <summary>Só o humano da vez joga (o atacante na mira, o defensor no goleiro do "Vai chutar").</summary>
        private bool CanPlay(PlayerSlotId slot)
        {
            if (match.State is MatchState.Waiting or MatchState.End) return false;
            var side = CoachActor.SideFor(slot);
            return side == match.ActingSide && !match.IsAi(side);
        }

        private void Block(IPlayerSessionScopedAccess access, PlayerSlotId slot)
        {
            var result = access.RequestBlockRuntimeGameplay(slot, Source, "fora da vez");
            if (result.Succeeded)
            {
                blocks[slot] = result.BlockToken;
                retryAt.Remove(slot);
                return;
            }
            retryAt[slot] = Time.unscaledTime + RetrySeconds;
            Debug.LogWarning($"[Jogadores] Não consegui bloquear {slot}: {result.Status} {result.Message}");
        }

        private void Release(IPlayerSessionScopedAccess access, PlayerSlotId slot, PlayerGameplayAvailabilityBlockToken token)
        {
            var result = access.RequestReleaseRuntimeGameplay(token, Source, "vez do jogador");
            if (result.Succeeded || result.Status == PlayerGameplayAvailabilityBlockStatus.RejectedForeignOrStaleToken)
            {
                // Token velho: o bloqueio já não existe.
                blocks.Remove(slot);
                retryAt.Remove(slot);
                return;
            }
            retryAt[slot] = Time.unscaledTime + RetrySeconds;
            Debug.LogWarning($"[Jogadores] Não consegui liberar {slot}: {result.Status} {result.Message}");
        }

        private void OnDisable()
        {
            if (session != null && session.TryGetAccess(out var access, out _))
                foreach (var pair in blocks)
                    if (pair.Value.IsValid) access.RequestReleaseRuntimeGameplay(pair.Value, Source, "partida saiu");
            blocks.Clear();
            retryAt.Clear();
            occurrence = -1;
        }

#if UNITY_EDITOR
        /// <summary>Teste no editor: a Partida abriu direto, sem o Menu. Entra o Jogador 1 (o time de baixo).</summary>
        private void TryTestJoin(PlayerSessionScopedObservationSnapshot observation)
        {
            if (testJoinTried || MatchSession.VsAi != null) return;
            testJoinTried = true;

            foreach (var slot in observation.Slots)
                if (slot.IsJoined) return;

            if (!session.TryGetJoinAccess(out var join, out string issue))
            {
                Debug.LogWarning($"[Jogadores] Join de teste indisponível: {issue}");
                return;
            }
            var result = join.RequestJoin(new LocalPlayerJoinRequest(Source, "teste no editor: partida aberta sem o Menu"));
            Debug.Log(result.Succeeded
                ? $"[Jogadores] Join de teste: {result.Slot.PlayerSlotId} entrou."
                : $"[Jogadores] Join de teste falhou: {result.Status} {result.Message}");
        }
#endif
    }
}
