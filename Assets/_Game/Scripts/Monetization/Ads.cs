using System;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Anúncios do jogo (só Android; no editor e em build de desenvolvimento, anúncios de teste na tela,
    /// <see cref="SimulatedAds"/>). Começam depois da tela de idade, que decide como são pedidos (<see cref="AgeGate"/>).
    /// Entre partidas: um a cada <see cref="InterstitialEvery"/> saídas do Resultado, e nenhum para quem comprou
    /// "remover anúncios". Com prêmio (dobrar moedas, dobrar prêmio, patrocínio): sempre que o jogador pedir.
    /// O AdMob (Assets/_Game/Lojas/AdMobAds.cs, com o símbolo PROGOL_ADMOB) se registra em <see cref="Factory"/>;
    /// fora do editor, ele é quem mostra os anúncios.
    /// </summary>
    public static class Ads
    {
        public const int InterstitialEvery = 2;

        private static IAdsProvider provider;

        /// <summary>Cria o provedor de verdade (o AdMob registra aqui antes da primeira cena).</summary>
        public static Func<IAdsProvider> Factory;
        private static bool showing;
        /// <summary>Telas abertas que querem o banner (Menu, Resultado). Na troca de cena as duas podem existir juntas.</summary>
        private static readonly System.Collections.Generic.HashSet<object> bannerOwners = new();
        private static bool BannerWanted => bannerOwners.Count > 0;

        /// <summary>Lugar com anúncios: Android (e o editor, para testar). No navegador (itch) não há.</summary>
        public static bool Supported => Application.isEditor || Application.platform == RuntimePlatform.Android;

        /// <summary>Já dá para pedir anúncio com prêmio (tem quem mostre e a idade foi respondida).</summary>
        public static bool RewardedAvailable => provider != null;

        public static bool Removed => GameSave.Data.ads != null && GameSave.Data.ads.adsRemoved;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            provider = null;
            showing = false;
            bannerOwners.Clear();
            Initialize();
        }

        /// <summary>Liga os anúncios se a idade já foi respondida (no começo do jogo ou ao confirmar a tela de idade).</summary>
        public static void Initialize()
        {
            if (provider != null || !Supported || !AgeGate.Answered) return;
            provider = Create();
            provider?.Initialize(AgeGate.Group);
            // O Menu pediu o banner antes da tela de idade ser respondida.
            if (BannerWanted && !Removed) provider?.ShowBanner();
        }

        private static IAdsProvider Create()
        {
            if (Factory != null && !Application.isEditor) return Factory();
            // Sem o AdMob: anúncio de teste só no editor e em build de desenvolvimento; no build final, nenhum.
            return Application.isEditor || Debug.isDebugBuild ? new SimulatedAds() : null;
        }

        /// <summary>
        /// Saída do Resultado: conta a partida e, na vez, mostra o anúncio entre partidas; depois chama <paramref name="then"/>.
        /// <paramref name="watchedReward"/>: o jogador acabou de ver um anúncio com prêmio nesta tela, então não leva outro
        /// (a conta recomeça).
        /// </summary>
        public static void AfterMatch(Action then, bool watchedReward = false)
        {
            var data = GameSave.Data.ads;
            if (provider == null || Removed || showing || data == null)
            {
                then?.Invoke();
                return;
            }
            if (watchedReward) data.matchesSinceInterstitial = 0;
            else data.matchesSinceInterstitial++;
            bool show = data.matchesSinceInterstitial >= InterstitialEvery;
            if (show) data.matchesSinceInterstitial = 0;
            GameSave.WriteOnly("anúncio");
            if (!show)
            {
                then?.Invoke();
                return;
            }
            showing = true;
            provider.ShowInterstitial(() =>
            {
                showing = false;
                OverlayUi.SafeInvoke(then);
            });
        }

        /// <summary>Anúncio com prêmio; <paramref name="done"/> recebe verdadeiro se é para dar o prêmio.</summary>
        public static void ShowRewarded(Action<bool> done)
        {
            if (provider == null || showing)
            {
                done?.Invoke(false);
                return;
            }
            showing = true;
            provider.ShowRewarded(rewarded =>
            {
                showing = false;
                OverlayUi.SafeInvoke(() => done?.Invoke(rewarded));
            });
        }

        /// <summary>Banner no topo (Menu e Resultado). Quem comprou "remover anúncios" não vê.</summary>
        public static void ShowBanner(object owner)
        {
            if (owner == null) return;
            bannerOwners.Add(owner);
            if (provider == null || Removed) return;
            provider.ShowBanner();
        }

        /// <summary>A tela que pediu o banner fechou (entrou na partida, por exemplo).</summary>
        public static void HideBanner(object owner)
        {
            bannerOwners.Remove(owner);
            if (!BannerWanted) provider?.HideBanner();
        }

        /// <summary>Comprou "remover anúncios" (ou a loja do Google devolveu a compra ao reinstalar).</summary>
        public static void MarkRemoved()
        {
            if (Removed) return;
            GameSave.Data.ads.adsRemoved = true;
            provider?.HideBanner();
            GameSave.WriteOnly("remover anúncios");
        }
    }
}
