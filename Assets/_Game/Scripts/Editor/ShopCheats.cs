using UnityEditor;
using UnityEngine;

namespace FutebolDeBotao.Editor
{
    /// <summary>Atalhos para testar a Loja com o jogo rodando (o save só é carregado no Play).</summary>
    public static class ShopCheats
    {
        [MenuItem("Futebol de Botão/Teste/Dar 100 moedas")]
        private static void GiveCoins()
        {
            Shop.AddCoins(100);
            GameSave.Save("teste: moedas");
            Debug.Log($"[Loja] +100 moedas (saldo: {Shop.Coins}).");
        }

        [MenuItem("Futebol de Botão/Teste/Zerar moedas")]
        private static void ClearCoins()
        {
            Shop.AddCoins(-Shop.Coins);
            GameSave.Save("teste: zerar moedas");
            Debug.Log("[Loja] Moedas zeradas.");
        }

        [MenuItem("Futebol de Botão/Teste/Zerar compras")]
        private static void ClearPurchases()
        {
            Shop.ResetPurchases();
            Debug.Log("[Loja] Compras apagadas.");
        }

        [MenuItem("Futebol de Botão/Teste/Dar 100 moedas", true)]
        [MenuItem("Futebol de Botão/Teste/Zerar moedas", true)]
        [MenuItem("Futebol de Botão/Teste/Zerar compras", true)]
        private static bool OnlyPlaying() => Application.isPlaying;
    }
}
