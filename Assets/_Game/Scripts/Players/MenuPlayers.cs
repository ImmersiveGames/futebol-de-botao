using Immersive.Framework.PlayerParticipation;
using Immersive.Framework.PlayerSlots;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// No Menu, acerta quem está na sessão antes da partida.
    /// Contra a IA: só o Jogador 1 (Azul, embaixo). 2 jogadores: por enquanto ninguém entra e a partida lê o
    /// mouse/teclado direto, até o framework aceitar dois jogadores no mesmo dispositivo.
    /// </summary>
    public sealed class MenuPlayers : MonoBehaviour
    {
        private const string Source = "futebol.menu";

        [Tooltip("Player Session Observer do Menu (escopo Route).")]
        [SerializeField] private PlayerSessionObserver session;

        public void Configure(PlayerSessionObserver observer) => session = observer;

        /// <summary>Contra a IA: garante o Jogador 1 e tira o Jogador 2.</summary>
        public void PrepareVsAi()
        {
            if (!TryGetObservation(out var access, out var observation)) return;

            bool player1Joined = false;
            foreach (var slot in observation.Slots)
            {
                if (!slot.IsJoined) continue;
                if (slot.Slot.PlayerSlotId == PlayerSlotId.Player1) player1Joined = true;
                else Leave(access, slot);
            }
            if (player1Joined) return;

            if (!session.TryGetJoinAccess(out var join, out string issue))
            {
                Debug.LogWarning($"[Jogadores] Join indisponível no Menu: {issue}");
                return;
            }
            // Sem dispositivo: a Unity dá ao Jogador 1 o primeiro esquema que os dispositivos livres atendem (teclado e mouse ou toque).
            var result = join.RequestJoin(new LocalPlayerJoinRequest(Source, "contra a IA"));
            if (result.Succeeded) Debug.Log($"[Jogadores] {result.Slot.PlayerSlotId} entrou.");
            else Debug.LogWarning($"[Jogadores] Join do Jogador 1 falhou: {result.Status} {result.Message}");
        }

        /// <summary>2 jogadores: tira todo mundo (a partida volta a ler o mouse/teclado direto).</summary>
        public void PrepareTwoPlayers()
        {
            if (!TryGetObservation(out var access, out var observation)) return;
            foreach (var slot in observation.Slots)
                if (slot.IsJoined) Leave(access, slot);
        }

        private bool TryGetObservation(out IPlayerSessionScopedAccess access, out PlayerSessionScopedObservationSnapshot observation)
        {
            observation = null;
            access = null;
            if (session == null || !session.TryGetAccess(out access, out string issue))
            {
                Debug.LogWarning($"[Jogadores] Sessão de jogadores indisponível no Menu. {(session == null ? "Falta o Player Session Observer." : "")}");
                return false;
            }
            return access.TryGetObservation(out observation) && observation != null && observation.IsAvailable;
        }

        private static void Leave(IPlayerSessionScopedAccess access, PlayerSessionScopedSlotObservation slot)
        {
            var result = access.RequestLeave(new SessionPlayerLeaveRequest(slot.Slot.PlayerSlotId, slot.Slot.Revision, Source, "modo do Menu"));
            if (!result.Succeeded) Debug.LogWarning($"[Jogadores] Leave de {slot.Slot.PlayerSlotId} falhou: {result.Status} {result.Message}");
        }
    }
}
