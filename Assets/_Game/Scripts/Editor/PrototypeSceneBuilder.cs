using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FutebolDeBotao.Editor
{
    /// <summary>
    /// Monta a cena de teste da fase 1: campo vertical, paredes, gols, goleiros, 5 botões por time e a bola.
    /// Menu: Futebol de Botão > Criar cena de teste (fase 1).
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        private const string DataFolder = "Assets/_Game/Data";
        private const string ArtFolder = "Assets/_Game/Art/Placeholder";
        private const string ScenePath = "Assets/_Game/Scenes/Partida.unity";

        // Medidas do campo em unidades do mundo (campo sempre vertical).
        private const float FieldWidth = 7f;
        private const float FieldHeight = 11f;
        private const float WallThickness = 0.5f;
        private const float GoalWidth = 2.4f;
        private const float GoalDepth = 0.8f;
        private const float DiscDiameter = 0.7f;
        private const float BallDiameter = 0.4f;
        private const float KeeperWidth = 1.0f;
        private const float KeeperHeight = 0.25f;

        private static readonly Color FeltColor = new(0.13f, 0.45f, 0.2f);
        private static readonly Color LineColor = new(1f, 1f, 1f, 0.5f);
        private static readonly Color WallColor = new(0.35f, 0.22f, 0.12f);
        private static readonly Color BlueTeam = new(0.2f, 0.45f, 0.95f);
        private static readonly Color RedTeam = new(0.9f, 0.2f, 0.2f);

        // Formação de 5 botões para o time de baixo; o de cima é espelhado.
        private static readonly Vector2[] Formation =
        {
            new(0f, -0.9f),
            new(-1.6f, -1.8f),
            new(1.6f, -1.8f),
            new(-1.0f, -3.4f),
            new(1.0f, -3.4f)
        };

        [MenuItem("Futebol de Botão/Criar cena de teste (fase 1)")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(DataFolder);
            EnsureFolder(ArtFolder);
            EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));

            var tuning = LoadOrCreateTuning();
            var square = LoadOrCreateSprite("Square", false);
            var circle = LoadOrCreateSprite("Circle", true);
            var wallMaterial = LoadOrCreateWallMaterial(tuning);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera();
            var table = new GameObject("Mesa").transform;
            CreateFieldVisuals(table, square, circle);
            CreateWalls(table, square, wallMaterial);

            var ballGo = CreateBall(table, circle, tuning);
            var ball = ballGo.GetComponent<Ball>();

            CreateGoal(table, square, TeamSide.Bottom, tuning, ball);
            CreateGoal(table, square, TeamSide.Top, tuning, ball);
            CreateTeam(table, circle, TeamSide.Bottom, BlueTeam, tuning);
            CreateTeam(table, circle, TeamSide.Top, RedTeam, tuning);

            CreateSystems(tuning);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[Futebol de Botão] Cena de teste criada em {ScenePath}. Ajuste a física em {DataFolder}/PhysicsTuning.asset.");

            // O Immersive Framework pode trocar a cena inicial do Play por uma cena de bootstrap vazia (sem câmera).
            if (EditorSceneManager.playModeStartScene != null)
                Debug.LogWarning($"[Futebol de Botão] O Play vai iniciar em '{AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene)}', não nesta cena. Para testar a fase 1, use Project Settings > Immersive Framework > Editor Play Mode Startup = Current Scene Only.");
        }

        private static void CreateCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = new Vector3(0f, 0f, -10f);
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = FieldHeight * 0.5f + GoalDepth + 0.6f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.1f);
        }

        private static void CreateFieldVisuals(Transform parent, Sprite square, Sprite circle)
        {
            CreateSprite("Feltro", parent, square, FeltColor, Vector2.zero, new Vector2(FieldWidth, FieldHeight), -10);
            CreateSprite("Linha do meio", parent, square, LineColor, Vector2.zero, new Vector2(FieldWidth, 0.05f), -9);
            CreateSprite("Círculo central", parent, circle, LineColor, Vector2.zero, new Vector2(1.8f, 1.8f), -9);
            CreateSprite("Feltro do círculo", parent, circle, FeltColor, Vector2.zero, new Vector2(1.7f, 1.7f), -8);
        }

        private static void CreateWalls(Transform parent, Sprite square, PhysicsMaterial2D material)
        {
            float halfW = FieldWidth * 0.5f;
            float halfH = FieldHeight * 0.5f;
            float t = WallThickness;
            float sideSegment = (FieldWidth - GoalWidth) * 0.5f;
            float segmentX = GoalWidth * 0.5f + sideSegment * 0.5f;

            CreateWall("Parede esquerda", parent, square, material, new Vector2(-halfW - t * 0.5f, 0f), new Vector2(t, FieldHeight + 2f * t));
            CreateWall("Parede direita", parent, square, material, new Vector2(halfW + t * 0.5f, 0f), new Vector2(t, FieldHeight + 2f * t));

            foreach (float sign in new[] { -1f, 1f })
            {
                string end = sign < 0f ? "baixo" : "cima";
                float y = sign * (halfH + t * 0.5f);
                CreateWall($"Fundo {end} esquerdo", parent, square, material, new Vector2(-segmentX, y), new Vector2(sideSegment, t));
                CreateWall($"Fundo {end} direito", parent, square, material, new Vector2(segmentX, y), new Vector2(sideSegment, t));

                // Rede do gol: laterais e fundo.
                float netY = sign * (halfH + GoalDepth * 0.5f);
                float netX = GoalWidth * 0.5f + t * 0.25f;
                CreateWall($"Rede {end} esquerda", parent, square, material, new Vector2(-netX, netY), new Vector2(t * 0.5f, GoalDepth), false);
                CreateWall($"Rede {end} direita", parent, square, material, new Vector2(netX, netY), new Vector2(t * 0.5f, GoalDepth), false);
                CreateWall($"Rede {end} fundo", parent, square, material, new Vector2(0f, sign * (halfH + GoalDepth + t * 0.25f)), new Vector2(GoalWidth + t, t * 0.5f), false);
            }
        }

        private static void CreateWall(string wallName, Transform parent, Sprite square, PhysicsMaterial2D material, Vector2 position, Vector2 size, bool pushesBack = true)
        {
            var go = CreateSprite(wallName, parent, square, WallColor, position, size, -5);
            var box = go.AddComponent<BoxCollider2D>();
            box.sharedMaterial = material;
            go.AddComponent<Wall>().Configure(pushesBack);
        }

        private static GameObject CreateBall(Transform parent, Sprite circle, PhysicsTuning tuning)
        {
            var go = CreateSprite("Bola", parent, circle, Color.white, Vector2.zero, Vector2.one * BallDiameter, 10);
            AddBody(go);
            go.AddComponent<CircleCollider2D>().radius = 0.5f;
            go.AddComponent<Ball>().Configure(tuning);
            return go;
        }

        private static void CreateGoal(Transform parent, Sprite square, TeamSide defending, PhysicsTuning tuning, Ball ball)
        {
            float sign = defending == TeamSide.Bottom ? -1f : 1f;
            float halfH = FieldHeight * 0.5f;
            string end = defending == TeamSide.Bottom ? "de baixo" : "de cima";

            var goal = new GameObject($"Gol {end}");
            goal.transform.SetParent(parent, false);
            goal.transform.position = new Vector2(0f, sign * (halfH + GoalDepth * 0.5f + 0.1f));
            var trigger = goal.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(GoalWidth, GoalDepth - 0.2f);
            goal.AddComponent<GoalTrigger>().Configure(defending);

            var keeperColor = defending == TeamSide.Bottom ? BlueTeam : RedTeam;
            var keeper = CreateSprite($"Goleiro {end}", parent, square, Color.Lerp(keeperColor, Color.black, 0.25f), new Vector2(0f, sign * (halfH - 0.35f)), new Vector2(KeeperWidth, KeeperHeight), 5);
            AddBody(keeper).bodyType = RigidbodyType2D.Kinematic;
            keeper.AddComponent<BoxCollider2D>();
            keeper.AddComponent<Goalkeeper>().Configure(defending, tuning, (GoalWidth - KeeperWidth) * 0.5f, ball);
        }

        private static void CreateTeam(Transform parent, Sprite circle, TeamSide side, Color color, PhysicsTuning tuning)
        {
            var team = new GameObject(side == TeamSide.Bottom ? "Time Azul" : "Time Vermelho").transform;
            team.SetParent(parent, false);
            float mirror = side == TeamSide.Bottom ? 1f : -1f;

            for (int i = 0; i < Formation.Length; i++)
            {
                var position = new Vector2(Formation[i].x, Formation[i].y * mirror);
                var go = CreateSprite($"Botão {i + 1}", team, circle, color, position, Vector2.one * DiscDiameter, 10);
                AddBody(go);
                go.AddComponent<CircleCollider2D>().radius = 0.5f;
                go.AddComponent<Disc>().Configure(side, tuning);

                // Anel de cor secundária (placeholder do visual do GDD).
                CreateSprite("Anel", go.transform, circle, Color.white, Vector2.zero, Vector2.one * 0.55f, 11, true);
                CreateSprite("Centro", go.transform, circle, color, Vector2.zero, Vector2.one * 0.42f, 12, true);
            }
        }

        private static void CreateSystems(PhysicsTuning tuning)
        {
            var go = new GameObject("Partida (sistemas)");
            var monitor = go.AddComponent<MotionMonitor>();
            monitor.Configure(tuning);
            go.AddComponent<WallRepulsion>().Configure(tuning);
            go.AddComponent<AimController>().Configure(tuning, monitor);
            go.AddComponent<AimVisuals>();
            go.AddComponent<PrototypeMatch>();
        }

        private static Rigidbody2D AddBody(GameObject go)
        {
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            return body;
        }

        private static GameObject CreateSprite(string objectName, Transform parent, Sprite sprite, Color color, Vector2 position, Vector2 size, int order, bool local = false)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            if (local) go.transform.localPosition = position;
            else go.transform.position = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return go;
        }

        private static PhysicsTuning LoadOrCreateTuning()
        {
            string path = $"{DataFolder}/PhysicsTuning.asset";
            var tuning = AssetDatabase.LoadAssetAtPath<PhysicsTuning>(path);
            if (tuning != null) return tuning;
            tuning = ScriptableObject.CreateInstance<PhysicsTuning>();
            AssetDatabase.CreateAsset(tuning, path);
            AssetDatabase.SaveAssets();
            return tuning;
        }

        private static PhysicsMaterial2D LoadOrCreateWallMaterial(PhysicsTuning tuning)
        {
            string path = $"{DataFolder}/Parede.physicsMaterial2D";
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
            if (material != null) return material;
            material = new PhysicsMaterial2D("Parede") { bounciness = tuning.bounciness, friction = tuning.friction };
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();
            return material;
        }

        /// <summary>Gera um sprite branco de 1 unidade (quadrado ou círculo) para o placeholder.</summary>
        private static Sprite LoadOrCreateSprite(string spriteName, bool round)
        {
            string path = $"{ArtFolder}/{spriteName}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float alpha = 1f;
                if (round)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    alpha = Mathf.Clamp01(r - d);
                }
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }

            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = size;
            importer.filterMode = FilterMode.Point;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parentFolder = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parentFolder);
            AssetDatabase.CreateFolder(parentFolder, Path.GetFileName(folder));
        }
    }
}
