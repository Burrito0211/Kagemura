using System;
using UnityEngine;

namespace Kagemura.Narrative
{
    /// <summary>
    /// One story beat, authored as an asset (spec §6) so the writing lives outside the code and
    /// can be edited without a recompile.
    ///
    /// Answers spec §9's open question on story delivery in the cheapest of the three listed
    /// forms: text boxes. No portraits, no voice. The speaker is a plain string rather than a
    /// character asset, so adding a portrait later means adding one field here and one Image in
    /// DialogueUI — not restructuring what has already been written.
    ///
    /// Spec §3.2 wants beats placed at natural pacing breaks, which is why these are assets
    /// dropped onto triggers rather than a timeline: moving a beat is moving a trigger.
    /// </summary>
    [CreateAssetMenu(fileName = "Dialogue", menuName = "Kagemura/Dialogue")]
    public class DialogueData : ScriptableObject
    {
        [Serializable]
        public struct Line
        {
            [Tooltip("Who is speaking. Leave empty for unattributed narration.")]
            public string speaker;

            [TextArea(2, 5)]
            public string text;
        }

        [Tooltip("Shown in order, one box at a time.")]
        public Line[] lines;

        [Header("Playback")]
        [Tooltip("Freeze the game while this plays. On for story beats; off for barks that " +
                 "should not interrupt a fight.")]
        public bool pauseGame = true;

        [Tooltip("Characters revealed per second. 0 shows each line instantly.")]
        public float charactersPerSecond = 45f;
    }
}
