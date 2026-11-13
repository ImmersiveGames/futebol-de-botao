using System;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Compras com dinheiro de verdade: por enquanto só "remover anúncios" (<see cref="RemoveAdsId"/>, produto não
    /// consumível no Play Console). Quem fala com a loja é o <see cref="IStoreProvider"/> registrado pelo
    /// UnityIapStore (Assets/_Game/Lojas, fora do assembly do jogo para usar o pacote do Unity IAP sem referência).
    /// A loja do Google devolve a compra sozinha ao reinstalar; "Restaurar compras" pede de novo.
    /// </summary>
    public static class Purchases
    {
        public const string RemoveAdsId = "remover_anuncios";

        private static IStoreProvider provider;

        /// <summary>Chamado pela loja ao abrir o jogo (Android e editor).</summary>
        public static void Register(IStoreProvider store)
        {
            if (store == null || provider != null) return;
            provider = store;
            try
            {
                provider.Connect();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Compras] Não consegui conectar à loja: {exception.Message}");
            }
        }

        /// <summary>Tem loja e ela já conhece os produtos.</summary>
        public static bool Ready => provider != null && provider.Ready;

        /// <summary>Tem loja (mesmo que ainda conectando): os botões aparecem.</summary>
        public static bool Supported => provider != null;

        /// <summary>Motivo da loja não estar pronta (diagnóstico).</summary>
        public static string Status => provider != null ? provider.Status : "sem loja";

        /// <summary>Loja não pronta: tenta conectar/pedir os produtos de novo.</summary>
        public static void Retry()
        {
            if (provider == null || provider.Ready) return;
            try
            {
                provider.Retry();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Compras] Nova tentativa falhou: {exception.Message}");
            }
        }

        public static string RemoveAdsPrice => provider != null ? provider.Price(RemoveAdsId) : string.Empty;

        public static void BuyRemoveAds(Action<bool> done)
        {
            if (!Ready || Ads.Removed)
            {
                done?.Invoke(false);
                return;
            }
            provider.Buy(RemoveAdsId, ok => OverlayUi.SafeInvoke(() => done?.Invoke(ok)));
        }

        public static void Restore(Action<bool> done)
        {
            if (provider == null)
            {
                done?.Invoke(false);
                return;
            }
            provider.Restore(ok => OverlayUi.SafeInvoke(() => done?.Invoke(ok)));
        }

        /// <summary>A loja entregou um produto (compra nova ou devolvida): libera no save.</summary>
        public static void Grant(string productId)
        {
            if (productId == RemoveAdsId) Ads.MarkRemoved();
            else Debug.LogWarning($"[Compras] Produto desconhecido: {productId}");
        }
    }
}
