using System;
using System.Reflection;

namespace FutebolDeBotao.Tests
{
    /// <summary>
    /// Troca o save do jogo por um de mentira durante o teste e devolve o verdadeiro no fim.
    /// O "store" (onde o save é gravado no disco) fica nulo no meio: mesmo se algo pedir para gravar, nada vai para o arquivo.
    /// Uso: <c>using (SaveSandbox.Use(new SaveData())) { ... }</c>.
    /// </summary>
    internal sealed class SaveSandbox : IDisposable
    {
        private const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        private static readonly PropertyInfo DataProperty = typeof(GameSave).GetProperty("Data", Flags);
        private static readonly FieldInfo StoreField = typeof(GameSave).GetField("store", Flags);

        private readonly SaveData realData;
        private readonly object realStore;
        private readonly bool realLeagueMode;

        private SaveSandbox(SaveData fake)
        {
            realData = GameSave.Data;
            realStore = StoreField.GetValue(null);
            realLeagueMode = Tournament.LeagueMode;
            StoreField.SetValue(null, null);
            SetData(fake);
        }

        public static SaveSandbox Use(SaveData fake) => new(fake);

        public void Dispose()
        {
            SetData(realData);
            StoreField.SetValue(null, realStore);
            Tournament.LeagueMode = realLeagueMode;
        }

        private static void SetData(SaveData data) => DataProperty.GetSetMethod(true).Invoke(null, new object[] { data });

        /// <summary>Chama um método privado ou interno estático do jogo (para testar funções sem abrir o acesso a elas).</summary>
        public static T Call<T>(Type type, string method, params object[] args)
        {
            var info = type.GetMethod(method, Flags);
            if (info == null) throw new MissingMethodException(type.Name, method);
            try { return (T)info.Invoke(null, args); }
            catch (TargetInvocationException e) when (e.InnerException != null) { throw e.InnerException; }
        }
    }
}
