namespace Schedule4Net.Constraint
{
    /// <summary>
    /// This constraint is provided with two items from the schedule and has to decide if they violate the constraint or not.
    /// For example, such a constraint could check if the execution times of two items overlap or if some dependency logic is fulfilled ("schedule item A after item B").
    /// </summary>
    public interface ItemPairConstraint
    {
        /// <summary>
        /// Checks if the specified items violate the constraint.
        /// </summary>
        /// <param name="item1">The first item to check.</param>
        /// <param name="item2">The second item to check.</param>
        /// <returns>A decision that indicates if the constraint is fulfilled and if it is a soft or a hard constraint.</returns>
        ConstraintDecision Check(ScheduledItem item1, ScheduledItem item2);

        /// <summary>
        /// This method is used as an optimization by the scheduler to see if this constraint never has to check the specified pair of items (because they can never violate the constraint).
        /// Note that this can also be a SwitchLaneItem that can have additional, optional durations.
        /// </summary>
        /// <param name="item1">The item1.</param>
        /// <param name="item2">The item2.</param>
        /// <returns></returns>
        bool NeedsChecking(ItemToSchedule item1, ItemToSchedule item2);

        /// <summary>
        /// This method is used as an optimization by the scheduler to predict the constraint decision and make the execution of the constraint unnecessary in some cases.
        /// For example, a constraint that checks if two items overlap is able to predict that two items will never violate the constraint if one is scheduled after the other.
        /// </summary>
        /// <param name="movedItem">The item whole execution time is moved before, during and after the fix item's execution time.</param>
        /// <param name="fixItem">The fix item.</param>
        /// <returns>A prediction that states if the constraint will be violated under certain conditions.</returns>
        ConstraintPrediction PredictDecision(ItemToSchedule movedItem, ItemToSchedule fixItem);
    }
}
