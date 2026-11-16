using System;

namespace FutebolDeBotao
{
    /// <summary>
    /// Google Play Games (login, save na nuvem, conquistas e ranking), registrado de fora pelo GooglePlayGamesBackend
    /// (Assets/_Game/Lojas, só no Android com PROGOL_PLAYGAMES). Sem ele (editor, web), o jogo segue sem nada disso.
    /// </summary>
    public interface IPlayGamesBackend
    {
        bool SignedIn { get; }

        /// <summary>Entra na conta do Play Games; <paramref name="manual"/> = o jogador pediu (pode abrir a tela do Google).</summary>
        void SignIn(bool manual, Action<bool> done);

        /// <summary>Lê o save da nuvem: ok e os bytes (vazio = ainda não há save na nuvem).</summary>
        void ReadSave(Action<bool, byte[]> done);

        /// <summary>Grava o save na nuvem (substitui o que havia).</summary>
        void WriteSave(byte[] data, string description, Action<bool> done);

        void ShowAchievements();
        void ShowLeaderboard(string leaderboardId);
        void Unlock(string achievementId);
        void SetStepsAtLeast(string achievementId, int steps);
        void ReportScore(string leaderboardId, long score);
    }
}
