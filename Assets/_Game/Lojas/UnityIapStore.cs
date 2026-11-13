using System;
using System.Collections.Generic;
using FutebolDeBotao;
using UnityEngine;
using UnityEngine.Purchasing;

/// <summary>
/// Loja do Google Play pelo Unity IAP 5 (no editor, a loja de teste do próprio pacote). Fica fora do assembly do jogo
/// (Assembly-CSharp enxerga o pacote sozinho) e se registra em <see cref="Purchases"/> ao abrir o jogo.
/// Fluxo do IAP 5: compra → pendente → entrega no save → confirma. Compra pendente de antes (o jogo fechou no meio)
/// volta sozinha ao conectar.
/// </summary>
public sealed class UnityIapStore : IStoreProvider
{
    private StoreController store;
    private Action<bool> buying;

    public bool Ready { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (!Application.isEditor && Application.platform != RuntimePlatform.Android) return;
        Purchases.Register(new UnityIapStore());
    }

    public string Price(string productId)
    {
        var product = store != null ? store.GetProductById(productId) : null;
        return product != null && product.metadata != null ? product.metadata.localizedPriceString : string.Empty;
    }

    public async void Connect()
    {
        try
        {
            store = UnityIAPServices.StoreController();
            // Todos os eventos antes do Connect: compra pendente de antes pode chegar na hora.
            store.OnPurchasePending += OnPurchasePending;
            store.OnPurchaseConfirmed += OnPurchaseConfirmed;
            store.OnPurchaseFailed += OnPurchaseFailed;
            store.OnPurchaseDeferred += _ => Finish(false);
            store.OnStoreConnected += OnStoreConnected;
            store.OnStoreDisconnected += failure =>
            {
                Ready = false;
                Debug.LogWarning($"[Compras] Loja desconectada: {failure.Message}");
            };
            store.OnProductsFetched += _ =>
            {
                Ready = true;
                store.FetchPurchases();
            };
            store.OnProductsFetchFailed += failure => Debug.LogWarning($"[Compras] Produtos não vieram: {failure.FailureReason}");
            store.OnPurchasesFetched += orders =>
            {
                foreach (var order in orders.ConfirmedOrders) Grant(order.CartOrdered);
            };
            store.OnPurchasesFetchFailed += failure => Debug.LogWarning($"[Compras] Compras não vieram: {failure.Message}");
            await store.Connect();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Compras] Não consegui conectar: {exception.Message}");
        }
    }

    private void OnStoreConnected()
    {
        store.FetchProducts(new List<ProductDefinition> { new(Purchases.RemoveAdsId, ProductType.NonConsumable) });
    }

    private void OnPurchasePending(PendingOrder order)
    {
        // Entrega primeiro (grava no save), depois confirma; se fechar no meio, a loja manda de novo.
        Grant(order.CartOrdered);
        store.ConfirmPurchase(order);
        Finish(true);
    }

    private static void Grant(ICart cart)
    {
        if (cart == null) return;
        foreach (var item in cart.Items())
            if (item?.Product != null) Purchases.Grant(item.Product.definition.id);
    }

    private static void OnPurchaseConfirmed(Order order)
    {
        if (order is FailedOrder failed) Debug.LogWarning($"[Compras] Confirmação falhou: {failed.FailureReason} {failed.Details}");
    }

    private void OnPurchaseFailed(FailedOrder failed)
    {
        Debug.LogWarning($"[Compras] Compra não feita: {failed.FailureReason} {failed.Details}");
        Finish(false);
    }

    public void Buy(string productId, Action<bool> done)
    {
        if (store == null || !Ready)
        {
            done?.Invoke(false);
            return;
        }
        buying = done;
        store.PurchaseProduct(productId);
    }

    public void Restore(Action<bool> done)
    {
        if (store == null)
        {
            done?.Invoke(false);
            return;
        }
        store.RestoreTransactions((ok, error) =>
        {
            if (!ok) Debug.LogWarning($"[Compras] Restaurar falhou: {error}");
            store.FetchPurchases();
            done?.Invoke(ok);
        });
    }

    private void Finish(bool ok)
    {
        var done = buying;
        buying = null;
        done?.Invoke(ok);
    }
}
