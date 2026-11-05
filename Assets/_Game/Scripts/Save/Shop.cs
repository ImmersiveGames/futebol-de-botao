namespace FutebolDeBotao
{
    /// <summary>
    /// Moedas e compras, no <see cref="GameSave"/>: o saldo fica no Meu time (<see cref="SaveData.ProfileData.coins"/>) e
    /// os itens comprados na lista <see cref="SaveData.unlocked"/>, como "camisa:id", "bola:id", "mesa:id" e "feltro:id".
    /// Partida terminada contra a IA paga pelo nível e resultado (GDD); abandono e 2 jogadores não pagam.
    /// </summary>
    public static class Shop
    {
        /// <summary>Moedas por vitória, empate e derrota em cada nível (Fácil, Médio, Difícil).</summary>
        private static readonly int[,] Rewards = { { 5, 3, 1 }, { 10, 5, 2 }, { 12, 6, 3 } };

        public static int Coins => PlayerProfile.Data.coins;

        public static bool Owns(string key) => GameSave.Data.unlocked != null && GameSave.Data.unlocked.Contains(key);

        /// <summary>Moedas de uma partida terminada contra a IA no nível (0 a 2) com o placar do jogador e da IA.</summary>
        public static int Reward(int level, int playerScore, int aiScore)
        {
            if (level < 0 || level > 2) return 0;
            int result = playerScore > aiScore ? 0 : playerScore == aiScore ? 1 : 2;
            return Rewards[level, result];
        }

        /// <summary>Soma moedas (sem gravar; quem chama grava).</summary>
        public static void AddCoins(int amount) => PlayerProfile.Data.coins = System.Math.Max(0, PlayerProfile.Data.coins + amount);

        /// <summary>Compra o item: tira as moedas, marca como comprado e grava. Falso se já tem ou não dá.</summary>
        public static bool Buy(string key, int price)
        {
            if (Owns(key) || Coins < price) return false;
            AddCoins(-price);
            GameSave.Data.stats.coinsSpent += price;
            GameSave.Data.stats.itemsBought++;
            GameSave.Data.unlocked ??= new System.Collections.Generic.List<string>();
            GameSave.Data.unlocked.Add(key);
            GameSave.Save("compra " + key);
            return true;
        }

        /// <summary>Preço do item do código (camisa:, bola:, mesa: ou feltro: + id); -1 se ele não existir mais.</summary>
        public static int PriceOf(string key)
        {
            foreach (var item in ShirtPattern.All()) if (item.ShopKey == key) return item.price;
            foreach (var item in BallTheme.All()) if (item.ShopKey == key) return item.price;
            foreach (var item in TableTheme.All()) if (item.ShopKey == key) return item.price;
            foreach (var item in FeltTheme.All()) if (item.ShopKey == key) return item.price;
            return -1;
        }

        /// <summary>Teste: apaga todas as compras.</summary>
        public static void ResetPurchases()
        {
            GameSave.Data.unlocked?.Clear();
            GameSave.Save("zerar compras");
        }
    }
}
