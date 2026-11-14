using UnityEngine;

namespace Kagemura.Feel
{
    /// <summary>
    /// The current camera shake, as an offset for whoever positions the camera to add on.
    ///
    /// Deliberately only a value. <see cref="CombatFeel"/> writes it once a frame and
    /// CameraFollow reads it, so neither knows the other exists, and the camera's own smoothing
    /// never sees the shake — if the offset were written straight to the camera transform, the
    /// follow would chase it and smear a sharp jolt into a wobble.
    /// </summary>
    public static class ScreenShake
    {
        /// <summary>World-space offset to add to the camera this frame. Zero when still.</summary>
        public static Vector2 Offset { get; internal set; }
    }
}
