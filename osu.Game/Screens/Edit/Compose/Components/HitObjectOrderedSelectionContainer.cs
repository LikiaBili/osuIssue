// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Screens.Edit.Compose.Components
{
    /// <summary>
    /// A container for <see cref="SelectionBlueprint{HitObject}"/> ordered by their <see cref="HitObject"/> start times.
    /// </summary>
    public sealed class HitObjectOrderedSelectionContainer : Container<SelectionBlueprint<HitObject>>
    {
        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            editorBeatmap.HitObjectUpdated += hitObjectUpdated;
        }

        private void hitObjectUpdated(HitObject _) => SortInternal();

        public override void Add(SelectionBlueprint<HitObject> drawable)
        {
            SortInternal();
            base.Add(drawable);
        }

        public override bool Remove(SelectionBlueprint<HitObject> drawable)
        {
            SortInternal();
            return base.Remove(drawable);
        }

        protected override int Compare(Drawable x, Drawable y)
        {
            var xObj = (SelectionBlueprint<HitObject>)x;
            var yObj = (SelectionBlueprint<HitObject>)y;

            // Put earlier blueprints towards the end of the list, so they handle input first
            int i = yObj.Item.StartTime.CompareTo(xObj.Item.StartTime);
            if (i != 0) return i;

            // Fall back to end time if the start time is equal.
            i = yObj.Item.GetEndTime().CompareTo(xObj.Item.GetEndTime());
            if (i != 0) return i;

            // As a final fallback, use combo information if available.
            if (xObj.Item is IHasComboInformation xHasCombo && yObj.Item is IHasComboInformation yHasCombo)
            {
                i = yHasCombo.ComboIndex.CompareTo(xHasCombo.ComboIndex);
                if (i != 0) return i;

                i = yHasCombo.IndexInCurrentCombo.CompareTo(xHasCombo.IndexInCurrentCombo);
                if (i != 0) return i;
            }

            return CompareReverseChildID(y, x);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (editorBeatmap != null)
                editorBeatmap.HitObjectUpdated -= hitObjectUpdated;
        }
    }
}
