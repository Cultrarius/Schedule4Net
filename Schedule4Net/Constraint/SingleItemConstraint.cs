namespace Schedule4Net.Constraint
{
    /// <summary>
    /// This constraint is provided with a single scheduled item and has to decide if this items violates the constraint or not.
    /// It is used if a given scheduled item can be evaluated without looking at any other item in the schedule.
    /// </summary>
    public interface SingleItemConstraint
    {
        /// <summary>
        /// Checks if the specified item violates the constraint.
        /// </summary>
        /// <param name="item">The item to check for a constraint violation.</param>
        /// <returns>A decision that indicates if the constraint is fulfilled and if it is a soft or a hard constraint.</returns>
        ConstraintDecision Check(ScheduledItem item);
    }
}
