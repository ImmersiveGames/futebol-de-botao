using System;
using System.Collections.Generic;

namespace FutebolDeBotao
{
    /// <summary>De onde vieram as moedas ganhas (resumo da aba Financeiro).</summary>
    public enum FinanceSource
    {
        Match,
        Tournament,
        Tutorial,
        /// <summary>Patrocínio vendido na aba Financeiro (anúncio com prêmio).</summary>
        Sponsor
    }

    /// <summary>
    /// Moedas que entram e saem, para a aba Financeiro do Meu time: soma no saldo (<see cref="Shop.AddCoins"/>), nos totais
    /// ganho/gasto, no ganho por origem e no extrato (as últimas <see cref="StatementSize"/> movimentações). Não grava:
    /// quem chama grava. O menu de teste mexe no saldo sem passar por aqui.
    /// </summary>
    public static class Finance
    {
        /// <summary>Movimentações guardadas no extrato (as mais antigas saem).</summary>
        public const int StatementSize = 30;

        public static SaveData.FinanceData Data => GameSave.Data.finance ??= new SaveData.FinanceData();

        /// <summary>Ganhou moedas (0 ou menos: nada muda).</summary>
        public static void Earn(FinanceSource source, string description, int amount)
        {
            if (amount <= 0) return;
            Shop.AddCoins(amount);
            var data = Data;
            data.earned += amount;
            switch (source)
            {
                case FinanceSource.Tournament: data.fromTournaments += amount; break;
                case FinanceSource.Tutorial: data.fromTutorial += amount; break;
                case FinanceSource.Sponsor: data.fromSponsors += amount; break;
                default: data.fromMatches += amount; break;
            }
            Add(description, amount);
        }

        /// <summary>Gastou moedas (o saldo já foi conferido por quem chama).</summary>
        public static void Spend(string description, int amount)
        {
            if (amount <= 0) return;
            Shop.AddCoins(-amount);
            Data.spent += amount;
            Add(description, -amount);
        }

        private static void Add(string description, int amount)
        {
            var entries = Data.entries ??= new List<SaveData.FinanceEntry>();
            entries.Add(new SaveData.FinanceEntry
            {
                date = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                text = description,
                amount = amount
            });
            if (entries.Count > StatementSize) entries.RemoveRange(0, entries.Count - StatementSize);
        }

        /// <summary>Data curta do extrato ("11/11"); vazio se não der para ler.</summary>
        public static string ShortDate(string date) =>
            DateTime.TryParseExact(date, "yyyy-MM-dd HH:mm", null, System.Globalization.DateTimeStyles.None, out var when)
                ? when.ToString("dd/MM")
                : string.Empty;

        /// <summary>"Vitória no Médio", "Torneio: empate no Pro"...</summary>
        public static string MatchText(int level, int outcome, bool tournament, bool walkover)
        {
            // Escrito no idioma em uso na hora (o extrato guarda o texto pronto).
            string result = outcome > 0 ? "Vitória" : outcome == 0 ? "Empate" : "Derrota";
            if (walkover && outcome != 0) result += " por W.O.";
            string id = tournament ? $"Torneio: {result.ToLowerInvariant()} no {{0}}" : $"{result} no {{0}}";
            return Txt.Format(id, Txt.Get(AiDifficulty.DisplayName((AiLevel)level)));
        }

        /// <summary>"Loja: Camisa Listrada" a partir da chave do item (camisa:, bola:, mesa:, feltro:).</summary>
        public static string ShopText(string key, string itemName)
        {
            int colon = key != null ? key.IndexOf(':') : -1;
            string kind = colon > 0 ? key.Substring(0, colon) : string.Empty;
            string category = kind switch
            {
                "camisa" => "Camisa",
                "bola" => "Bola",
                "mesa" => "Mesa",
                "feltro" => "Feltro",
                _ => string.Empty
            };
            return string.IsNullOrEmpty(category) ? Txt.Format("Loja: {0}", itemName) : Txt.Format("Loja: {0} {1}", Txt.Get(category), itemName);
        }
    }
}
