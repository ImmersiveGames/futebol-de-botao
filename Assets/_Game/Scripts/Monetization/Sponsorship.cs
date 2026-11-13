using System;

namespace FutebolDeBotao
{
    /// <summary>
    /// "Vender patrocínio" na aba Financeiro do Meu time: um anúncio com prêmio rende <see cref="Reward"/> moedas, até
    /// <see cref="PerDay"/> por dia (conta pela data do aparelho).
    /// </summary>
    public static class Sponsorship
    {
        public const int Reward = 5;
        public const int PerDay = 3;

        private static SaveData.AdsData Data => GameSave.Data.ads ??= new SaveData.AdsData();

        private static string Today => DateTime.Now.ToString("yyyy-MM-dd");

        public static int RemainingToday => Data.sponsorDay == Today ? Math.Max(0, PerDay - Data.sponsorsToday) : PerDay;

        /// <summary>Mostra o anúncio e, se assistiu, paga e grava. <paramref name="done"/> diz se pagou.</summary>
        public static void Sell(Action<bool> done)
        {
            if (RemainingToday <= 0)
            {
                done?.Invoke(false);
                return;
            }
            Ads.ShowRewarded(rewarded =>
            {
                if (rewarded)
                {
                    var data = Data;
                    if (data.sponsorDay != Today)
                    {
                        data.sponsorDay = Today;
                        data.sponsorsToday = 0;
                    }
                    data.sponsorsToday++;
                    Finance.Earn(FinanceSource.Sponsor, Txt.Get("Patrocínio"), Reward);
                    GameSave.WriteOnly("patrocínio");
                }
                done?.Invoke(rewarded);
            });
        }
    }
}
