using System;
using System.Text;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Google Play Games para o resto do jogo: login automático ao abrir, botões das Configurações (entrar, conquistas,
    /// ranking) e o save na nuvem. Quem fala com o Google é o <see cref="IPlayGamesBackend"/>; sem ele tudo aqui vira
    /// nada e o jogo segue normal.
    ///
    /// Save na nuvem: um arquivo "progol" com a cópia do save (<see cref="GameSave.CloudJson"/>), sem o que é só do
    /// aparelho. Ao logar, lê a nuvem e decide:
    /// - nuvem vazia, ou é o mesmo envio deste aparelho: segue e envia o que mudou;
    /// - aparelho sem progresso, ou sem mudança desde a última sincronização: usa a nuvem sem perguntar;
    /// - os dois com progresso diferente: pergunta ao jogador qual usar (no Menu, quando nada mais está aberto).
    /// Depois disso, cada gravação marca o save como mudado e ele sobe no máximo a cada <see cref="UploadInterval"/>
    /// segundos, e na hora em que o jogo vai para o fundo.
    /// </summary>
    public static class PlayGames
    {
        private const float UploadInterval = 20f;
        private const float RetryInterval = 90f;

        private enum SyncState { Off, Reading, Conflict, Ready, Failed }

        private static IPlayGamesBackend backend;
        private static SyncState state;
        private static SaveData pendingCloud;
        private static bool asking;
        private static bool dirty;
        private static bool uploading;
        private static float lastUpload = -999f;
        private static float lastFailure;

        /// <summary>Tem Play Games neste aparelho: o botão aparece nas Configurações.</summary>
        public static bool Supported => backend != null;

        public static bool SignedIn => backend != null && backend.SignedIn;

        /// <summary>Chamado pelo backend ao abrir o jogo: tenta o login automático.</summary>
        public static void Register(IPlayGamesBackend service)
        {
            if (service == null || backend != null) return;
            backend = service;
            var runner = new GameObject("Play Games");
            UnityEngine.Object.DontDestroyOnLoad(runner);
            runner.AddComponent<PlayGamesRunner>();
            SignIn(false, null);
        }

        /// <summary>Entra no Play Games. <paramref name="manual"/>: o jogador apertou o botão.</summary>
        public static void SignIn(bool manual, Action<bool> done)
        {
            if (backend == null)
            {
                done?.Invoke(false);
                return;
            }
            try
            {
                backend.SignIn(manual, ok => OverlayUi.SafeInvoke(() =>
                {
                    Debug.Log($"[Play Games] Login {(ok ? "ok" : "não feito")}{(manual ? " (botão)" : string.Empty)}.");
                    if (ok)
                    {
                        Achievements.ForgetSent();
                        StartSync();
                    }
                    done?.Invoke(ok);
                }));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Play Games] Login falhou: {exception.Message}");
                done?.Invoke(false);
            }
        }

        public static void ShowAchievements()
        {
            if (SignedIn) Safe(() => backend.ShowAchievements());
        }

        public static void ShowLeaderboard()
        {
            if (SignedIn) Safe(() => backend.ShowLeaderboard(GPGSIds.leaderboard_weekly_points));
        }

        internal static void Unlock(string achievementId)
        {
            if (SignedIn) Safe(() => backend.Unlock(achievementId));
        }

        internal static void SetSteps(string achievementId, int steps)
        {
            if (SignedIn) Safe(() => backend.SetStepsAtLeast(achievementId, steps));
        }

        internal static void ReportScore(long score)
        {
            if (SignedIn) Safe(() => backend.ReportScore(GPGSIds.leaderboard_weekly_points, score));
        }

        /// <summary>O save do aparelho mudou: sobe para a nuvem na próxima janela.</summary>
        internal static void SaveChanged()
        {
            if (state == SyncState.Ready && GameSave.CloudHash() != GameSave.Data.cloud.syncedHash) dirty = true;
        }

        // ---- Sincronização ----

        private static void StartSync()
        {
            if (state == SyncState.Reading || state == SyncState.Conflict || state == SyncState.Ready) return;
            state = SyncState.Reading;
            Safe(() => backend.ReadSave((ok, bytes) => OverlayUi.SafeInvoke(() => OnCloudRead(ok, bytes))), () => Fail("leitura"));
        }

        private static void OnCloudRead(bool ok, byte[] bytes)
        {
            if (!ok)
            {
                Fail("leitura");
                return;
            }
            var local = GameSave.Data.cloud;
            var cloud = bytes != null && bytes.Length > 0 ? GameSave.ParseCloud(Encoding.UTF8.GetString(bytes)) : null;
            if (cloud == null || !GameSave.HasProgress(cloud))
            {
                // Nuvem vazia (ou sem nada que valha guardar): o aparelho manda.
                Ready(true);
                return;
            }
            string cloudId = cloud.cloud != null ? cloud.cloud.uploadId : null;
            bool localChanged = GameSave.CloudHash() != local.syncedHash;
            if (!string.IsNullOrEmpty(cloudId) && cloudId == local.uploadId)
            {
                // A nuvem é o último envio deste aparelho.
                Ready(localChanged);
                return;
            }
            bool synced = !string.IsNullOrEmpty(local.uploadId);
            if (!GameSave.HasProgress(GameSave.Data) || (synced && !localChanged))
            {
                // Aparelho novo/zerado, ou nada mudou aqui desde a última vez: o outro aparelho avançou.
                Debug.Log("[Play Games] Usando o save da nuvem.");
                GameSave.ApplyCloud(cloud);
                Ready(false);
                return;
            }
            pendingCloud = cloud;
            state = SyncState.Conflict;
            Debug.Log("[Play Games] Progresso diferente na nuvem e no aparelho: perguntando.");
        }

        private static void Ready(bool upload)
        {
            state = SyncState.Ready;
            dirty = upload;
            if (upload) lastUpload = -999f;
            // Com o save certo na mão: manda as conquistas que ele já merece e a pontuação da semana.
            Achievements.Report();
            WeeklyRanking.Report();
        }

        private static void Fail(string what)
        {
            Debug.LogWarning($"[Play Games] Save na nuvem indisponível ({what}); tento de novo depois.");
            state = SyncState.Failed;
            lastFailure = Time.unscaledTime;
            // Sem a nuvem, as conquistas e a pontuação do aparelho sobem do mesmo jeito.
            Achievements.Report();
            WeeklyRanking.Report();
        }

        private static void Upload()
        {
            if (backend == null || uploading || state != SyncState.Ready || !backend.SignedIn) return;
            uploading = true;
            dirty = false;
            lastUpload = Time.unscaledTime;
            string id = Guid.NewGuid().ToString("N");
            long ticks = DateTime.UtcNow.Ticks;
            string hash = GameSave.CloudHash();
            byte[] bytes = Encoding.UTF8.GetBytes(GameSave.CloudJson(id, ticks));
            string description = $"Pro Gol! - {GameSave.Data.profile.teamName}";
            Safe(() => backend.WriteSave(bytes, description, ok => OverlayUi.SafeInvoke(() =>
            {
                uploading = false;
                if (!ok)
                {
                    dirty = true;
                    Debug.LogWarning("[Play Games] Não consegui enviar o save; tento de novo depois.");
                    return;
                }
                var cloud = GameSave.Data.cloud;
                cloud.uploadId = id;
                cloud.savedAtTicks = ticks;
                cloud.syncedHash = hash;
                GameSave.WriteRaw("nuvem");
                // Gravou de novo enquanto enviava: sobe outra vez na próxima janela.
                dirty = GameSave.CloudHash() != hash;
            })), () =>
            {
                uploading = false;
                dirty = true;
            });
        }

        private static void Resolve(bool useCloud)
        {
            asking = false;
            var cloud = pendingCloud;
            pendingCloud = null;
            if (useCloud && cloud != null)
            {
                GameSave.ApplyCloud(cloud);
                Ready(false);
            }
            else Ready(true);
        }

        private static void Ask()
        {
            asking = true;
            string message = Txt.Format("Achamos no Play Games um progresso diferente do deste aparelho.\n\nNuvem: {0}\nEste aparelho: {1}\n\nQual você quer usar? O outro será substituído.",
                Summary(pendingCloud, true), Summary(GameSave.Data, false));
            MessageDialog.Open(Txt.Get("Progresso na nuvem"), message, Txt.Get("Nuvem"), () => Resolve(true), Txt.Get("Este aparelho"), () => Resolve(false));
        }

        private static string Summary(SaveData data, bool withDate)
        {
            int played = data.stats != null && data.stats.total != null ? data.stats.total.Played : 0;
            int coins = data.profile != null ? data.profile.coins : 0;
            string text = Txt.Format("{0} partidas, {1} moedas", played, coins);
            long ticks = data.cloud != null ? data.cloud.savedAtTicks : 0;
            if (withDate && ticks > 0)
                text += $" ({new DateTime(ticks, DateTimeKind.Utc).ToLocalTime():dd/MM/yyyy})";
            return text;
        }

        private static void Safe(Action action, Action failed = null)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Play Games] {exception.Message}");
                failed?.Invoke();
            }
        }

        /// <summary>Envia o save no tempo certo, pergunta o conflito quando dá e tenta de novo depois de falhas.</summary>
        private sealed class PlayGamesRunner : MonoBehaviour
        {
            private float nextCheck;

            private void Update()
            {
                if (Time.unscaledTime < nextCheck) return;
                nextCheck = Time.unscaledTime + 0.5f;
                switch (state)
                {
                    case SyncState.Ready when dirty && Time.unscaledTime - lastUpload >= UploadInterval:
                        Upload();
                        break;
                    case SyncState.Conflict when !asking && CanAsk():
                        Ask();
                        break;
                    case SyncState.Failed when Time.unscaledTime - lastFailure >= RetryInterval && SignedIn:
                        state = SyncState.Off;
                        StartSync();
                        break;
                }
            }

            private void OnApplicationPause(bool paused)
            {
                if (paused && state == SyncState.Ready && dirty) Upload();
            }

            private void OnApplicationFocus(bool focused)
            {
                if (!focused && state == SyncState.Ready && dirty) Upload();
            }

            // Só no Menu, sem outra tela ou pop-up por cima (a pergunta troca o progresso inteiro).
            private static bool CanAsk() =>
                FindAnyObjectByType<MenuScreen>() != null && FindAnyObjectByType<MessageDialog>() == null &&
                FindAnyObjectByType<AgeScreen>() == null && FindAnyObjectByType<SettingsScreen>() == null;
        }
    }
}
