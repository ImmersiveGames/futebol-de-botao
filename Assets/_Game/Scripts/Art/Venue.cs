using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>Uma montagem de mesa para a partida: mesa, feltro, cor das traves (posição na mesa) e bola.</summary>
    public sealed class VenueSetup
    {
        public string TableId;
        public string FeltId;
        public int GoalColor;
        public string BallId;

        public TableTheme Table => TableTheme.Find(TableId);
        public FeltTheme Felt => FeltTheme.Find(FeltId);
        public BallTheme Ball => BallTheme.Find(BallId);

        public VenueSetup Clone() => (VenueSetup)MemberwiseClone();
    }

    /// <summary>
    /// Mesa de casa (a que o jogador monta na aba Mesa do Meu time) e a do visitante (sorteada), sempre só com os itens
    /// liberados. A do visitante tem mesa e feltro diferentes dos de casa; se o jogador só tiver uma mesa (ou um feltro),
    /// ao menos o outro muda.
    /// </summary>
    public static class Venue
    {
        /// <summary>Mesas liberadas, na ordem do id (sem nenhuma liberada, todas).</summary>
        public static List<TableTheme> Tables() => Unlocked(TableTheme.All(), t => t.Unlocked);

        public static List<FeltTheme> Felts() => Unlocked(FeltTheme.All(), f => f.Unlocked);

        /// <summary>Bolas liberadas com quadros (sem nenhuma, todas com quadros).</summary>
        public static List<BallTheme> Balls()
        {
            var withFrames = new List<BallTheme>();
            foreach (var ball in BallTheme.All())
                if (ball.HasFrames) withFrames.Add(ball);
            return Unlocked(withFrames.ToArray(), b => b.Unlocked);
        }

        /// <summary>A mesa de casa guardada no save; o que não existir ou não estiver liberado vira o primeiro liberado.</summary>
        public static VenueSetup Home()
        {
            var data = PlayerProfile.Data;
            var setup = new VenueSetup
            {
                TableId = Pick(Tables(), data.tableId, t => t.id),
                FeltId = Pick(Felts(), data.feltId, f => f.id),
                BallId = Pick(Balls(), data.ballId, b => b.id),
            };
            setup.GoalColor = GoalByName(setup.Table, setup.Felt, data.goalColorName);
            return setup;
        }

        /// <summary>Sorteia a mesa do visitante: mesa e feltro diferentes dos de casa (quando der), traves e bola.</summary>
        public static VenueSetup RollAway(VenueSetup home)
        {
            var tables = Tables();
            var felts = Felts();
            var otherTables = tables.FindAll(t => home == null || t.id != home.TableId);
            var otherFelts = felts.FindAll(f => home == null || f.id != home.FeltId);
            // Só uma mesa (ou um feltro): pelo menos o outro muda.
            if (otherTables.Count == 0) otherTables = tables;
            if (otherFelts.Count == 0) otherFelts = felts;

            var table = PickRandom(otherTables);
            var felt = PickRandom(otherFelts);
            var ball = PickRandom(Balls());
            return new VenueSetup
            {
                TableId = table != null ? table.id : null,
                FeltId = felt != null ? felt.id : null,
                GoalColor = table != null ? table.PickGoalColor(felt) : 0,
                BallId = ball != null ? ball.id : null,
            };
        }

        /// <summary>A cor de trave com esse nome, se o feltro aceitar; senão a primeira que ele aceita.</summary>
        public static int GoalByName(TableTheme table, FeltTheme felt, string colorName)
        {
            if (table == null) return 0;
            var allowed = table.AllowedGoalColors(felt);
            if (allowed.Count == 0) return 0;
            foreach (int index in allowed)
                if (!string.IsNullOrEmpty(colorName) && string.Equals(table.goalColors[index].name, colorName, System.StringComparison.OrdinalIgnoreCase))
                    return index;
            return allowed[0];
        }

        /// <summary>A cor de trave <paramref name="step"/> casas depois da atual, só entre as que o feltro aceita.</summary>
        public static int StepGoal(TableTheme table, FeltTheme felt, int current, int step)
        {
            if (table == null) return 0;
            var allowed = table.AllowedGoalColors(felt);
            if (allowed.Count == 0) return 0;
            int position = allowed.IndexOf(current);
            if (position < 0) return allowed[0];
            int count = allowed.Count;
            return allowed[(((position + step) % count) + count) % count];
        }

        /// <summary>O item <paramref name="step"/> casas depois do de id <paramref name="currentId"/> na lista (dá a volta).</summary>
        public static string Step<T>(List<T> items, string currentId, int step, System.Func<T, string> id)
        {
            if (items.Count == 0) return currentId;
            int position = items.FindIndex(item => id(item) == currentId);
            if (position < 0) return id(items[0]);
            int count = items.Count;
            return id(items[(((position + step) % count) + count) % count]);
        }

        private static List<T> Unlocked<T>(T[] all, System.Predicate<T> unlocked)
        {
            var list = new List<T>();
            foreach (var item in all)
                if (unlocked(item)) list.Add(item);
            return list.Count > 0 ? list : new List<T>(all);
        }

        private static string Pick<T>(List<T> items, string wanted, System.Func<T, string> id)
        {
            foreach (var item in items)
                if (id(item) == wanted) return wanted;
            return items.Count > 0 ? id(items[0]) : null;
        }

        private static T PickRandom<T>(List<T> items) where T : class =>
            items.Count > 0 ? items[UnityEngine.Random.Range(0, items.Count)] : null;
    }
}
