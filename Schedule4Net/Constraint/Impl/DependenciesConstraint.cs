using System;
using Schedule4Net.Core;

namespace Schedule4Net.Constraint.Impl
{
    /// <summary>
    /// This constraint ensures that item-dependencies are met by the scheduler. The dependencies are given by the <see cref="ItemToSchedule"/>'s <c>RequiredItems</c> property.
    /// 
    /// This is a hard constraint.
    /// </summary>
    public sealed class DependenciesConstraint : ItemPairConstraint
    {
        public ConstraintDecision Check(ScheduledItem item1, ScheduledItem item2)
        {
            bool isFulfilled = true;
            int overlappedValue = 0;
            ItemToSchedule itemToSchedule1 = item1.ItemToSchedule;
            ItemToSchedule itemToSchedule2 = item2.ItemToSchedule;
            int distanceToEnd = 0;
            if (itemToSchedule1.RequiredItems.Contains(itemToSchedule2))
            {
                distanceToEnd = ScheduleUtil.GetMinimumDistanceToEnd(item2, item1);
            }
            else if (itemToSchedule2.RequiredItems.Contains(itemToSchedule1))
            {
                distanceToEnd = ScheduleUtil.GetMinimumDistanceToEnd(item1, item2);
            }
            if (distanceToEnd < 0)
            {
                isFulfilled = false;
                overlappedValue = Math.Max(item1.ItemToSchedule.DurationSummary, item2.ItemToSchedule.DurationSummary);
            }
            return new ConstraintDecision(true, isFulfilled, overlappedValue);
        }

        public bool NeedsChecking(ItemToSchedule item1, ItemToSchedule item2)
        {
            return item1.RequiredItems.Contains(item2) || item2.RequiredItems.Contains(item1);
        }

        public ConstraintPrediction PredictDecision(ItemToSchedule movedItem, ItemToSchedule fixItem)
        {
            int conflictValue = Math.Max(movedItem.DurationSummary, fixItem.DurationSummary);
            if (fixItem.RequiredItems.Contains(movedItem))
            {
                return new ConstraintPrediction(ConstraintPrediction.Prediction.NoConflict, ConstraintPrediction.Prediction.Conflict, ConstraintPrediction.Prediction.Conflict, conflictValue);
            }
            if (movedItem.RequiredItems.Contains(fixItem))
            {
                return new ConstraintPrediction(ConstraintPrediction.Prediction.Conflict, ConstraintPrediction.Prediction.Conflict, ConstraintPrediction.Prediction.NoConflict, conflictValue);
            }
            return new ConstraintPrediction(ConstraintPrediction.Prediction.NoConflict, ConstraintPrediction.Prediction.NoConflict, ConstraintPrediction.Prediction.NoConflict, 0);
        }
    }
}
