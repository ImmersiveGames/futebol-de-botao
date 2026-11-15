#if PROGOL_FIREBASE
using System.Collections.Generic;
using Firebase;
using Firebase.Analytics;
using Firebase.Crashlytics;
using FutebolDeBotao;
using UnityEngine;

/// <summary>
/// Firebase Analytics + Crashlytics, ligados pelo símbolo PROGOL_FIREBASE. Só no aparelho (no editor nada é enviado).
/// Sem ID de publicidade e sem personalização de anúncios (consentimento de anúncio negado; o AndroidManifest do
/// ProGolPrivacy.androidlib já começa negado antes de o jogo abrir). Eventos antes de o Firebase ficar pronto
/// esperam numa fila.
/// </summary>
public static class FirebaseServices
{
    private static bool ready;
    private static readonly List<(string name, Parameter[] parameters)> queue = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (Application.isEditor) return;
        GameAnalytics.Sink = Send;
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
        {
            var status = task.Result;
            if (status != DependencyStatus.Available)
            {
                Debug.LogWarning($"[Firebase] Indisponível: {status}");
                return;
            }
            // Cria o app padrão: o Crashlytics começa a pegar os travamentos a partir daqui.
            _ = FirebaseApp.DefaultInstance;
            FirebaseAnalytics.SetConsent(new Dictionary<ConsentType, ConsentStatus>
            {
                { ConsentType.AnalyticsStorage, ConsentStatus.Granted },
                { ConsentType.AdStorage, ConsentStatus.Denied },
                { ConsentType.AdUserData, ConsentStatus.Denied },
                { ConsentType.AdPersonalization, ConsentStatus.Denied }
            });
            FirebaseAnalytics.SetUserProperty("allow_personalized_ads", "false");
            Crashlytics.ReportUncaughtExceptionsAsFatal = true;
            lock (queue)
            {
                ready = true;
                foreach (var (name, parameters) in queue) FirebaseAnalytics.LogEvent(name, parameters);
                queue.Clear();
            }
        });
    }

    private static void Send(string name, IReadOnlyList<KeyValuePair<string, object>> values)
    {
        var parameters = new Parameter[values.Count];
        for (int i = 0; i < values.Count; i++)
        {
            var (key, value) = (values[i].Key, values[i].Value);
            parameters[i] = value switch
            {
                long number => new Parameter(key, number),
                int number => new Parameter(key, (long)number),
                double number => new Parameter(key, number),
                float number => new Parameter(key, (double)number),
                _ => new Parameter(key, value?.ToString() ?? string.Empty)
            };
        }
        lock (queue)
        {
            if (!ready)
            {
                queue.Add((name, parameters));
                return;
            }
        }
        FirebaseAnalytics.LogEvent(name, parameters);
    }
}
#endif
