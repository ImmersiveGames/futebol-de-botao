using System.IO;
using Immersive.Framework.Actors;
using Immersive.Framework.Authoring;
using Immersive.Framework.Editor.PlayerParticipation;
using Immersive.Framework.Pause;
using Immersive.Framework.PlayerParticipation;
using Immersive.Framework.UnityInput;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace FutebolDeBotao.Editor
{
    /// <summary>
    /// Monta os jogadores no formato do Immersive Framework (Manager-Provisioned):
    /// controles (Input Actions), perfis de sessão, vagas e Actor, o prefab do Actor (técnico) e o prefab do host,
    /// liga a Player Session na Game Application, a participação na Activity "Jogo" e o provisionamento na Persistent Content.
    /// Pode rodar de novo: perfis e prefabs são atualizados; os controles só são criados se ainda não existem.
    /// </summary>
    public static class PlayersBuilder
    {
        private const string Folder = "Assets/_Game/App/Jogadores";
        private const string ControlsPath = Folder + "/Controles.inputactions";
        private const string ActorProfilePath = Folder + "/Tecnico.asset";
        private const string Slot1Path = Folder + "/Jogador1.asset";
        private const string Slot2Path = Folder + "/Jogador2.asset";
        private const string SessionPath = Folder + "/SessaoDeJogadores.asset";
        private const string ActorPrefabPath = Folder + "/Tecnico.prefab";
        private const string HostPrefabPath = Folder + "/Jogador.prefab";
        private const string PersistentScenePath = "Assets/_Game/Scenes/PersistentContent.unity";
        private const string ActivityPath = "Assets/_Game/App/Jogo.asset";

        public const string GameplayMap = "Jogo";
        public const string GlobalMap = "Global";
        private const string KeyboardMouse = "Teclado e mouse";
        private const string Touch = "Toque";

        [MenuItem("Futebol de Botão/Criar jogadores")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();

            var controls = LoadOrCreateControls();
            if (controls == null) return;

            var actorProfile = LoadOrCreate<ActorProfile>(ActorProfilePath);
            Set(actorProfile, so =>
            {
                so.FindProperty("actorProfileId").stringValue = "actor-profile.futebol.tecnico";
                so.FindProperty("displayName").stringValue = "Técnico";
                so.FindProperty("actorKind").intValue = (int)ActorKind.Player;
                so.FindProperty("actorRole").intValue = (int)ActorRole.Protagonist;
            });

            var slot1 = Slot(Slot1Path, "player.1", "Jogador 1", new Color(0.2f, 0.45f, 1f), 0, actorProfile);
            var slot2 = Slot(Slot2Path, "player.2", "Jogador 2", new Color(0.9f, 0.2f, 0.2f), 1, actorProfile);

            var session = LoadOrCreate<PlayerSessionProfile>(SessionPath);
            Set(session, so =>
            {
                var slots = so.FindProperty("supportedSlots");
                slots.arraySize = 2;
                slots.GetArrayElementAtIndex(0).objectReferenceValue = slot1;
                slots.GetArrayElementAtIndex(1).objectReferenceValue = slot2;
                so.FindProperty("initialJoiningOpen").boolValue = true;
                so.FindProperty("hostProvisioning").intValue = (int)PlayerHostProvisioningMode.ManagerProvisioned;
                so.FindProperty("actorResolutionPolicy").intValue = (int)PlayerActorResolutionPolicy.ResolveConfiguredDefault;
            });
            AssetDatabase.SaveAssets();

            var actorHost = BuildActorPrefab(controls);
            if (actorHost == null) return;
            var hostPrefab = BuildHostPrefab(controls, actorHost);
            if (hostPrefab == null) return;

            if (!LinkGameApplication(session)) return;
            if (!LinkActivity()) return;
            if (!SetupProvisioning(hostPrefab, 2)) return;

            AssetDatabase.SaveAssets();
            Debug.Log("[Futebol de Botão] Jogadores criados: controles, perfis, prefabs do técnico e do host, Player Session ligada e " +
                      "provisionamento na Persistent Content. Rode \"Criar telas\" de novo para o Menu e a Partida usarem os jogadores.");
        }

        // ---- Controles ----

        private static InputActionAsset LoadOrCreateControls()
        {
            if (AssetDatabase.LoadAssetAtPath<InputActionAsset>(ControlsPath) == null)
            {
                var asset = ScriptableObject.CreateInstance<InputActionAsset>();
                asset.name = "Controles";
                asset.AddControlScheme(KeyboardMouse).WithRequiredDevice("<Keyboard>").WithRequiredDevice("<Mouse>");
                asset.AddControlScheme(Touch).WithRequiredDevice("<Touchscreen>");

                const string both = KeyboardMouse + ";" + Touch;
                var game = asset.AddActionMap(GameplayMap);
                var point = game.AddAction("Apontar", InputActionType.PassThrough);
                point.expectedControlType = "Vector2";
                point.AddBinding("<Pointer>/position", groups: both);
                var press = game.AddAction("Pressionar", InputActionType.Button);
                press.AddBinding("<Pointer>/press", groups: both);
                game.AddAction("Cancelar", InputActionType.Button).AddBinding("<Mouse>/rightButton", groups: KeyboardMouse);
                var keeper = game.AddAction("MoverGoleiro", InputActionType.Value);
                keeper.expectedControlType = "Axis";
                keeper.AddCompositeBinding("1DAxis")
                    .With("Negative", "<Keyboard>/leftArrow", KeyboardMouse)
                    .With("Positive", "<Keyboard>/rightArrow", KeyboardMouse);
                keeper.AddCompositeBinding("1DAxis")
                    .With("Negative", "<Keyboard>/a", KeyboardMouse)
                    .With("Positive", "<Keyboard>/d", KeyboardMouse);
                game.AddAction("Confirmar", InputActionType.Button).AddBinding("<Keyboard>/space", groups: KeyboardMouse);
                game.AddAction("VaiChutar", InputActionType.Button).AddBinding("<Keyboard>/v", groups: KeyboardMouse);
                game.AddAction("Reiniciar", InputActionType.Button).AddBinding("<Keyboard>/r", groups: KeyboardMouse);

                var global = asset.AddActionMap(GlobalMap);
                global.AddAction("Pausa", InputActionType.Button).AddBinding("<Keyboard>/escape", groups: KeyboardMouse);

                File.WriteAllText(ControlsPath, asset.ToJson());
                Object.DestroyImmediate(asset);
                AssetDatabase.ImportAsset(ControlsPath, ImportAssetOptions.ForceSynchronousImport);
            }

            var controls = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ControlsPath);
            if (controls == null) Debug.LogError($"[Futebol de Botão] Não consegui importar {ControlsPath}.");
            return controls;
        }

        private static InputActionReference Action(InputActionAsset controls, string map, string action)
        {
            var target = controls.FindActionMap(map)?.FindAction(action);
            if (target == null)
            {
                Debug.LogError($"[Futebol de Botão] Falta a ação {map}/{action} em {ControlsPath}.");
                return null;
            }
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(ControlsPath))
                if (sub is InputActionReference reference && reference.action != null && reference.action.id == target.id)
                    return reference;
            Debug.LogError($"[Futebol de Botão] Não achei a referência da ação {map}/{action} em {ControlsPath}.");
            return null;
        }

        // ---- Perfis ----

        private static PlayerSlotProfile Slot(string path, string id, string displayName, Color color, int order, ActorProfile actor)
        {
            var slot = LoadOrCreate<PlayerSlotProfile>(path);
            Set(slot, so =>
            {
                so.FindProperty("playerSlotId").stringValue = id;
                so.FindProperty("displayName").stringValue = displayName;
                so.FindProperty("accentColor").colorValue = color;
                so.FindProperty("displayOrder").intValue = order;
                so.FindProperty("defaultActorProfile").objectReferenceValue = actor;
            });
            return slot;
        }

        // ---- Prefabs ----

        private static PlayerActorRuntimeHost BuildActorPrefab(InputActionAsset controls)
        {
            var root = new GameObject("Técnico");
            try
            {
                var host = root.AddComponent<PlayerActorRuntimeHost>();
                var declaration = root.GetComponent<PlayerActorDeclaration>();
                if (declaration == null) declaration = root.AddComponent<PlayerActorDeclaration>();
                var reader = root.AddComponent<PlayerGameplayInputReader>();
                var coach = root.AddComponent<CoachActor>();
                coach.Configure(reader,
                    Action(controls, GameplayMap, "Apontar"),
                    Action(controls, GameplayMap, "Pressionar"),
                    Action(controls, GameplayMap, "Cancelar"),
                    Action(controls, GameplayMap, "MoverGoleiro"),
                    Action(controls, GameplayMap, "Confirmar"),
                    Action(controls, GameplayMap, "VaiChutar"),
                    Action(controls, GameplayMap, "Reiniciar"));
                Set(host, so => so.FindProperty("playerActorDeclaration").objectReferenceValue = declaration);

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, ActorPrefabPath);
                return prefab != null ? prefab.GetComponent<PlayerActorRuntimeHost>() : null;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildHostPrefab(InputActionAsset controls, PlayerActorRuntimeHost actorHost)
        {
            var root = new GameObject("Jogador");
            try
            {
                var mount = new GameObject("ActorMount").transform;
                mount.SetParent(root.transform, false);

                var input = root.AddComponent<PlayerInput>();
                input.actions = controls;
                input.defaultActionMap = GameplayMap;
                input.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;

                var host = root.AddComponent<LocalPlayerHostAuthoring>();
                Set(host, so =>
                {
                    so.FindProperty("playerInput").objectReferenceValue = input;
                    so.FindProperty("actorMount").objectReferenceValue = mount;
                    so.FindProperty("playerActorRuntimeHostPrefab").objectReferenceValue = actorHost;
                });

                var map = controls.FindActionMap(GameplayMap);
                var gate = root.AddComponent<UnityPlayerInputGateAdapter>();
                Set(gate, so =>
                {
                    so.FindProperty("playerInput").objectReferenceValue = input;
                    var mapReference = so.FindProperty("gameplayActionMap");
                    mapReference.FindPropertyRelative("actionAsset").objectReferenceValue = controls;
                    mapReference.FindPropertyRelative("actionMapId").stringValue = map.id.ToString("D");
                    mapReference.FindPropertyRelative("cachedActionMapName").stringValue = map.name;
                });

                // Esc no mapa Global: o framework liga só no primeiro jogador que entrou (o Jogador 1).
                var pause = root.AddComponent<PlayerPauseInput>();
                Set(pause, so => so.FindProperty("pauseAction").objectReferenceValue = Action(controls, GlobalMap, "Pausa"));

                return PrefabUtility.SaveAsPrefabAsset(root, HostPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---- Game Application, Activity e Persistent Content ----

        private static bool LinkGameApplication(PlayerSessionProfile session)
        {
            var guids = AssetDatabase.FindAssets("t:GameApplicationAsset");
            if (guids.Length == 0)
            {
                Debug.LogError("[Futebol de Botão] Nenhuma Game Application encontrada.");
                return false;
            }
            var app = AssetDatabase.LoadAssetAtPath<GameApplicationAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
            Set(app, so =>
            {
                so.FindProperty("playerSessionEnabled").boolValue = true;
                so.FindProperty("defaultPlayerSessionProfile").objectReferenceValue = session;
            });
            return true;
        }

        private static bool LinkActivity()
        {
            var activity = AssetDatabase.LoadAssetAtPath<ActivityAsset>(ActivityPath);
            if (activity == null)
            {
                Debug.LogError($"[Futebol de Botão] Não achei a Activity {ActivityPath}.");
                return false;
            }
            // Todos que entraram jogam a partida; sem ninguém (2 jogadores por enquanto) também vale.
            Set(activity, so =>
            {
                so.FindProperty("playerParticipationProjectionMode").intValue = (int)ActivityParticipationProjectionMode.AllJoinedSlots;
                so.FindProperty("playerParticipationZeroParticipantPolicy").intValue = (int)ActivityParticipationZeroParticipantPolicy.Allowed;
                so.FindProperty("playerParticipationRequirementLevel").intValue = (int)PlayerParticipationRequirementLevel.GameplayReady;
            });
            return true;
        }

        private static bool SetupProvisioning(GameObject hostPrefab, int slotCount)
        {
            var scene = EditorSceneManager.OpenScene(PersistentScenePath, OpenSceneMode.Single);
            LocalPlayerProvisioningAuthoring authoring = null;
            foreach (var rootObject in scene.GetRootGameObjects())
            {
                authoring = rootObject.GetComponentInChildren<LocalPlayerProvisioningAuthoring>(true);
                if (authoring != null) break;
            }
            if (authoring == null)
            {
                SceneManager.SetActiveScene(scene);
                authoring = LocalPlayerProvisioningSetupCreator.Create();
            }

            Set(authoring, so => so.FindProperty("localPlayerHostPrefab").objectReferenceValue = hostPrefab);
            var manager = authoring.GetComponent<PlayerInputManager>();
            if (manager == null)
            {
                Debug.LogError("[Futebol de Botão] O provisionamento não tem PlayerInputManager.");
                return false;
            }
            // Limite derivado das vagas da sessão (mesmo campo que o editor do framework ajusta).
            Set(manager, so => so.FindProperty("m_MaxPlayerCount").intValue = slotCount);
            manager.playerPrefab = null;
            manager.splitScreen = false;
            EditorUtility.SetDirty(manager);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return true;
        }

        // ---- Utilitários ----

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
