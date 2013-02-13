namespace Schedule4Net.Constraint
{
    public interface SingleItemConstraint
    {
        ConstraintDecision Check(ScheduledItem item);
    }
}
