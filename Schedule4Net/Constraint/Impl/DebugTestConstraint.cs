using Schedule4Net.Core;

namespace Schedule4Net.Constraint.Impl
{
    public sealed class DebugTestConstraint : ItemPairConstraint
    {
        ConstraintDecision ItemPairConstraint.Check(ScheduledItem item1, ScheduledItem item2)
        {
            bool isFulfilled = true;
            int overlappedValue = 0;
            if (item1.ItemToSchedule.Id % 10 == item2.ItemToSchedule.Id % 10)
            {
                foreach (Lane lane1 in item1.ItemToSchedule.Lanes)
                {
                    foreach (Lane lane2 in item2.ItemToSchedule.Lanes)
                    {
                        int overlapping = ScheduleUtil.GetOverlappingValue(item1.Start, item1.GetEnd(lane1), item2.Start,
                                item2.GetEnd(lane2));
                        if (overlapping <= 0) continue;
                        isFulfilled = false;
                        overlappedValue += overlapping;
                    }
                }
            }
            return new ConstraintDecision(true, isFulfilled, overlappedValue);
        }

        bool ItemPairConstraint.NeedsChecking(ItemToSchedule item1, ItemToSchedule item2)
        {
            return item1.Id % 10 == item2.Id % 10;
        }

        ConstraintPrediction ItemPairConstraint.PredictDecision(ItemToSchedule movedItem, ItemToSchedule fixItem)
        {
            bool idsCollide = movedItem.Id % 10 == fixItem.Id % 10;
            int overlappedValue = 0;
            if (idsCollide)
            {
                foreach (Lane lane1 in movedItem.Lanes)
                {
                    foreach (Lane lane2 in fixItem.Lanes)
                    {
                        overlappedValue += ScheduleUtil.GetOverlappingValue(0, movedItem.GetDuration(lane1), 0, fixItem.GetDuration(lane2));
                    }
                }
            }
            return new ConstraintPrediction(ConstraintPrediction.Prediction.NoConflict, idsCollide ? ConstraintPrediction.Prediction.Conflict : ConstraintPrediction.Prediction.NoConflict,
                    ConstraintPrediction.Prediction.NoConflict, overlappedValue);
        }
    }
}
