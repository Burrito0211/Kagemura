using System;
using Kagemura.Enemies;
using UnityEngine;

namespace Kagemura.Levels
{
    /// <summary>Which season a level wears (spec §3.1). The arena is the boss, after winter.</summary>
    public enum Season { Spring, Summer, Autumn, Winter, BossArena }

    /// <summary>
    /// Build Order step 9: one level, described as data.
    ///
    /// The levels are handcrafted, not procedural (spec §3.1) — this is where the handcrafting
    /// lives. Layout, enemy placement and palette are authored here and baked into a scene by
    /// LevelSceneSetup, rather than dragged into the scene by hand, for two reasons:
    ///
    ///   - Greybox exists to be thrown away. Every slab in these levels is a stand-in for art that
    ///     does not exist yet, and moving a gap by two units should not mean hunting a BoxCollider
    ///     in a hierarchy. Retuning here and rebuilding is faster than editing five scenes.
    ///   - The four seasons are the same game re-skinned (spec §3.1 again — seasonal palette shifts
    ///     are meant to carry the visual variety). Keeping the palette beside the layout is what
    ///     makes a season a single asset you can look at whole.
    ///
    /// This is a starting point for the art pass, not a permanent format. Once real tiles and
    /// backdrops exist the scenes get hand-dressed and this stops being rebuilt — nothing at
    /// runtime reads a LevelData, so it can simply stop being used.
    /// </summary>
    [CreateAssetMenu(fileName = "Level", menuName = "Kagemura/Level Data")]
    public class LevelData : ScriptableObject
    {
        /// <summary>
        /// One piece of terrain. Authored as a top edge rather than a centre, because what matters
        /// when placing a platform is the surface the player lands on — the thickness below it is
        /// only there to have something to see.
        /// </summary>
        [Serializable]
        public struct Slab
        {
            [Tooltip("Left edge, world X.")]
            public float startX;
            [Tooltip("Right edge, world X. Must be greater than Start X.")]
            public float endX;
            [Tooltip("Height of the walkable surface, world Y.")]
            public float topY;
            [Tooltip("How deep the slab is drawn below its surface.")]
            public float thickness;

            public float Width => endX - startX;
            public Vector2 Size => new Vector2(Mathf.Abs(Width), Mathf.Max(0.1f, thickness));
            public Vector2 Center => new Vector2(startX + Width * 0.5f, topY - Size.y * 0.5f);
        }

        /// <summary>Where one enemy stands at level start. Y is the ground it spawns above.</summary>
        [Serializable]
        public struct Placement
        {
            [Tooltip("Stats asset. A Boss kind needs a BossData asset.")]
            public EnemyData data;
            [Tooltip("Which controller it gets.")]
            public EnemyKind kind;
            [Tooltip("World position. Drop it slightly above the surface so it lands rather than " +
                     "starting clipped into the slab.")]
            public Vector2 position;
        }

        [Header("Identity")]
        public string displayName = "Level";
        public Season season = Season.Spring;

        [Header("Palette (spec §4)")]
        [Tooltip("Sky behind everything — the gameplay camera's clear colour.")]
        public Color skyColor = new Color(0.16f, 0.18f, 0.35f);
        [Tooltip("Terrain tint.")]
        public Color groundColor = new Color(0.30f, 0.28f, 0.34f);
        [Tooltip("The one colour that shifts per season. Used on the exit marker.")]
        public Color accentColor = new Color(0.95f, 0.55f, 0.65f);
        [Tooltip("Global 2D light colour — how warm or cold the whole level reads.")]
        public Color ambientLight = Color.white;

        [Header("Layout")]
        public Vector2 playerStart = new Vector2(2f, -1f);
        public Slab[] slabs;

        [Header("Enemies")]
        public Placement[] enemies;

        [Header("Exit")]
        [Tooltip("Where the level ends. The exit marker is centred here.")]
        public Vector2 exitPosition = new Vector2(100f, -1.5f);
        [Tooltip("Scene the exit loads. Empty means this level has no exit — the boss arena ends " +
                 "at the EndScreen instead, so it wants this blank.")]
        public string nextScene = "";

        [Header("Falling")]
        [Tooltip("Below this world Y the player has fallen out of the level and dies. Keep it " +
                 "well under the lowest slab.")]
        public float killPlaneY = -20f;

        /// <summary>
        /// Horizontal span of the terrain, padded. Used to size the kill plane so it catches a
        /// fall anywhere in the level, including off the far end.
        /// </summary>
        public void GetHorizontalBounds(out float minX, out float maxX)
        {
            minX = playerStart.x;
            maxX = Mathf.Max(playerStart.x, exitPosition.x);

            if (slabs != null)
            {
                foreach (var slab in slabs)
                {
                    minX = Mathf.Min(minX, slab.startX);
                    maxX = Mathf.Max(maxX, slab.endX);
                }
            }

            minX -= 20f;
            maxX += 20f;
        }
    }
}
