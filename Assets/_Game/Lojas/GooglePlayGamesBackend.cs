#if PROGOL_PLAYGAMES && UNITY_ANDROID
using System;
using FutebolDeBotao;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using GooglePlayGames.BasicApi.SavedGame;
using UnityEngine;

/// <summary>
/// Google Play Games pelo plugin oficial (2.x): login automático, save na nuvem (Jogos salvos, um arquivo só),
/// conquistas e ranking. Só no aparelho Android (no editor o plugin não faz nada); se registra em
/// <see cref="PlayGames"/> ao abrir o jogo. IDs em GPGSIds (gerado pelo Setup do plugin).
/// </summary>
public sealed class GooglePlayGamesBackend : IPlayGamesBackend
{
    private const string FileName = "progol";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (Application.isEditor) return;
        try
        {
            PlayGamesPlatform.Activate();
            PlayGames.Register(new GooglePlayGamesBackend());
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Play Games] Não consegui iniciar: {exception.Message}");
        }
    }

    public bool SignedIn => PlayGamesPlatform.Instance.IsAuthenticated();

    public void SignIn(bool manual, Action<bool> done)
    {
        Action<SignInStatus> callback = status => done?.Invoke(status == SignInStatus.Success);
        if (manual) PlayGamesPlatform.Instance.ManuallyAuthenticate(callback);
        else PlayGamesPlatform.Instance.Authenticate(callback);
    }

    public void ReadSave(Action<bool, byte[]> done)
    {
        Open((ok, metadata) =>
        {
            if (!ok)
            {
                done(false, null);
                return;
            }
            PlayGamesPlatform.Instance.SavedGame.ReadBinaryData(metadata,
                (status, bytes) => done(status == SavedGameRequestStatus.Success, bytes ?? Array.Empty<byte>()));
        });
    }

    public void WriteSave(byte[] data, string description, Action<bool> done)
    {
        Open((ok, metadata) =>
        {
            if (!ok)
            {
                done(false);
                return;
            }
            var update = new SavedGameMetadataUpdate.Builder().WithUpdatedDescription(description).Build();
            PlayGamesPlatform.Instance.SavedGame.CommitUpdate(metadata, update, data,
                (status, _) => done(status == SavedGameRequestStatus.Success));
        });
    }

    // Abre (ou cria) o arquivo; conflito entre aparelhos fica com o salvo por último (a pergunta ao jogador é do jogo).
    private static void Open(Action<bool, ISavedGameMetadata> done)
    {
        var client = PlayGamesPlatform.Instance.SavedGame;
        if (client == null)
        {
            done(false, null);
            return;
        }
        client.OpenWithAutomaticConflictResolution(FileName, DataSource.ReadNetworkOnly,
            ConflictResolutionStrategy.UseMostRecentlySaved,
            (status, metadata) => done(status == SavedGameRequestStatus.Success && metadata != null, metadata));
    }

    public void ShowAchievements() => PlayGamesPlatform.Instance.ShowAchievementsUI();

    public void ShowLeaderboard(string leaderboardId) => PlayGamesPlatform.Instance.ShowLeaderboardUI(leaderboardId);

    public void Unlock(string achievementId) => PlayGamesPlatform.Instance.UnlockAchievement(achievementId, _ => { });

    public void SetStepsAtLeast(string achievementId, int steps) =>
        PlayGamesPlatform.Instance.SetStepsAtLeast(achievementId, steps, _ => { });

    public void ReportScore(string leaderboardId, long score) =>
        PlayGamesPlatform.Instance.ReportScore(score, leaderboardId, _ => { });
}
#endif
