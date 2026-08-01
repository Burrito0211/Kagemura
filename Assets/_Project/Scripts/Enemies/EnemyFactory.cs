using Kagemura.Systems;
using Kagemura.UI;
using UnityEngine;

namespace Kagemura.Enemies
{
    /// <summary>Which controller an enemy gets. Chosen by behaviour pattern, not stats (spec §2.5).</summary>
    public enum EnemyKind { Rusher, Ranged, Shielded, Boss }

    /// <summary>
    /// Builds a working enemy GameObject from an <see cref="EnemyData"/> asset.
    ///
    /// Lifted out of the dev spawn menu when the level scenes needed the same thing. The logic is
    /// short but three lines of it are load-bearing, and each one fails silently when it is wrong —
    /// the enemy looks perfect, chases, telegraphs and swings, and simply cannot be hurt or cannot
    /// hurt you. Having two copies of that would mean fixing it twice, and finding out the second
    /// copy was wrong the slow way:
    ///   - The collider goes on the same GameObject as Health, or weapons find the collider and
    ///     then fail to find anything damageable on it.
    ///   - The enemy layer has to be one the player's weapons actually target.
    ///   - The enemy's own target layers have to include the player, or it tracks you and swings
    ///     straight through you.
    ///
    /// Lives outside DevTools deliberately: the spawn menu gets deleted before shipping, and the
    /// level scenes still need this.
    ///
    /// Works at edit time as well as at runtime, which is what lets the level builder bake enemies
    /// into a scene. The one caveat is <see cref="Setup.bodySprite"/> — see its note.
    /// </summary>
    public static class EnemyFactory
    {
        public static readonly Vector2 DefaultBodySize = new Vector2(1f, 1.8f);
        public static readonly Vector2 DefaultBossBodySize = new Vector2(1.8f, 2.6f);

        public struct Setup
        {
            /// <summary>Stats asset. A Boss kind needs a BossData asset.</summary>
            public EnemyData data;
            public EnemyKind kind;

            /// <summary>Layer the enemy sits on. Must be in the player's weapon target mask.</summary>
            public int layer;
            /// <summary>Layers this enemy attacks. Must include the player's layer.</summary>
            public LayerMask targetLayers;
            /// <summary>Terrain that stops bolts and line of sight.</summary>
            public LayerMask blockingLayers;

            /// <summary>Greybox body size. Zero takes the default for the kind.</summary>
            public Vector2 bodySize;

            /// <summary>
            /// Greybox body sprite. Left null, this uses <see cref="GreyboxArt.WhiteSprite"/>,
            /// which is generated in memory — fine at runtime, but it cannot be saved into a scene
            /// or a prefab, so anything baking an enemy at edit time has to pass a real asset here
            /// or the body comes back invisible after the next domain reload.
            /// </summary>
            public Sprite bodySprite;

            /// <summary>Body tint. Unset takes the default for the kind.</summary>
            public Color? bodyColor;

            /// <summary>Name for the new GameObject. Empty takes the data's display name.</summary>
            public string objectName;

            /// <summary>Floating health bar above the body.</summary>
            public bool addHealthBar;
        }

        /// <summary>
        /// Assemble one enemy at <paramref name="position"/>, or null if the setup can't work.
        ///
        /// Built while inactive on purpose: adding a controller to a live GameObject runs its Awake
        /// immediately, and Awake reads the stats asset — so the data has to be in place before the
        /// object switches on, or the enemy wakes with nothing and logs a missing-data error.
        /// </summary>
        public static GameObject Build(Setup setup, Vector3 position)
        {
            if (setup.data == null)
            {
                Debug.LogError("[EnemyFactory] No EnemyData given, so there is nothing to build.");
                return null;
            }

            if (setup.kind == EnemyKind.Boss && !(setup.data is BossData))
            {
                Debug.LogError($"[EnemyFactory] '{setup.data.name}' is being built as a Boss but is " +
                               "plain EnemyData. The boss needs a BossData asset.", setup.data);
                return null;
            }

            Vector2 size = setup.bodySize == Vector2.zero ? DefaultSizeFor(setup.kind) : setup.bodySize;

            var go = new GameObject(string.IsNullOrEmpty(setup.objectName)
                ? setup.data.displayName
                : setup.objectName);
            go.SetActive(false);
            go.layer = setup.layer;
            go.transform.position = position;

            // The body is a child so the root can stay at unit scale. Scaling the root is the
            // obvious way to size a greybox, but everything else parented to it inherits the
            // stretch — a 1x1.8 body would hand the health bar's world canvas the same 1.8, and the
            // boss would get a bar half again as wide as its own. EnemyBase finds the renderer with
            // GetComponentInChildren, so the controllers are unaffected.
            var bodyGo = new GameObject("Body", typeof(SpriteRenderer));
            bodyGo.transform.SetParent(go.transform, false);
            bodyGo.transform.localScale = new Vector3(size.x, size.y, 1f);

            var sprite = bodyGo.GetComponent<SpriteRenderer>();
            sprite.sprite = setup.bodySprite != null ? setup.bodySprite : GreyboxArt.WhiteSprite();
            sprite.color = setup.bodyColor ?? DefaultTintFor(setup.kind);
            sprite.drawMode = SpriteDrawMode.Simple;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.freezeRotation = true;
            rb.gravityScale = 1f;

            // On this GameObject, not the child: weapons overlap for colliders and then ask the
            // collider they hit for IDamageable, which lives on Health here.
            var col = go.AddComponent<BoxCollider2D>();
            col.size = size;                     // the root is unit scale, so this is world size

            // Adding the controller pulls in Health via RequireComponent.
            EnemyBase enemy = setup.kind switch
            {
                EnemyKind.Ranged => go.AddComponent<EnemyRanged>(),
                EnemyKind.Shielded => go.AddComponent<EnemyShielded>(),
                EnemyKind.Boss => go.AddComponent<BossController>(),
                _ => go.AddComponent<EnemyRusher>()
            };

            if (setup.addHealthBar)
            {
                // WorldHealthBar, not HUDController: the latter is the player's screen-corner HUD
                // and binds to the player's own Health, so one per enemy would stack duplicate
                // full-screen canvases that vanish as their enemy dies.
                var healthBar = go.AddComponent<WorldHealthBar>();
                healthBar.SetHeightOffset(size.y * 0.5f + 0.35f);   // clears the body at any height
            }

            enemy.Configure(setup.data, setup.targetLayers, setup.blockingLayers);

            go.SetActive(true);
            return go;
        }

        public static Vector2 DefaultSizeFor(EnemyKind kind) =>
            kind == EnemyKind.Boss ? DefaultBossBodySize : DefaultBodySize;

        /// <summary>
        /// Greybox tint per kind. Only the boss differs so far — the three normal types are told
        /// apart by their telegraph and guard colours, which they set themselves, and giving them
        /// distinct resting colours here would fight those tells rather than add to them.
        /// </summary>
        public static Color DefaultTintFor(EnemyKind kind) => kind switch
        {
            EnemyKind.Boss => new Color(0.85f, 0.2f, 0.3f),
            _ => Color.white
        };

        /// <summary>
        /// Guess the kind from a stats asset. The boss is certain (it has its own type); the rest
        /// are read off the asset name, which is why every caller treats this as a starting point
        /// to be corrected rather than an answer.
        /// </summary>
        public static EnemyKind GuessKind(EnemyData data)
        {
            if (data is BossData) return EnemyKind.Boss;

            string assetName = data.name.ToLowerInvariant();
            if (assetName.Contains("range")) return EnemyKind.Ranged;
            if (assetName.Contains("shield")) return EnemyKind.Shielded;
            return EnemyKind.Rusher;
        }
    }
}
