namespace Schedule4Net.Constraint
{
    public interface ItemPairConstraint
    {
        ConstraintDecision Check(ScheduledItem item1, ScheduledItem item2);

        bool NeedsChecking(ItemToSchedule item1, ItemToSchedule item2);

        //TODO: does not separate between hard- and soft constraints
        ConstraintPrediction PredictDecision(ItemToSchedule movedItem, ItemToSchedule fixItem);
    }
}
