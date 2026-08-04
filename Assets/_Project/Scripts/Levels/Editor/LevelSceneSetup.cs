using System.Collections.Generic;
using System.IO;
using Kagemura.Enemies;
using Kagemura.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Kagemura.LevelTools
{
    /// <summary>
    /// Build Order step 9, greybox half: turn the <see cref="LevelData"/> assets into playable
    /// level scenes.
    ///
    /// Two commands, meant to be run in order and re-run freely:
    ///   4. Create the level data assets — the handcrafted layouts, written once and then tuned in
    ///      the inspector. Existing assets are never overwritten.
    ///   5. Build the scenes — one scene per asset, rebuilt from scratch each time.
    ///
    /// Each scene is built by COPYING Game.unity and replacing its terrain, rather than assembling
    /// a level from nothing. Game.unity already holds a player with every weapon, special and
    /// binding tuned across eight build-order steps, plus the HUD, the pause menu and the end
    /// screen. Rebuilding that from code would mean re-deriving tuning that only exists as
    /// inspector values, and getting it subtly wrong — the levels would play differently from the
    /// scene the combat was actually tuned in, which is the one thing greybox levels must not do.
    ///
    /// The cost of that choice is that the player exists five times over, so a tuning change made
    /// in Game.unity does not reach the levels until they are rebuilt. That is the right trade
    /// while the levels are disposable and combat is still moving; it stops being right at the art
    /// pass, when the scenes get hand-dressed and can no longer be regenerated. The fix at that
    /// point is a player prefab, not a cleverer builder.
    ///
    /// Anything the builder writes is namable and reproducible; nothing in a level scene is hand
    /// work yet, so "rebuild it" is always a safe answer.
    /// </summary>
    public static class LevelSceneSetup
    {
        private const string DataFolder = "Assets/_Project/Scripts/Levels";
        private const string ScenesFolder = "Assets/Scenes";
        private const string TemplateScenePath = ScenesFolder + "/Game.unity";

        /// <summary>
        /// The levels in play order, as scene names. Build Settings order comes from this, and so
        /// does each level's exit — level N's exit loads level N+1, and the last one loads the boss
        /// arena. Reordering here reorders the game.
        /// </summary>
        public static readonly string[] LevelSceneNames =
        {
            "Level 01 Spring",
            "Level 02 Summer",
            "Level 03 Autumn",
            "Level 04 Winter",
            "Boss Arena"
        };

        public static string ScenePathFor(string sceneName) => $"{ScenesFolder}/{sceneName}.unity";
        public static string DataPathFor(string sceneName) => $"{DataFolder}/{sceneName}.asset";

        /// <summary>
        /// Data, then scenes, then Build Settings — the order they depend on each other in. The one
        /// command to run after pulling this branch, and the one to re-run after tuning a level.
        /// </summary>
        [MenuItem("Kagemura/Setup/Build the Whole Game Flow (steps 4, 5, 6, then 3)")]
        public static void BuildEverything()
        {
            CreateLevelDataAssets();
            BuildLevelScenes();
            FitSeasonalEdge();
            Kagemura.DevTools.MenuSceneSetup.RegisterScenesInBuildSettings();
        }

        // ------------------------------------------------------------------ step 4: data assets

        [MenuItem("Kagemura/Setup/4. Create the Season Level Data Assets")]
        public static void CreateLevelDataAssets() => CreateLevelDataAssets(overwrite: false);

        /// <summary>
        /// Throw away every level asset and write the defaults again. Separate command, and named
        /// for what it costs, because the assets are where the level tuning lives — this is the one
        /// destructive thing in here.
        /// </summary>
        [MenuItem("Kagemura/Setup/Reset the Level Data Assets to Defaults (discards tuning)")]
        public static void ResetLevelDataAssets()
        {
            bool ok = EditorUtility.DisplayDialog(
                "Reset level data?",
                "This overwrites every LevelData asset with the built-in defaults. Any layout, " +
                "palette or enemy placement you have tuned in the inspector is lost.",
                "Reset", "Cancel");

            if (ok) CreateLevelDataAssets(overwrite: true);
        }

        private static void CreateLevelDataAssets(bool overwrite)
        {
            if (!Directory.Exists(DataFolder))
            {
                Debug.LogError($"[LevelSceneSetup] No folder at {DataFolder}.");
                return;
            }

            var enemies = LoadEnemyData();
            int written = 0, skipped = 0;

            foreach (var defaults in DefaultLevels(enemies))
            {
                string path = DataPathFor(defaults.name);
                var existing = AssetDatabase.LoadAssetAtPath<LevelData>(path);

                if (existing != null && !overwrite)
                {
                    // The defaults were built before we knew whether they were wanted; drop them
                    // rather than leaving an orphan instance in memory until the next reload.
                    Object.DestroyImmediate(defaults);
                    skipped++;
                    continue;
                }

                if (existing != null)
                {
                    EditorUtility.CopySerialized(defaults, existing);
                    EditorUtility.SetDirty(existing);
                    Object.DestroyImmediate(defaults);
                }
                else
                {
                    AssetDatabase.CreateAsset(defaults, path);
                }

                written++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[LevelSceneSetup] {written} level asset(s) written, {skipped} left alone. " +
                      $"They are in {DataFolder} — tune them there, then run step 5 again.");
        }

        // ---------------------------------------------------------------------- step 5: scenes

        [MenuItem("Kagemura/Setup/5. Build the Level Scenes from Level Data")]
        public static void BuildLevelScenes()
        {
            if (!File.Exists(TemplateScenePath))
            {
                Debug.LogError($"[LevelSceneSetup] No template scene at {TemplateScenePath}. Every " +
                               "level is built from it, so there is nothing to build from.");
                return;
            }

            int built = 0;

            foreach (string sceneName in LevelSceneNames)
            {
                var data = AssetDatabase.LoadAssetAtPath<LevelData>(DataPathFor(sceneName));

                if (data == null)
                {
                    Debug.LogWarning($"[LevelSceneSetup] No level asset for '{sceneName}'. Run " +
                                     "step 4 first.");
                    continue;
                }

                if (BuildScene(sceneName, data)) built++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[LevelSceneSetup] Built {built} level scene(s). Run step 3 to put them in " +
                      "Build Settings, or the exits will have nothing to load.");
        }

        /// <summary>
        /// Build one level scene. Returns false if it could not be built, having said why.
        ///
        /// Replaces the scene at the target path outright. Safe because nothing in a level scene is
        /// hand-authored yet — everything in one comes from the LevelData asset.
        /// </summary>
        private static bool BuildScene(string sceneName, LevelData data)
        {
            string path = ScenePathFor(sceneName);

            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);

            if (!AssetDatabase.CopyAsset(TemplateScenePath, path))
            {
                Debug.LogError($"[LevelSceneSetup] Could not copy {TemplateScenePath} to {path}.");
                return false;
            }

            AssetDatabase.Refresh();

            // Additive, so whatever the user has open is neither closed nor prompted to save.
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            var template = FindRoot(scene, "Ground");
            if (template == null)
            {
                Debug.LogError($"[LevelSceneSetup] '{sceneName}' has no object called 'Ground' to " +
                               "copy terrain from. The template scene must keep one — it is where " +
                               "the slab sprite, material and layer come from.");
                EditorSceneManager.CloseScene(scene, removeScene: true);
                return false;
            }

            var templateRenderer = template.GetComponent<SpriteRenderer>();
            Sprite boxSprite = templateRenderer != null ? templateRenderer.sprite : null;
            int groundLayer = template.layer;

            BuildTerrain(scene, data, template);
            Object.DestroyImmediate(template);

            BuildEnemies(scene, data, boxSprite, groundLayer);
            BuildExit(scene, data, boxSprite);
            BuildKillPlane(scene, data);
            PlacePlayer(scene, data);
            ApplyPalette(scene, data);
            AddSeasonalEdge(scene, data);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, removeScene: true);

            Debug.Log($"[LevelSceneSetup] Built {path}.");
            return true;
        }

        /// <summary>
        /// Lay the slabs out as copies of the template ground.
        ///
        /// Copied rather than built from scratch so each slab inherits the template's sprite,
        /// material, layer and collider setup. That matters more than it sounds: the greybox sprite
        /// the rest of the project uses is generated in memory at runtime and cannot be saved into
        /// a scene, so a slab built from nothing would come back invisible after the next domain
        /// reload — with a collider still there, which is a genuinely confusing thing to debug.
        ///
        /// The root keeps the name "Ground" and the ground layer, because the dev spawn menu finds
        /// its blocking layer with GameObject.Find("Ground").
        /// </summary>
        private static void BuildTerrain(Scene scene, LevelData data, GameObject template)
        {
            var root = NewRoot("Ground", scene);
            root.layer = template.layer;

            if (data.slabs == null || data.slabs.Length == 0)
            {
                Debug.LogWarning($"[LevelSceneSetup] '{data.name}' has no slabs, so the level has " +
                                 "no floor.", data);
                return;
            }

            for (int i = 0; i < data.slabs.Length; i++)
            {
                var slab = data.slabs[i];

                if (slab.Width <= 0f)
                {
                    Debug.LogWarning($"[LevelSceneSetup] '{data.name}' slab {i} has End X at or " +
                                     "before Start X, so it was skipped.", data);
                    continue;
                }

                var go = Object.Instantiate(template, root.transform);
                go.name = $"Slab {i + 1:00}";
                go.transform.position = slab.Center;

                // The template's collider is a unit box and its sprite is a unit square, so scale
                // sizes both at once — the same trick the greybox test scene already uses.
                go.transform.localScale = new Vector3(slab.Size.x, slab.Size.y, 1f);

                if (go.TryGetComponent<SpriteRenderer>(out var renderer))
                    renderer.color = data.groundColor;
            }
        }

        private static void BuildEnemies(Scene scene, LevelData data, Sprite bodySprite, int groundLayer)
        {
            if (data.enemies == null || data.enemies.Length == 0) return;

            var root = NewRoot("Enemies", scene);

            int enemyLayer = LayerMask.NameToLayer("Enemy");
            int playerLayer = LayerMask.NameToLayer("Player");

            if (enemyLayer < 0 || playerLayer < 0)
            {
                Debug.LogError("[LevelSceneSetup] The project has no 'Enemy' and/or 'Player' " +
                               "layer, so enemies would spawn unhittable. Add them under Project " +
                               "Settings > Tags and Layers.");
                return;
            }

            foreach (var placement in data.enemies)
            {
                if (placement.data == null)
                {
                    Debug.LogWarning($"[LevelSceneSetup] '{data.name}' has an enemy placement with " +
                                     "no EnemyData, so it was skipped.", data);
                    continue;
                }

                var go = EnemyFactory.Build(new EnemyFactory.Setup
                {
                    data = placement.data,
                    kind = placement.kind,
                    layer = enemyLayer,
                    targetLayers = 1 << playerLayer,
                    blockingLayers = 1 << groundLayer,
                    // A real asset, not the in-memory greybox sprite: this one has to survive
                    // being saved into the scene. See EnemyFactory.Setup.bodySprite.
                    bodySprite = bodySprite,
                    addHealthBar = true
                }, placement.position);

                if (go != null) go.transform.SetParent(root.transform, worldPositionStays: true);
            }
        }

        /// <summary>
        /// The end of the level: a trigger the player walks into, with a marker so it is visible in
        /// greybox. Painted in the season's accent colour, since the accent is the one colour that
        /// changes per season (spec §4) and the exit is the thing you are looking for.
        ///
        /// The boss arena leaves Next Scene empty and gets no exit at all — it ends at the
        /// EndScreen when the boss dies, not at a doorway.
        /// </summary>
        private static void BuildExit(Scene scene, LevelData data, Sprite markerSprite)
        {
            if (string.IsNullOrEmpty(data.nextScene)) return;

            var go = NewRoot("Level Exit", scene);
            go.transform.position = data.exitPosition;

            var size = new Vector2(2f, 4f);

            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = size;

            var markerGo = new GameObject("Marker", typeof(SpriteRenderer));
            markerGo.transform.SetParent(go.transform, false);
            markerGo.transform.localScale = new Vector3(size.x, size.y, 1f);

            var marker = markerGo.GetComponent<SpriteRenderer>();
            marker.sprite = markerSprite;

            var tint = data.accentColor;
            tint.a = 0.45f;                       // see-through, so it reads as a doorway not a wall
            marker.color = tint;

            go.AddComponent<LevelExit>().SetNextScene(data.nextScene);
        }

        /// <summary>
        /// The floor under the level, spanning the whole thing plus margin so a fall off either end
        /// is caught too.
        /// </summary>
        private static void BuildKillPlane(Scene scene, LevelData data)
        {
            data.GetHorizontalBounds(out float minX, out float maxX);

            const float depth = 20f;

            var go = NewRoot("Kill Volume", scene);
            go.transform.position = new Vector3((minX + maxX) * 0.5f, data.killPlaneY - depth * 0.5f, 0f);

            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(maxX - minX, depth);

            go.AddComponent<KillVolume>();
        }

        private static void PlacePlayer(Scene scene, LevelData data)
        {
            var player = FindInScene<Kagemura.Player.PlayerController>(scene);

            if (player == null)
            {
                Debug.LogError($"[LevelSceneSetup] No player in the copy of {TemplateScenePath}, " +
                               "so the level starts empty. The template scene must contain one.");
                return;
            }

            player.transform.position = data.playerStart;
        }

        /// <summary>
        /// The season, applied (spec §4). Two settings carry it in greybox: the camera's clear
        /// colour is the sky, and the global 2D light is how warm or cold everything under it
        /// reads. The terrain tint is applied as the slabs are built.
        ///
        /// The 3D backdrop of step 10 is deliberately not wired in here. BackgroundRig needs its
        /// own camera, a background layer and actual 3D geometry to draw, and a half-built URP
        /// camera stack renders black — which would make every level look broken rather than
        /// unfinished. A flat sky colour is the honest greybox stand-in for it.
        /// </summary>
        private static void ApplyPalette(Scene scene, LevelData data)
        {
            var camera = FindInScene<Camera>(scene);
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = data.skyColor;
            }

            var light = FindInScene<Light2D>(scene);
            if (light != null) light.color = data.ambientLight;
        }

        /// <summary>
        /// Put the season's weapon edge in the scene (spec §2.6), pointed at this level's data.
        ///
        /// The only runtime link from a scene back to its LevelData. Everything else a level has
        /// is baked geometry, but the edge is a number applied to the player's weapons, so
        /// something in the scene has to carry it.
        ///
        /// Adds or updates — never duplicates — so it is safe on a scene that already has one.
        /// That is what lets the existing level scenes be retrofitted rather than rebuilt.
        /// </summary>
        private static void AddSeasonalEdge(Scene scene, LevelData data)
        {
            var edge = FindInScene<SeasonalEdge>(scene);

            if (edge == null)
            {
                var go = NewRoot("Seasonal Edge", scene);
                edge = go.AddComponent<SeasonalEdge>();
            }

            edge.SetLevel(data);
            EditorUtility.SetDirty(edge);
        }

        // ---------------------------------------------------------- step 6: the seasonal edge

        /// <summary>
        /// Fit the seasonal weapon edge (spec §2.6) to level scenes that already exist, without
        /// rebuilding them.
        ///
        /// Separate from step 5 on purpose. Step 5 replaces a scene outright, which is fine while
        /// a level is nothing but generated greybox and stops being fine the moment anything in
        /// one is hand-placed. This opens each scene, changes the one thing, and saves — so it
        /// stays safe to run after the art pass has started, when step 5 no longer is.
        ///
        /// Also writes the season-to-weapon mapping onto the LevelData assets, since assets
        /// created before §2.6 existed have the field at its default of None.
        /// </summary>
        [MenuItem("Kagemura/Setup/6. Fit the Seasonal Weapon Edge to the Existing Scenes")]
        public static void FitSeasonalEdge()
        {
            int assetsSet = 0, scenesFitted = 0;

            foreach (string sceneName in LevelSceneNames)
            {
                var data = AssetDatabase.LoadAssetAtPath<LevelData>(DataPathFor(sceneName));

                if (data == null)
                {
                    Debug.LogWarning($"[LevelSceneSetup] No level asset for '{sceneName}'. Run " +
                                     "step 4 first.");
                    continue;
                }

                data.sharpenedWeapon = EdgeFor(data.season);
                data.edgeMultiplier = DefaultEdgeMultiplier;
                EditorUtility.SetDirty(data);
                assetsSet++;

                string scenePath = ScenePathFor(sceneName);
                if (!File.Exists(scenePath))
                {
                    Debug.LogWarning($"[LevelSceneSetup] {scenePath} does not exist yet, so the " +
                                     "edge was written to the asset but not into a scene. Run " +
                                     "step 5.");
                    continue;
                }

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                AddSeasonalEdge(scene, data);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                EditorSceneManager.CloseScene(scene, removeScene: true);
                scenesFitted++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[LevelSceneSetup] Seasonal edge written to {assetsSet} level asset(s) and " +
                      $"fitted into {scenesFitted} existing scene(s). No scene was rebuilt.");
        }

        // ------------------------------------------------------------------------- scene helpers

        private static GameObject NewRoot(string name, Scene scene)
        {
            // New GameObjects land in the active scene, which is whatever the user had open — not
            // the one being built, since it was opened additively.
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == name) return root;

            return null;
        }

        /// <summary>
        /// Find a component in one specific scene. FindFirstObjectByType searches every loaded
        /// scene, and this runs with at least two open — it would happily return the player from
        /// the scene the user was already working in.
        /// </summary>
        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }

            return null;
        }

        // --------------------------------------------------------------------- default layouts

        private static Dictionary<EnemyKind, EnemyData> LoadEnemyData()
        {
            var byKind = new Dictionary<EnemyKind, EnemyData>();

            foreach (string guid in AssetDatabase.FindAssets("t:EnemyData"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<EnemyData>(
                    AssetDatabase.GUIDToAssetPath(guid));

                if (asset == null) continue;

                EnemyKind kind = EnemyFactory.GuessKind(asset);
                if (!byKind.ContainsKey(kind)) byKind[kind] = asset;
            }

            foreach (EnemyKind kind in System.Enum.GetValues(typeof(EnemyKind)))
                if (!byKind.ContainsKey(kind))
                    Debug.LogWarning($"[LevelSceneSetup] No EnemyData asset reads as '{kind}', so " +
                                     "those placements will be empty. Check the asset names in " +
                                     "Assets/_Project/Scripts/Enemies.");

            return byKind;
        }

        /// <summary>
        /// The four seasons and the arena, laid out by hand (spec §3.1 — handcrafted, not
        /// procedural). Each is a horizontal run of ~2-3 minutes with escalating pressure.
        ///
        /// The progression is what §3.1 asks for — one new thing per season rather than everything
        /// at once — spent on enemy types, since three types across four levels is exactly enough
        /// to introduce one at a time and then combine them:
        ///   Spring — flat ground, wide gaps, rushers only. Movement and the sword.
        ///   Summer — verticality, and the ranged type on perches you have to climb to.
        ///   Autumn — narrow platforms over pits, and the shielded type that punishes mashing.
        ///   Winter — all three together, leading into the arena.
        ///
        /// No environmental hazards yet. §3.1 offers "one enemy type OR one hazard" per season, and
        /// the enemy types alone cover all four seasons — a hazard would be a new system built for
        /// greybox, which the cut list (§8) is there to prevent.
        ///
        /// Distances are sized against the player as tuned: 8 units/second of run and a jump that
        /// clears roughly ten units of height, so a 6-unit gap is comfortable and a 4-unit rise is
        /// free. These are intended to be walked through and retuned, not trusted.
        /// </summary>
        private static IEnumerable<LevelData> DefaultLevels(Dictionary<EnemyKind, EnemyData> enemies)
        {
            EnemyData Rusher() => Get(enemies, EnemyKind.Rusher);
            EnemyData Ranged() => Get(enemies, EnemyKind.Ranged);
            EnemyData Shielded() => Get(enemies, EnemyKind.Shielded);
            EnemyData Boss() => Get(enemies, EnemyKind.Boss);

            // --- Spring: cherry blossom, first yokai. Teaches movement and the sword. ---
            var spring = New(LevelSceneNames[0], "Spring — First Blossom", Season.Spring);
            spring.skyColor = new Color(0.96f, 0.80f, 0.82f);
            spring.groundColor = new Color(0.36f, 0.28f, 0.34f);
            spring.accentColor = new Color(0.95f, 0.55f, 0.65f);
            spring.ambientLight = new Color(1f, 0.94f, 0.94f);
            spring.playerStart = new Vector2(0f, -1.5f);
            spring.slabs = new[]
            {
                Slab(-6f, 26f, -3f),
                Slab(32f, 54f, -3f),
                Slab(60f, 74f, -1f),
                Slab(80f, 108f, -3f)
            };
            spring.enemies = new[]
            {
                At(Rusher(), EnemyKind.Rusher, 18f, -1.5f),
                At(Rusher(), EnemyKind.Rusher, 44f, -1.5f),
                At(Rusher(), EnemyKind.Rusher, 68f, 0.5f),
                At(Rusher(), EnemyKind.Rusher, 94f, -1.5f)
            };
            spring.exitPosition = new Vector2(104f, -1.5f);
            spring.nextScene = LevelSceneNames[1];
            yield return spring;

            // --- Summer: storm-lit, vertical. Introduces the ranged type, on perches. ---
            var summer = New(LevelSceneNames[1], "Summer — Storm Road", Season.Summer);
            summer.skyColor = new Color(0.13f, 0.25f, 0.32f);
            summer.groundColor = new Color(0.14f, 0.26f, 0.24f);
            summer.accentColor = new Color(0.62f, 0.68f, 0.74f);
            summer.ambientLight = new Color(0.76f, 0.86f, 0.92f);
            summer.playerStart = new Vector2(0f, -1.5f);
            summer.slabs = new[]
            {
                Slab(-6f, 22f, -3f),
                Slab(28f, 42f, -1f),
                Slab(48f, 60f, 1f),
                Slab(66f, 86f, -3f),
                Slab(92f, 102f, 2f),
                Slab(108f, 132f, -3f)
            };
            summer.enemies = new[]
            {
                At(Rusher(), EnemyKind.Rusher, 14f, -1.5f),
                At(Ranged(), EnemyKind.Ranged, 36f, 0.5f),
                At(Rusher(), EnemyKind.Rusher, 54f, 2.5f),
                At(Ranged(), EnemyKind.Ranged, 78f, -1.5f),
                At(Ranged(), EnemyKind.Ranged, 97f, 3.5f),
                At(Rusher(), EnemyKind.Rusher, 120f, -1.5f)
            };
            summer.exitPosition = new Vector2(128f, -1.5f);
            summer.nextScene = LevelSceneNames[2];
            yield return summer;

            // --- Autumn: narrow ground over pits. Introduces the shielded type. ---
            var autumn = New(LevelSceneNames[2], "Autumn — Falling Leaves", Season.Autumn);
            autumn.skyColor = new Color(0.85f, 0.45f, 0.22f);
            autumn.groundColor = new Color(0.34f, 0.19f, 0.14f);
            autumn.accentColor = new Color(0.90f, 0.30f, 0.20f);
            autumn.ambientLight = new Color(1f, 0.86f, 0.72f);
            autumn.playerStart = new Vector2(0f, -1.5f);
            autumn.slabs = new[]
            {
                Slab(-6f, 18f, -3f),
                Slab(24f, 34f, -3f),
                Slab(40f, 50f, -1f),
                Slab(56f, 64f, -3f),
                Slab(70f, 92f, -3f),
                Slab(98f, 110f, 0f),
                Slab(116f, 140f, -3f)
            };
            autumn.enemies = new[]
            {
                At(Shielded(), EnemyKind.Shielded, 12f, -1.5f),
                At(Rusher(), EnemyKind.Rusher, 29f, -1.5f),
                At(Ranged(), EnemyKind.Ranged, 45f, 0.5f),
                At(Shielded(), EnemyKind.Shielded, 78f, -1.5f),
                At(Rusher(), EnemyKind.Rusher, 86f, -1.5f),
                At(Ranged(), EnemyKind.Ranged, 104f, 1.5f),
                At(Shielded(), EnemyKind.Shielded, 130f, -1.5f)
            };
            autumn.exitPosition = new Vector2(136f, -1.5f);
            autumn.nextScene = LevelSceneNames[3];
            yield return autumn;

            // --- Winter: all three types, mixed. Leads into the arena. ---
            var winter = New(LevelSceneNames[3], "Winter — The Watching Blade", Season.Winter);
            winter.skyColor = new Color(0.74f, 0.82f, 0.90f);
            winter.groundColor = new Color(0.20f, 0.24f, 0.32f);
            winter.accentColor = new Color(0.92f, 0.96f, 1f);
            winter.ambientLight = new Color(0.86f, 0.92f, 1f);
            winter.playerStart = new Vector2(0f, -1.5f);
            winter.slabs = new[]
            {
                Slab(-6f, 24f, -3f),
                Slab(30f, 46f, -2f),
                Slab(52f, 66f, 0f),
                Slab(72f, 96f, -3f),
                Slab(102f, 118f, -1f),
                Slab(124f, 152f, -3f)
            };
            winter.enemies = new[]
            {
                At(Rusher(), EnemyKind.Rusher, 16f, -1.5f),
                At(Ranged(), EnemyKind.Ranged, 38f, -0.5f),
                At(Shielded(), EnemyKind.Shielded, 58f, 1.5f),
                At(Rusher(), EnemyKind.Rusher, 80f, -1.5f),
                At(Ranged(), EnemyKind.Ranged, 90f, -1.5f),
                At(Shielded(), EnemyKind.Shielded, 110f, 0.5f),
                At(Rusher(), EnemyKind.Rusher, 132f, -1.5f),
                At(Shielded(), EnemyKind.Shielded, 142f, -1.5f)
            };
            winter.exitPosition = new Vector2(148f, -1.5f);
            winter.nextScene = LevelSceneNames[4];
            yield return winter;

            // --- The arena: closed box, ink-black (spec §4), one boss, no way out. ---
            var arena = New(LevelSceneNames[4], "The Arena", Season.BossArena);
            arena.skyColor = new Color(0.06f, 0.07f, 0.10f);
            arena.groundColor = new Color(0.11f, 0.11f, 0.15f);
            arena.accentColor = new Color(0.85f, 0.20f, 0.30f);
            arena.ambientLight = new Color(0.62f, 0.66f, 0.80f);
            arena.playerStart = new Vector2(-16f, -1.5f);
            arena.slabs = new[]
            {
                Slab(-24f, 24f, -3f, 2f),
                // Walls, so a two-phase fight cannot be walked away from.
                Slab(-26f, -24f, 10f, 13f),
                Slab(24f, 26f, 10f, 13f)
            };
            arena.enemies = new[]
            {
                At(Boss(), EnemyKind.Boss, 14f, -1f)
            };
            // No exit: the EndScreen resolves this one when the boss dies.
            arena.nextScene = "";
            yield return arena;
        }

        /// <summary>Opening value for the edge (spec §2.6). A nudge; playtesting moves it.</summary>
        private const float DefaultEdgeMultiplier = 1.2f;

        /// <summary>
        /// Which weapon a season sharpens (spec §2.6). The one place this mapping is written —
        /// both the level defaults and the retrofit read it here, so the two cannot disagree.
        ///
        /// Winter and the arena return None, and that is the design rather than a gap: there are
        /// three weapons and four seasons, and winter is where the game stops helping.
        /// </summary>
        private static SharpenedWeapon EdgeFor(Season season) => season switch
        {
            Season.Spring => SharpenedWeapon.Sword,     // the season you learn it in
            Season.Summer => SharpenedWeapon.Bow,       // archers on perches; range duels range
            Season.Autumn => SharpenedWeapon.Sickle,    // narrow ledges force close quarters
            _ => SharpenedWeapon.None
        };

        private static LevelData New(string assetName, string displayName, Season season)
        {
            var data = ScriptableObject.CreateInstance<LevelData>();
            data.name = assetName;
            data.displayName = displayName;
            data.season = season;
            data.sharpenedWeapon = EdgeFor(season);
            data.edgeMultiplier = DefaultEdgeMultiplier;
            return data;
        }

        private static LevelData.Slab Slab(float startX, float endX, float topY, float thickness = 1f)
            => new LevelData.Slab
            {
                startX = startX,
                endX = endX,
                topY = topY,
                thickness = thickness
            };

        private static LevelData.Placement At(EnemyData data, EnemyKind kind, float x, float y)
            => new LevelData.Placement
            {
                data = data,
                kind = kind,
                position = new Vector2(x, y)
            };

        private static EnemyData Get(Dictionary<EnemyKind, EnemyData> enemies, EnemyKind kind)
            => enemies.TryGetValue(kind, out var data) ? data : null;
    }
}
