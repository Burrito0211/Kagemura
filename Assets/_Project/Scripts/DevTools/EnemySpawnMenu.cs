using System;
using System.Collections.Generic;
using Kagemura.Enemies;
using Kagemura.Player;
using Kagemura.Systems;
using Kagemura.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kagemura.DevTools
{
    /// <summary>
    /// Dev tool: press ` to open a menu that drops an enemy in front of the player.
    ///
    /// Exists because there are three enemy types and a boss whose numbers have never been felt,
    /// and the alternative to this is hand-building a GameObject in the scene every time you want
    /// to see whether a guard threshold or a phase timing is right.
    ///
    /// It builds enemies from code rather than from prefabs, so it works with no setup at all —
    /// but that means it also encodes the three things that silently break a hand-built enemy,
    /// each of which cost time to find the hard way:
    ///   - The collider goes on the same GameObject as Health, or weapons find it and then fail
    ///     to find anything damageable on it.
    ///   - The enemy layer has to be one the player's weapons actually target.
    ///   - Its own target layers have to include the player, or it tracks you and swings through you.
    ///
    /// All three are detected from what is already in the scene rather than asked for, so the
    /// menu cannot be misconfigured into spawning enemies that look fine and do nothing.
    ///
    /// Delete this folder before shipping; nothing in the game references it.
    /// </summary>
    public class EnemySpawnMenu : GreyboxMenu
    {
        public enum Kind { Rusher, Ranged, Shielded, Boss }

        [Serializable]
        public struct Entry
        {
            [Tooltip("Button label.")]
            public string label;
            [Tooltip("Which controller to attach.")]
            public Kind kind;
            [Tooltip("Stats asset. A Boss entry needs a BossData asset.")]
            public EnemyData data;
        }

        [Header("Spawnable")]
        [Tooltip("One button per entry. Right-click this component > Find Enemy Data to fill it " +
                 "from the project.")]
        [SerializeField] private Entry[] entries;

        [Header("Open")]
        [SerializeField] private string toggleControl = "<Keyboard>/backquote";

        [Header("Placement")]
        [Tooltip("How far in front of the player to drop it.")]
        [SerializeField] private float spawnDistance = 6f;
        [Tooltip("Height above the player to drop from, so it lands rather than clipping ground.")]
        [SerializeField] private float spawnHeight = 1.5f;

        [Header("Layers (auto-detected when left unset)")]
        [Tooltip("Layer to put spawned enemies on. Taken from an existing enemy if 0.")]
        [SerializeField] private int enemyLayer;
        [Tooltip("Layers spawned enemies attack. Taken from the player if 0.")]
        [SerializeField] private LayerMask enemyTargetLayers;
        [Tooltip("Terrain that stops bolts and sight. Taken from the player's ground layer if 0.")]
        [SerializeField] private LayerMask blockingLayers;

        [Header("Greybox Body")]
        [SerializeField] private Vector2 bodySize = new Vector2(1f, 1.8f);
        [SerializeField] private Vector2 bossBodySize = new Vector2(1.8f, 2.6f);

        private InputAction _toggleAction;
        private Transform _player;

        protected override string Title => "SPAWN";

        protected override void Awake()
        {
            _player = ResolvePlayer();
            ResolveLayers();

            // Before base.Awake(), which builds one button per entry.
            if (entries == null || entries.Length == 0)
                Debug.LogWarning("[EnemySpawnMenu] No entries. Right-click the component and " +
                                 "choose 'Find Enemy Data' to fill it from the project.", this);

            base.Awake();
        }

        private void OnEnable()
        {
            // Bound directly rather than through the action asset: this menu suspends the Player
            // map while it is open, so a key living in that map could never close it again.
            _toggleAction = new InputAction("ToggleSpawnMenu", InputActionType.Button, toggleControl);
            _toggleAction.Enable();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            _toggleAction?.Disable();
            _toggleAction?.Dispose();
            _toggleAction = null;
        }

        private void Update()
        {
            if (_toggleAction != null && _toggleAction.WasPressedThisFrame()) Toggle();
        }

        protected override IEnumerable<MenuItem> BuildItems()
        {
            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    var captured = entry;      // captured per button, not per loop
                    string label = string.IsNullOrEmpty(entry.label)
                        ? entry.kind.ToString()
                        : entry.label;

                    yield return new MenuItem(label, () => SpawnAndClose(captured));
                }
            }

            yield return new MenuItem("Close", Close);
        }

        private void SpawnAndClose(Entry entry)
        {
            Spawn(entry);
            Close();
        }

        /// <summary>
        /// Build one enemy and drop it in front of the player.
        ///
        /// Assembled while inactive on purpose: adding a controller to a live GameObject runs its
        /// Awake immediately, which reads the stats asset — so the data has to be in place before
        /// the object is switched on, or the enemy wakes up with nothing and logs an error.
        /// </summary>
        public GameObject Spawn(Entry entry)
        {
            if (entry.data == null)
            {
                Debug.LogError($"[EnemySpawnMenu] '{entry.label}' has no EnemyData assigned.", this);
                return null;
            }

            if (entry.kind == Kind.Boss && !(entry.data is BossData))
            {
                Debug.LogError($"[EnemySpawnMenu] '{entry.label}' is a Boss entry but its asset is " +
                               "plain EnemyData. The boss needs a BossData asset.", this);
                return null;
            }

            bool isBoss = entry.kind == Kind.Boss;
            Vector2 size = isBoss ? bossBodySize : bodySize;

            var go = new GameObject($"{entry.data.displayName} (spawned)");
            go.SetActive(false);
            go.layer = enemyLayer;
            go.transform.position = SpawnPosition();

            // The body is a child so the root can stay at unit scale. Scaling the root is the
            // obvious way to size a greybox, but everything else parented to it inherits the
            // stretch — a 1x1.8 body would hand the health bar's world canvas the same 1.8, and
            // the boss would get a bar half again as wide as its own. EnemyBase finds this with
            // GetComponentInChildren, so the controllers are unaffected.
            var bodyGo = new GameObject("Body", typeof(SpriteRenderer));
            bodyGo.transform.SetParent(go.transform, false);
            bodyGo.transform.localScale = new Vector3(size.x, size.y, 1f);

            var sprite = bodyGo.GetComponent<SpriteRenderer>();
            sprite.sprite = GreyboxArt.WhiteSprite();
            sprite.color = TintFor(entry.kind);
            sprite.drawMode = SpriteDrawMode.Simple;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.freezeRotation = true;
            rb.gravityScale = 1f;

            // On this GameObject, not a child: weapons overlap for colliders and then ask the
            // collider they hit for IDamageable, which lives on Health here.
            var col = go.AddComponent<BoxCollider2D>();
            col.size = size;                     // the root is unit scale, so this is world size

            // Adding the controller pulls in Health via RequireComponent.
            EnemyBase enemy = entry.kind switch
            {
                Kind.Ranged => go.AddComponent<EnemyRanged>(),
                Kind.Shielded => go.AddComponent<EnemyShielded>(),
                Kind.Boss => go.AddComponent<BossController>(),
                _ => go.AddComponent<EnemyRusher>()
            };

            // WorldHealthBar, not HUDController: the latter is the player's screen-corner HUD and
            // binds to the player's own Health, so one per enemy would stack duplicate full-screen
            // canvases that disappear as their enemy dies. Health is already on the object by now,
            // pulled in by the controller above, which is what this bar's RequireComponent wants.
            var healthBar = go.AddComponent<WorldHealthBar>();
            healthBar.SetHeightOffset(size.y * 0.5f + 0.35f);   // clears the body at any height

            enemy.Configure(entry.data, enemyTargetLayers, blockingLayers);

            go.SetActive(true);
            return go;
        }

        private Vector3 SpawnPosition()
        {
            if (_player == null) _player = ResolvePlayer();
            if (_player == null) return transform.position;

            // In front of where the player is facing, so it appears where you are looking.
            int facing = 1;
            if (_player.TryGetComponent<PlayerController>(out var controller)) facing = controller.Facing;

            return _player.position + new Vector3(spawnDistance * facing, spawnHeight, 0f);
        }

        private static Color TintFor(Kind kind) => kind switch
        {
            Kind.Boss => new Color(0.85f, 0.2f, 0.3f),
            _ => new Color(1f, 1f, 1f)
        };

        private static Transform ResolvePlayer()
        {
            var player = FindFirstObjectByType<PlayerController>();
            return player != null ? player.transform : null;
        }

        /// <summary>
        /// Read the three layer settings off the scene instead of trusting the inspector. An enemy
        /// on the wrong layer is invisible to every weapon, and looks like a damage bug rather
        /// than a setup one — which is exactly the trap this tool exists to avoid re-laying.
        /// </summary>
        private void ResolveLayers()
        {
            if (enemyLayer == 0)
            {
                var existing = FindFirstObjectByType<EnemyBase>();
                if (existing != null) enemyLayer = existing.gameObject.layer;
                else Debug.LogWarning("[EnemySpawnMenu] No enemy in the scene to copy a layer " +
                                      "from, so spawns land on Default and your weapons will " +
                                      "most likely not target them. Set Enemy Layer.", this);
            }

            if (enemyTargetLayers.value == 0)
            {
                if (_player != null) enemyTargetLayers = 1 << _player.gameObject.layer;
                else Debug.LogWarning("[EnemySpawnMenu] No player found, so spawned enemies have " +
                                      "nothing to attack. Set Enemy Target Layers.", this);
            }

            if (blockingLayers.value == 0)
            {
                // Best effort: whatever the ground is standing on. Ranged types only need this to
                // stop shooting through walls, so a wrong guess is cosmetic rather than fatal.
                var ground = GameObject.Find("Ground");
                if (ground != null) blockingLayers = 1 << ground.layer;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Fill the entry list from every EnemyData in the project. Editor-only convenience so the
        /// tool works without hand-wiring; the kind is guessed from the asset type and name, which
        /// is why it is a one-off command you can then correct rather than something done silently
        /// at runtime.
        /// </summary>
        [ContextMenu("Find Enemy Data")]
        private void FindEnemyData()
        {
            var found = new List<Entry>();

            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:EnemyData"))
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyData>(path);
                if (asset == null) continue;

                string assetName = asset.name.ToLowerInvariant();
                Kind kind = asset is BossData ? Kind.Boss
                          : assetName.Contains("range") ? Kind.Ranged
                          : assetName.Contains("shield") ? Kind.Shielded
                          : Kind.Rusher;

                found.Add(new Entry { label = asset.displayName, kind = kind, data = asset });
            }

            entries = found.ToArray();
            UnityEditor.EditorUtility.SetDirty(this);

            Debug.Log($"[EnemySpawnMenu] Found {found.Count} EnemyData asset(s). Check the Kind " +
                      "column — it is guessed from the asset name.", this);
        }
#endif
    }
}
