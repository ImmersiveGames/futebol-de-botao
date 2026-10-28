using System.Collections.Generic;
using Immersive.Framework.PlayerParticipation;
using Immersive.Framework.PlayerSlots;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Na Partida, deixa jogar só o jogador da vez: os outros ficam bloqueados pelo framework
    /// (bloqueio de disponibilidade, IF-ADR-044) e o leitor de input deles não devolve nada.
    /// Na vez da IA o Jogador 1 fica bloqueado. Cada vaga tem o seu Gameplay Availability Block Trigger (escopo Activity),
    /// que o framework solta sozinho quando a Partida acaba ou reinicia.
    /// No editor, se a partida abriu direto (sem o Menu), faz o Join do Jogador 1 para teste.
    /// </summary>
    public sealed class MatchPlayers : MonoBehaviour
    {
        private const float RetrySeconds = 0.5f;

        [Tooltip("Player Session Observer da Partida (escopo Activity).")]
        [SerializeField] private PlayerSessionObserver session;
        [SerializeField] private MatchController match;
        [Tooltip("Um bloqueio por vaga (escopo Activity).")]
        [SerializeField] private PlayerGameplayAvailabilityBlockTrigger[] blocks;
        [Tooltip("Join de teste no editor (escopo Activity).")]
        [SerializeField] private PlayerSessionJoinTrigger testJoin;

        private readonly Dictionary<PlayerSlotId, float> retryAt = new();
#if UNITY_EDITOR
        private const string Source = "futebol.partida";
        private bool testJoinTried;
#endif

        public void Configure(PlayerSessionObserver observer, MatchController matchController,
            PlayerGameplayAvailabilityBlockTrigger[] blockTriggers, PlayerSessionJoinTrigger testJoinTrigger)
        {
            session = observer;
            match = matchController;
            blocks = blockTriggers;
            testJoin = testJoinTrigger;
        }

        private void Update()
        {
            if (session == null || match == null) return;
            if (!session.TryGetObservation(out var observation)) return;

#if UNITY_EDITOR
            TryTestJoin(observation);
#endif

            foreach (var slot in observation.Slots)
            {
                if (!slot.IsJoined) continue;
                var id = slot.Slot.PlayerSlotId;
                var block = BlockFor(id);
                if (block == null) continue;
                bool allow = CanPlay(id);
                if (allow == !block.OwnsActiveBlock) continue;
                if (retryAt.TryGetValue(id, out float at) && Time.unscaledTime < at) continue;

                if (allow) block.RequestRelease();
                else block.RequestBlock();

                if (allow == !block.OwnsActiveBlock)
                {
                    retryAt.Remove(id);
                    continue;
                }
                retryAt[id] = Time.unscaledTime + RetrySeconds;
                Debug.LogWarning($"[Jogadores] Não consegui {(allow ? "liberar" : "bloquear")} {id}: {block.LastDiagnostic}");
            }
        }

        /// <summary>Só o humano da vez joga (o atacante na mira, o defensor no goleiro do "Pro Gol!").</summary>
        private bool CanPlay(PlayerSlotId slot)
        {
            if (match.State is MatchState.Waiting or MatchState.End) return false;
            var side = CoachActor.SideFor(slot);
            return side == match.ActingSide && !match.IsAi(side);
        }

        private PlayerGameplayAvailabilityBlockTrigger BlockFor(PlayerSlotId id)
        {
            if (blocks == null) return null;
            foreach (var block in blocks)
                if (block != null && block.PlayerSlot != null && block.PlayerSlot.TryGetPlayerSlotId(out var slot, out _) && slot == id)
                    return block;
            return null;
        }

        private void OnDisable() => retryAt.Clear();

#if UNITY_EDITOR
        /// <summary>Teste no editor: a Partida abriu direto, sem o Menu. Entra o Jogador 1 (o time de baixo).</summary>
        private void TryTestJoin(PlayerSessionScopedObservationSnapshot observation)
        {
            if (testJoinTried || MatchSession.VsAi != null) return;
            testJoinTried = true;

            foreach (var slot in observation.Slots)
                if (slot.IsJoined) return;

            if (testJoin == null)
            {
                Debug.LogWarning("[Jogadores] Falta o Join de teste na Partida (rode \"Criar telas\").");
                return;
            }
            var outcome = testJoin.RequestJoin(new LocalPlayerJoinRequest(Source, "teste no editor: partida aberta sem o Menu"));
            Debug.Log(outcome != null && outcome.Code == LocalPlayerJoinOutcomeCode.Joined
                ? "[Jogadores] Join de teste: entrou o Jogador 1."
                : $"[Jogadores] Join de teste falhou: {(outcome != null ? outcome.Code.ToString() : "sem resposta")}");
        }
#endif
    }
}
