using UnityEditor;
using UnityEngine;

namespace FutebolDeBotao.Editor
{
    /// <summary>Atalhos para testar o fim da partida (prorrogação e pênaltis) com o jogo rodando.</summary>
    public static class MatchCheats
    {
        [MenuItem("Futebol de Botão/Teste/Encerrar tempo")]
        private static void EndTime()
        {
            var match = Object.FindAnyObjectByType<MatchController>();
            if (match == null)
            {
                Debug.Log("[Teste] Nenhuma partida rodando.");
                return;
            }
            match.DebugEndTime();
            Debug.Log("[Teste] Relógio zerado: a partida acaba no próximo lance.");
        }

        [MenuItem("Futebol de Botão/Teste/Empatar placar")]
        private static void TieScore()
        {
            var match = Object.FindAnyObjectByType<MatchController>();
            if (match == null)
            {
                Debug.Log("[Teste] Nenhuma partida rodando.");
                return;
            }
            match.DebugTieScore();
            Debug.Log("[Teste] Placar empatado.");
        }

        [MenuItem("Futebol de Botão/Teste/W.O. (vitória do jogador)")]
        private static void Walkover()
        {
            var match = Object.FindAnyObjectByType<MatchController>();
            if (match == null)
            {
                Debug.Log("[Teste] Nenhuma partida rodando.");
                return;
            }
            match.DebugWalkover();
            Debug.Log("[Teste] W.O.: +3 gols para o jogador e fim da partida.");
        }

        [MenuItem("Futebol de Botão/Teste/Encerrar tempo", true)]
        [MenuItem("Futebol de Botão/Teste/W.O. (vitória do jogador)", true)]
        [MenuItem("Futebol de Botão/Teste/Empatar placar", true)]
        private static bool OnlyPlaying() => Application.isPlaying;
    }
}
