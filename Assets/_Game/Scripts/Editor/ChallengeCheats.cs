using UnityEditor;
using UnityEngine;

namespace FutebolDeBotao.Editor
{
    /// <summary>Atalhos para testar os Desafios com o jogo rodando (o save só é carregado no Play).</summary>
    public static class ChallengeCheats
    {
        [MenuItem("Futebol de Botão/Teste/Desafios: completar todos")]
        private static void CompleteAll()
        {
            Challenges.DebugCompleteAll();
            Debug.Log("[Desafios] Todos completos (dia e semana).");
        }

        [MenuItem("Futebol de Botão/Teste/Desafios: sortear de novo")]
        private static void NewDraw()
        {
            Challenges.DebugNewDraw();
            Debug.Log("[Desafios] Sorteados de novo (dia e semana).");
        }

        [MenuItem("Futebol de Botão/Teste/Desafios: completar todos", true)]
        [MenuItem("Futebol de Botão/Teste/Desafios: sortear de novo", true)]
        private static bool OnlyPlaying() => Application.isPlaying;
    }
}
