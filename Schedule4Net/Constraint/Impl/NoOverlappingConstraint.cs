using System.Collections.Generic;
using System.Linq;
using Schedule4Net.Core;

namespace Schedule4Net.Constraint.Impl
{
    public class NoOverlappingConstraint : ItemPairConstraint
    {
        public ConstraintDecision Check(ScheduledItem item1, ScheduledItem item2)
        {
            bool isFulfilled = true;
            int overlappedValue = 0;
            ISet<Lane> lanes = new HashSet<Lane>(item1.ItemToSchedule.Lanes);
            lanes.IntersectWith(item2.ItemToSchedule.Lanes);
            foreach (Lane lane in lanes)
            {
                int overlapping = ScheduleUtil.GetOverlappingValue(item1.Start, item1.GetEnd(lane), item2.Start, item2.GetEnd(lane));
                if (overlapping <= 0) continue;
                isFulfilled = false;
                overlappedValue += overlapping;
            }
            return new ConstraintDecision(true, isFulfilled, overlappedValue);
        }

        public bool NeedsChecking(ItemToSchedule item1, ItemToSchedule item2)
        {
            ISet<Lane> lanes = new HashSet<Lane>(item1.Lanes);
            lanes.IntersectWith(item2.Lanes);
            return lanes.Count != 0;
        }

        public ConstraintPrediction PredictDecision(ItemToSchedule movedItem, ItemToSchedule fixItem)
        {
            ISet<Lane> lanes = new HashSet<Lane>(movedItem.Lanes);
            lanes.IntersectWith(fixItem.Lanes);
            int overlappedValue = lanes.Sum(lane => ScheduleUtil.GetOverlappingValue(0, movedItem.GetDuration(lane), 0, fixItem.GetDuration(lane)));
            return new ConstraintPrediction(ConstraintPrediction.Prediction.NoConflict, lanes.Count == 0 ? ConstraintPrediction.Prediction.NoConflict : ConstraintPrediction.Prediction.Conflict, ConstraintPrediction.Prediction.NoConflict, overlappedValue);
        }
    }
}
