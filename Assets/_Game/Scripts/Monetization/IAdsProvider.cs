using System;

namespace FutebolDeBotao
{
    /// <summary>Quem mostra os anúncios de verdade (AdMob) ou de mentira (teste no editor).</summary>
    public interface IAdsProvider
    {
        /// <summary>Configura para a faixa de idade e começa a carregar os anúncios.</summary>
        void Initialize(AgeGroup group);

        /// <summary>Anúncio entre partidas; <paramref name="closed"/> é chamado quando fecha (ou se não deu para mostrar).</summary>
        void ShowInterstitial(Action closed);

        /// <summary>Anúncio com prêmio; <paramref name="done"/> diz se o jogador ganhou o prêmio (assistiu até o fim).</summary>
        void ShowRewarded(Action<bool> done);
    }
}
