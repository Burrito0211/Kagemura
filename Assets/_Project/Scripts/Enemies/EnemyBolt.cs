using Kagamura.Player.Weapons;
using Kagamura.Systems;
using UnityEngine;

namespace Kagamura.Enemies
{
    /// <summary>
    /// One place to make the projectiles enemies throw, shared by the archer and the boss.
    ///
    /// The projectile itself is ArrowProjectile, the same script the player's bow fires — it
    /// already flies straight, damages the first thing on the target layers and dies on terrain,
    /// and it credits Focus only to a source that owns a Focus pool, so an enemy firing one is
    /// inert on that path.
    ///
    /// Greybox construction only. Assign a prefab and none of the building below runs.
    /// </summary>
    public static class EnemyBolt
    {
        public static ArrowProjectile Spawn(ArrowProjectile prefab, Vector3 position,
                                            Vector2 size, Color color)
        {
            if (prefab != null)
                return Object.Instantiate(prefab, position, Quaternion.identity);

            var go = new GameObject("Yokai Bolt (greybox)");
            go.transform.position = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var sprite = go.AddComponent<SpriteRenderer>();
            sprite.sprite = GreyboxArt.WhiteSprite();
            sprite.color = color;
            sprite.sortingOrder = 10;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;               // flat flight, so the dodge window is constant
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;             // scaled to size by the transform above
            col.isTrigger = true;

            return go.AddComponent<ArrowProjectile>();
        }
    }
}
