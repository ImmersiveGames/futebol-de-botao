#if PROGOL_ADMOB
using System;
using FutebolDeBotao;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

/// <summary>
/// Anúncios do AdMob (Google Mobile Ads), ligados pelo símbolo PROGOL_ADMOB depois de instalar o pacote. Configura
/// pela faixa de idade da tela de idade:
/// - menor de 13: para crianças (child-directed), não personalizado, classificação G;
/// - 13 a 17: abaixo da idade de consentimento, não personalizado, classificação PG;
/// - 18+: pede o consentimento do Google (UMP) quando a lei exige, classificação PG.
/// Mantém um anúncio entre partidas e um com prêmio carregados; sem anúncio pronto, segue o jogo sem mostrar nada.
/// </summary>
public sealed class AdMobAds : IAdsProvider
{
    // IDs DE TESTE do Google. Trocar pelos blocos de anúncio da conta do AdMob antes de publicar.
    private const string InterstitialId = "ca-app-pub-3940256099942544/1033173712";
    private const string RewardedId = "ca-app-pub-3940256099942544/5224354917";
    private const string BannerId = "ca-app-pub-3940256099942544/9214589741";

    private AgeGroup group;
    private bool started;
    private bool loadingInterstitial, loadingRewarded;
    private InterstitialAd interstitial;
    private RewardedAd rewarded;
    private BannerView banner;
    private bool bannerWanted;
    private bool bannerLoaded;
    private bool bannerShown;

    public float BannerHeightPixels => banner != null && bannerLoaded && bannerShown ? banner.GetHeightInPixels() + Ads.TopInsetPixels : 0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register() => Ads.Factory = () => new AdMobAds();

    public void Initialize(AgeGroup ageGroup)
    {
        group = ageGroup;
        MobileAds.RaiseAdEventsOnUnityMainThread = true;
        MobileAds.SetRequestConfiguration(new RequestConfiguration
        {
            TagForChildDirectedTreatment = group == AgeGroup.Child ? TagForChildDirectedTreatment.True : TagForChildDirectedTreatment.Unspecified,
            TagForUnderAgeOfConsent = group != AgeGroup.Adult ? TagForUnderAgeOfConsent.True : TagForUnderAgeOfConsent.Unspecified,
            MaxAdContentRating = group == AgeGroup.Child ? MaxAdContentRating.G : MaxAdContentRating.PG
        });

        if (group != AgeGroup.Adult)
        {
            Start();
            return;
        }
        // Adulto: o formulário de consentimento aparece só onde a lei pede (Europa, Reino Unido...).
        ConsentInformation.Update(new ConsentRequestParameters { TagForUnderAgeOfConsent = false }, error =>
        {
            if (error != null) Debug.LogWarning($"[Anúncios] Consentimento: {error.Message}");
            ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
            {
                if (formError != null) Debug.LogWarning($"[Anúncios] Formulário de consentimento: {formError.Message}");
                if (ConsentInformation.CanRequestAds()) Start();
            });
        });
    }

    private void Start()
    {
        if (started) return;
        started = true;
        MobileAds.Initialize(_ =>
        {
            LoadInterstitial();
            LoadRewarded();
            if (bannerWanted) ShowBanner();
        });
    }

    private AdRequest Request()
    {
        var request = new AdRequest();
        // Menores: anúncio não personalizado.
        if (group != AgeGroup.Adult) request.Extras.Add("npa", "1");
        return request;
    }

    private void LoadInterstitial()
    {
        if (!started || loadingInterstitial || interstitial != null) return;
        loadingInterstitial = true;
        InterstitialAd.Load(InterstitialId, Request(), (ad, error) =>
        {
            loadingInterstitial = false;
            if (error != null || ad == null)
            {
                Debug.LogWarning($"[Anúncios] Entre partidas não carregou: {error?.GetMessage()}");
                return;
            }
            interstitial = ad;
        });
    }

    private void LoadRewarded()
    {
        if (!started || loadingRewarded || rewarded != null) return;
        loadingRewarded = true;
        RewardedAd.Load(RewardedId, Request(), (ad, error) =>
        {
            loadingRewarded = false;
            if (error != null || ad == null)
            {
                Debug.LogWarning($"[Anúncios] Com prêmio não carregou: {error?.GetMessage()}");
                return;
            }
            rewarded = ad;
        });
    }

    /// <summary>Banner adaptativo na largura da tela, no topo; criado uma vez e só mostrado/escondido depois.</summary>
    public void ShowBanner()
    {
        bannerWanted = true;
        if (!started) return;
        if (banner == null)
        {
            var size = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(AdSize.FullWidth);
            banner = new BannerView(BannerId, size, AdPosition.Top);
            banner.OnBannerAdLoaded += () => bannerLoaded = true;
            banner.OnBannerAdLoadFailed += error => Debug.LogWarning($"[Anúncios] Banner não carregou: {error?.GetMessage()}");
            banner.LoadAd(Request());
        }
        banner.Show();
        bannerShown = true;
    }

    public void HideBanner()
    {
        bannerWanted = false;
        bannerShown = false;
        banner?.Hide();
    }

    public void ShowInterstitial(Action closed)
    {
        var ad = interstitial;
        if (ad == null || !ad.CanShowAd())
        {
            LoadInterstitial();
            closed?.Invoke();
            return;
        }
        interstitial = null;
        bool finished = false;
        void Finish()
        {
            if (finished) return;
            finished = true;
            ad.Destroy();
            LoadInterstitial();
            closed?.Invoke();
        }
        ad.OnAdFullScreenContentClosed += Finish;
        ad.OnAdFullScreenContentFailed += _ => Finish();
        ad.Show();
    }

    public void ShowRewarded(Action<bool> done)
    {
        var ad = rewarded;
        if (ad == null || !ad.CanShowAd())
        {
            LoadRewarded();
            done?.Invoke(false);
            return;
        }
        rewarded = null;
        bool earned = false;
        bool finished = false;
        void Finish()
        {
            if (finished) return;
            finished = true;
            ad.Destroy();
            LoadRewarded();
            done?.Invoke(earned);
        }
        ad.OnAdFullScreenContentClosed += Finish;
        ad.OnAdFullScreenContentFailed += _ => Finish();
        ad.Show(_ => earned = true);
    }
}
#endif
