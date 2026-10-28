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
    /// (os dois viram Actors). Dois jogadores no mesmo dispositivo: ninguém entra e a partida lê os dispositivos direto.
    /// Sempre tira todo mundo antes e entra de novo alguns quadros depois, para o dispositivo já estar livre.
    /// Join pelo Player Session Join Trigger e Leave pelos Leave Command Triggers (um por vaga), todos no escopo Route.
    /// </summary>
    public sealed class MenuPlayers : MonoBehaviour
    {
        private const string Source = "futebol.menu";

        [Tooltip("Player Session Observer do Menu (escopo Route).")]
        [SerializeField] private PlayerSessionObserver session;
        [Tooltip("Join do framework (escopo Route).")]
        [SerializeField] private PlayerSessionJoinTrigger join;
        [Tooltip("Um Leave por vaga (escopo Route).")]
        [SerializeField] private PlayerSessionLeaveCommandTrigger[] leaves;

        private bool busy;

        public void Configure(PlayerSessionObserver observer, PlayerSessionJoinTrigger joinTrigger, PlayerSessionLeaveCommandTrigger[] leaveTriggers)
        {
            session = observer;
            join = joinTrigger;
            leaves = leaveTriggers;
        }

        /// <summary>Acerta os jogadores do modo escolhido e chama <paramref name="done"/> (mesmo se algo falhar).</summary>
        public void Prepare(Action done)
        {
            if (busy) return;
            StartCoroutine(PrepareRoutine(done));
        }

        private IEnumerator PrepareRoutine(Action done)
        {
            busy = true;
            if (TryGetObservation(out var observation))
            {
                bool anyLeft = false;
                foreach (var slot in observation.Slots)
                {
                    if (!slot.IsJoined) continue;
                    Leave(slot.Slot.PlayerSlotId);
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
            if (join == null)
            {
                Debug.LogWarning("[Jogadores] Falta o Player Session Join Trigger no Menu (rode \"Criar telas\").");
                return;
            }
            var request = device != null && device.added
                ? new LocalPlayerJoinRequest(Source, reason, device, DeviceChoice.SchemeFor(device))
                : new LocalPlayerJoinRequest(Source, reason);
            var outcome = join.RequestJoin(request);
            string with = device != null ? device.displayName : "sem dispositivo";
            if (outcome != null && outcome.Code == LocalPlayerJoinOutcomeCode.Joined) Debug.Log($"[Jogadores] Entrou um jogador ({reason}, {with}).");
            else Debug.LogWarning($"[Jogadores] Join falhou ({reason}, {with}): {(outcome != null ? outcome.Code.ToString() : "sem resposta")}");
        }

        private bool TryGetObservation(out PlayerSessionScopedObservationSnapshot observation)
        {
            observation = null;
            if (session != null && session.TryGetObservation(out observation)) return true;
            Debug.LogWarning($"[Jogadores] Sessão de jogadores indisponível no Menu. {(session == null ? "Falta o Player Session Observer." : session.Diagnostic)}");
            return false;
        }

        private void Leave(PlayerSlotId id)
        {
            var trigger = LeaveFor(id);
            if (trigger == null)
            {
                Debug.LogWarning($"[Jogadores] Sem Leave para {id} no Menu (rode \"Criar telas\").");
                return;
            }
            trigger.Invoke();
            var result = trigger.LastLeaveResult;
            if (result == null || !result.Succeeded) Debug.LogWarning($"[Jogadores] Leave de {id} falhou: {trigger.LastOutcome} {trigger.LastDiagnostic}");
        }

        private PlayerSessionLeaveCommandTrigger LeaveFor(PlayerSlotId id)
        {
            if (leaves == null) return null;
            foreach (var trigger in leaves)
                if (trigger != null && trigger.PlayerSlot != null && trigger.PlayerSlot.TryGetPlayerSlotId(out var slot, out _) && slot == id)
                    return trigger;
            return null;
        }
    }
}
