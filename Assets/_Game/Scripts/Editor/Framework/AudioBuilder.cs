using System.IO;
using Immersive.Audio.Authoring;
using Immersive.Audio.Contracts;
using Immersive.Audio.Unity.Hosts;
using Immersive.Framework.Audio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FutebolDeBotao.Editor
{
    /// <summary>
    /// Áudio com o com.immersive.audio e o BGM do framework:
    /// cues dos sons em Assets/_Game/Audio/Cues, o Audio Runtime (AudioRuntimeHost + BGM Director) no Persistent Content,
    /// a música de cada Route (Route BGM Binding) e os sons da partida (<see cref="MatchSounds"/>).
    /// "Criar áudio" gera os sons provisórios que faltam (<see cref="ChiptuneSounds"/>), cria os assets e o Audio Runtime e
    /// depois roda "Criar telas", que liga a música e os sons nas cenas.
    /// </summary>
    public static class AudioBuilder
    {
        private const string AudioFolder = "Assets/_Game/Audio";
        private const string ClipsFolder = AudioFolder + "/Sons";
        private const string CuesFolder = AudioFolder + "/Cues";
        private const string DefaultsPath = AudioFolder + "/PadroesDeAudio.asset";
        private const string PersistentScenePath = "Assets/_Game/Scenes/PersistentContent.unity";
        private const string AudioRuntimeName = "Áudio";

        public const string MenuMusic = "musica_menu";
        public const string MatchMusic = "musica_partida";

        // Sons da partida: nome do arquivo em Sons/, volume.
        private static readonly (string clip, float volume)[] SfxCues =
        {
            ("peteleco", 0.8f),
            ("batida_fraca", 0.6f),
            ("batida_forte", 0.9f),
            ("bola_parede", 0.7f),
            ("apito", 0.7f),
            ("apito_final", 0.8f),
            ("gol", 1f),
            ("cartao", 0.8f)
        };

        [MenuItem("Futebol de Botão/Criar áudio")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var defaults = CreateAssets();
            if (defaults == null) return;
            if (!SetupPersistentContent(defaults)) return;
            Debug.Log("[Futebol de Botão] Áudio criado (cues e Audio Runtime no Persistent Content). Ligando música e sons nas telas...");
            ScreensBuilder.Build();
        }

        private static AudioDefaultsAsset CreateAssets()
        {
            Directory.CreateDirectory(CuesFolder);
            // Sons provisórios (chiptune) para os que ainda não existem; um som de verdade com o mesmo nome fica.
            int created = ChiptuneSounds.WriteMissing(ClipsFolder);
            AssetDatabase.Refresh();
            if (created > 0)
            {
                ConfigureImports();
                Debug.Log($"[Futebol de Botão] {created} sons provisórios criados em {ClipsFolder}. Faça o commit dos .wav e .meta.");
            }

            var defaults = LoadOrCreate<AudioDefaultsAsset>(DefaultsPath);
            if (defaults == null)
            {
                Debug.LogError($"[Futebol de Botão] Não consegui criar {DefaultsPath}.");
                return null;
            }

            foreach (var (clip, volume) in SfxCues)
            {
                var cue = Cue<AudioSfxCueAsset>(clip, "sfx", volume, AudioBusKeys.Sfx);
                if (cue == null) return null;
                Set(cue, so =>
                {
                    so.FindProperty("executionMode").intValue = (int)AudioSfxExecutionMode.Direct;
                    so.FindProperty("playbackMode").intValue = (int)AudioPlaybackMode.Global;
                    so.FindProperty("loopMode").intValue = (int)AudioLoopMode.Off;
                });
            }

            foreach (var music in new[] { MenuMusic, MatchMusic })
            {
                var cue = Cue<AudioBgmCueAsset>(music, "bgm", 0.5f, AudioBusKeys.Bgm);
                if (cue == null) return null;
                Set(cue, so =>
                {
                    so.FindProperty("loopMode").intValue = (int)AudioLoopMode.On;
                    so.FindProperty("fadeInSeconds").floatValue = 0.8f;
                    so.FindProperty("fadeOutSeconds").floatValue = 0.6f;
                });
            }

            AssetDatabase.SaveAssets();
            return defaults;
        }

        private static T Cue<T>(string clipName, string kind, float volume, string bus) where T : AudioCueAsset
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{ClipsFolder}/{clipName}.wav");
            if (clip == null)
            {
                Debug.LogError($"[Futebol de Botão] Faltou o som {ClipsFolder}/{clipName}.wav.");
                return null;
            }

            var cue = LoadOrCreate<T>(CuePath(clipName));
            Set(cue, so =>
            {
                so.FindProperty("cueId").stringValue = $"{kind}.futebol.{clipName}";
                so.FindProperty("clip").objectReferenceValue = clip;
                so.FindProperty("volume").floatValue = volume;
                so.FindProperty("pitch").floatValue = 1f;
                so.FindProperty("routingBus").stringValue = bus;
            });
            return cue;
        }

        /// <summary>Mono; a música fica comprimida na memória e carrega em segundo plano.</summary>
        private static void ConfigureImports()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { ClipsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not AudioImporter importer) continue;
                bool music = Path.GetFileNameWithoutExtension(path).StartsWith("musica");
                importer.forceToMono = true;
                importer.loadInBackground = music;
                var settings = importer.defaultSampleSettings;
                settings.loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = music ? 0.7f : 1f;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
        }

        private static string CuePath(string clipName) => $"{CuesFolder}/{clipName}.asset";

        private static T LoadCue<T>(string clipName) where T : AudioCueAsset => AssetDatabase.LoadAssetAtPath<T>(CuePath(clipName));

        /// <summary>Audio Runtime no Persistent Content: toca a música entre as telas e os sons da partida.</summary>
        private static bool SetupPersistentContent(AudioDefaultsAsset defaults)
        {
            var scene = EditorSceneManager.OpenScene(PersistentScenePath, OpenSceneMode.Single);

            GameObject root = null;
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == AudioRuntimeName) root = go;
            if (root == null)
            {
                root = new GameObject(AudioRuntimeName);
                SceneManager.MoveGameObjectToScene(root, scene);
            }

            var host = root.GetComponent<AudioRuntimeHost>();
            if (host == null) host = root.AddComponent<AudioRuntimeHost>();
            Set(host, so =>
            {
                so.FindProperty("defaults").objectReferenceValue = defaults;
                so.FindProperty("playbackRoot").objectReferenceValue = root.transform;
                so.FindProperty("ensurePersistentListener").boolValue = true;
            });

            var director = root.GetComponent<FrameworkBgmDirector>();
            if (director == null) director = root.AddComponent<FrameworkBgmDirector>();
            Set(director, so => so.FindProperty("audioRuntimeHost").objectReferenceValue = host);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // Sem os padrões o host não toca nada (FailedMissingDefaults).
            if (host.Defaults == null)
            {
                Debug.LogError($"[Futebol de Botão] O Audio Runtime ficou sem os padrões. Arraste {DefaultsPath} no campo Defaults do objeto \"{AudioRuntimeName}\" no Persistent Content.");
                return false;
            }
            return true;
        }

        /// <summary>Música da Route desta cena (o Director do Persistent Content é injetado pelo framework).</summary>
        public static void AddRouteMusic(Transform parent, string music)
        {
            var cue = LoadCue<AudioBgmCueAsset>(music);
            if (cue == null) return; // "Criar áudio" ainda não rodou.

            var go = new GameObject("Música");
            if (parent != null) go.transform.SetParent(parent, false);
            var binding = go.AddComponent<RouteBgmAuthoring>();
            Set(binding, so =>
            {
                so.FindProperty("routeBgm").objectReferenceValue = cue;
                so.FindProperty("policy").intValue = (int)FrameworkBgmRoutePolicy.PlayOwn;
            });
        }

        /// <summary>Música e sons da partida.</summary>
        public static void AddMatchAudio(Transform parent, MatchController match)
        {
            AddRouteMusic(parent, MatchMusic);

            var flick = LoadCue<AudioSfxCueAsset>("peteleco");
            if (flick == null) return;
            var go = new GameObject("Sons da partida");
            go.transform.SetParent(parent, false);
            go.AddComponent<MatchSounds>().Configure(match,
                flick,
                LoadCue<AudioSfxCueAsset>("batida_fraca"),
                LoadCue<AudioSfxCueAsset>("batida_forte"),
                LoadCue<AudioSfxCueAsset>("bola_parede"),
                LoadCue<AudioSfxCueAsset>("apito"),
                LoadCue<AudioSfxCueAsset>("apito_final"),
                LoadCue<AudioSfxCueAsset>("gol"),
                LoadCue<AudioSfxCueAsset>("cartao"));
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void Set(Object target, System.Action<SerializedObject> configure)
        {
            var so = new SerializedObject(target);
            configure(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}
