﻿namespace Schedule4Net.Constraint.Impl
{
    class StartNowConstraint : SingleItemConstraint
    {
        public ConstraintDecision Check(ScheduledItem item)
        {
            return new ConstraintDecision(false, item.Start == 0, item.Start + item.ItemToSchedule.DurationSummary);
        }
    }
}