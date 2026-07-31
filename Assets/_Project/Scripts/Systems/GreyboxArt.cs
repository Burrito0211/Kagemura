using UnityEngine;

namespace Kagemura.Systems
{
    /// <summary>
    /// Placeholder art shared by everything built from code during greybox — HUD bars, the
    /// arrow, the special aim previews. All of it goes away at the art pass; keeping it in one
    /// place means there's one thing to delete rather than four copies to hunt down.
    /// </summary>
    public static class GreyboxArt
    {
        private static Sprite _white;

        /// <summary>
        /// A 1x1 white sprite, one world unit square. World sprites scale it via
        /// transform.localScale; UI Images ignore its size and take the RectTransform's.
        ///
        /// UI note: a sprite is mandatory for any Image using Image.Type.Filled — a sprite-less
        /// Image draws a plain quad and silently ignores fillAmount, so the bar sits full forever.
        /// </summary>
        public static Sprite WhiteSprite()
        {
            if (_white != null) return _white;

            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();

            // 1 pixel-per-unit, so the sprite is exactly one unit across.
            _white = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            return _white;
        }
    }
}
