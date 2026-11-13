using System;

namespace FutebolDeBotao
{
    /// <summary>A loja de dinheiro de verdade (Google Play pelo Unity IAP), registrada de fora do assembly do jogo.</summary>
    public interface IStoreProvider
    {
        /// <summary>Conectou e já sabe os produtos (preço).</summary>
        bool Ready { get; }

        /// <summary>Preço no formato da loja ("R$ 9,90"); vazio enquanto não sabe.</summary>
        string Price(string productId);

        void Connect();

        /// <summary>Abre a compra; <paramref name="done"/> diz se comprou (a entrega vem por <see cref="Purchases.Grant"/>).</summary>
        void Buy(string productId, Action<bool> done);

        /// <summary>Pede à loja as compras já feitas (trocou de celular, reinstalou).</summary>
        void Restore(Action<bool> done);
    }
}
