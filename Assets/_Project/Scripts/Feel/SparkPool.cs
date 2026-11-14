using Kagemura.Systems;
using UnityEngine;

namespace Kagemura.Feel
{
    /// <summary>
    /// Greybox particles: a fixed pool of white squares thrown out from a hit, shrinking as
    /// they go. Built from the same 1x1 sprite as the rest of the greybox art, not a
    /// ParticleSystem, because a ParticleSystem made from code has no material and renders
    /// magenta under URP — it would need a shader lookup that can silently fail in a build.
    ///
    /// Replaced at the art pass by real VFX spawned from the same call sites in CombatFeel.
    /// The pool never grows: when it is full the oldest spark is reused, so a screen full of
    /// hits degrades by dropping sparks rather than by allocating.
    /// </summary>
    internal sealed class SparkPool
    {
        private const int Capacity = 96;
        private const float Gravity = -14f;

        private struct Spark
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float Size;
        }

        private readonly Spark[] _sparks = new Spark[Capacity];
        private int _next;

        public SparkPool(Transform parent, int sortingOrder)
        {
            Sprite sprite = GreyboxArt.WhiteSprite();

            for (int i = 0; i < Capacity; i++)
            {
                var go = new GameObject("Spark");
                go.transform.SetParent(parent, false);

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = sortingOrder;
                renderer.enabled = false;

                _sparks[i].Transform = go.transform;
                _sparks[i].Renderer = renderer;
            }
        }

        /// <summary>
        /// Throw <paramref name="count"/> sparks from a point, in a cone around a direction.
        /// A spread of 180 degrees is a full circle — a burst rather than a spray.
        /// </summary>
        public void Emit(Vector2 position, Vector2 direction, int count, float spreadDegrees,
                         float speed, float lifetime, float size, Color color)
        {
            if (count <= 0 || lifetime <= 0f) return;

            float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            for (int i = 0; i < count; i++)
            {
                ref Spark spark = ref _sparks[_next];
                _next = (_next + 1) % Capacity;

                float angle = (baseAngle + Random.Range(-spreadDegrees, spreadDegrees)) * Mathf.Deg2Rad;

                spark.Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))
                                 * (speed * Random.Range(0.45f, 1f));
                spark.MaxLife = lifetime * Random.Range(0.6f, 1f);
                spark.Life = spark.MaxLife;
                spark.Size = size * Random.Range(0.7f, 1.3f);

                spark.Transform.position = new Vector3(position.x, position.y, 0f);
                spark.Transform.localScale = new Vector3(spark.Size, spark.Size, 1f);
                spark.Renderer.color = color;
                spark.Renderer.enabled = true;
            }
        }

        /// <summary>Advance every live spark. Scaled time, so sparks hang during a hit-stop.</summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            for (int i = 0; i < Capacity; i++)
            {
                ref Spark spark = ref _sparks[i];
                if (spark.Life <= 0f) continue;

                spark.Life -= deltaTime;
                if (spark.Life <= 0f)
                {
                    spark.Renderer.enabled = false;
                    continue;
                }

                spark.Velocity.y += Gravity * deltaTime;
                spark.Transform.position += (Vector3)(spark.Velocity * deltaTime);

                // Shrink to nothing rather than fade: a flat-colour square that shrinks stays
                // crisp, which suits the poster look better than a ghosting alpha.
                float scale = spark.Size * (spark.Life / spark.MaxLife);
                spark.Transform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        /// <summary>Hide everything — called on scene change so nothing hangs in the new level.</summary>
        public void Clear()
        {
            for (int i = 0; i < Capacity; i++)
            {
                _sparks[i].Life = 0f;
                if (_sparks[i].Renderer != null) _sparks[i].Renderer.enabled = false;
            }
        }
    }
}
