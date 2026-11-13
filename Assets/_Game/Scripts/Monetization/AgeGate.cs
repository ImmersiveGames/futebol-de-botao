using System;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>Faixa de idade que decide como os anúncios são pedidos.</summary>
    public enum AgeGroup
    {
        /// <summary>Menor de 13: anúncio para crianças, não personalizado e classificação G.</summary>
        Child,
        /// <summary>13 a 17: abaixo da idade de consentimento (anúncio não personalizado, sem pedir consentimento).</summary>
        Teen,
        /// <summary>18 ou mais: pede o consentimento do Google (UMP) quando a lei exigir.</summary>
        Adult
    }

    /// <summary>
    /// Tela de idade (ano de nascimento), perguntada uma vez na primeira abertura, só onde há anúncios (Android e editor).
    /// O ano fica só no aparelho (<see cref="SaveData.AdsData.birthYear"/>). Sem o mês, a idade é contada para baixo
    /// (quem nasceu em 2013 tem 12 até provar o contrário), para nunca tratar uma criança como mais velha.
    /// </summary>
    public static class AgeGate
    {
        public const int ChildUntil = 13;
        public const int AdultFrom = 18;
        public const int OldestYear = 1920;

        private static SaveData.AdsData Data => GameSave.Data.ads ??= new SaveData.AdsData();

        public static bool Answered => Data.birthYear > 0;

        /// <summary>A pergunta só aparece onde há anúncios.</summary>
        public static bool Needed => Ads.Supported && !Answered;

        public static int CurrentYear => DateTime.Now.Year;

        /// <summary>Idade mínima possível para o ano escolhido.</summary>
        public static int Age => Answered ? Mathf.Max(0, CurrentYear - Data.birthYear - 1) : 0;

        /// <summary>Sem resposta conta como criança (o mais restrito).</summary>
        public static AgeGroup Group => !Answered || Age < ChildUntil ? AgeGroup.Child : Age < AdultFrom ? AgeGroup.Teen : AgeGroup.Adult;

        public static void Set(int birthYear)
        {
            Data.birthYear = Mathf.Clamp(birthYear, OldestYear, CurrentYear);
            GameSave.WriteOnly("idade");
        }
    }
}
