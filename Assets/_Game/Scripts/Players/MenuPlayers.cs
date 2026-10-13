using System;
using System.Collections;
using Immersive.Framework.PlayerParticipation;
using Immersive.Framework.PlayerSlots;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FutebolDeBotao
{
    /// <summary>
    /// No Menu, acerta quem está na sessão antes da partida, com os dispositivos escolhidos na tela Controles.
    /// Contra a IA: só o Jogador 1. Dois jogadores com dispositivos diferentes: Jogador 1 e Jogador 2, cada um com o seu
    /// (os dois viram Actors). Dois jogadores no mesmo dispositivo: ninguém entra e a partida lê os dispositivos direto,
    /// até o framework aceitar dois jogadores no mesmo dispositivo.
    /// Sempre tira todo mundo antes e entra de novo alguns quadros depois, para o dispositivo já estar livre.
    /// </summary>
    public sealed class MenuPlayers : MonoBehaviour
    {
        private const string Source = "futebol.menu";

        [Tooltip("Player Session Observer do Menu (escopo Route).")]
        [SerializeField] private PlayerSessionObserver session;

        private bool busy;

        public void Configure(PlayerSessionObserver observer) => session = observer;

        /// <summary>Acerta os jogadores do modo escolhido e chama <paramref name="done"/> (mesmo se algo falhar).</summary>
        public void Prepare(Action done)
        {
            if (busy) return;
            StartCoroutine(PrepareRoutine(done));
        }

        private IEnumerator PrepareRoutine(Action done)
        {
            busy = true;
            if (TryGetObservation(out var access, out var observation))
            {
                bool anyLeft = false;
                foreach (var slot in observation.Slots)
                {
                    if (!slot.IsJoined) continue;
                    Leave(access, slot);
                    anyLeft = true;
                }
                // O PlayerInput de quem saiu some no fim do quadro; o dispositivo só fica livre depois.
                if (anyLeft)
                {
                    yield return null;
                    yield return null;
                }

                if (MatchSession.VsAi == true) Join(MatchSession.Player1Device, "contra a IA");
                else if (!MatchSession.SharedDevice && MatchSession.Player1Device != null && MatchSession.Player2Device != null)
                {
                    Join(MatchSession.Player1Device, "dois jogadores: jogador 1");
                    yield return null;
                    Join(MatchSession.Player2Device, "dois jogadores: jogador 2");
                }
            }
            busy = false;
            done?.Invoke();
        }

        /// <summary>Join com o dispositivo (e o esquema dele). Sem dispositivo, a Unity dá o primeiro esquema que os livres atendem.</summary>
        private void Join(InputDevice device, string reason)
        {
            if (!session.TryGetJoinAccess(out var join, out string issue))
            {
                Debug.LogWarning($"[Jogadores] Join indisponível no Menu: {issue}");
                return;
            }
            var request = device != null && device.added
                ? new LocalPlayerJoinRequest(Source, reason, device, DeviceChoice.SchemeFor(device))
                : new LocalPlayerJoinRequest(Source, reason);
            var result = join.RequestJoin(request);
            if (result.Succeeded) Debug.Log($"[Jogadores] {result.Slot.PlayerSlotId} entrou ({(device != null ? device.displayName : "sem dispositivo")}).");
            else Debug.LogWarning($"[Jogadores] Join falhou ({reason}): {result.Status} {result.Message}");
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
