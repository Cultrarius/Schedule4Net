namespace Schedule4Net.Constraint.Impl
{
    /// <summary>
    /// This constraint tells the scheduler that items should start rather sooner than later, so the scheduler will move items as close to the start time as possible.
    /// 
    /// This is a soft constraint.
    /// </summary>
    public sealed class StartNowConstraint : SingleItemConstraint
    {
        public ConstraintDecision Check(ScheduledItem item)
        {
            return new ConstraintDecision(false, item.Start == 0, item.Start + item.ItemToSchedule.DurationSummary);
        }
    }
}
