namespace Schedule4Net.Constraint
{
    /// <summary>
    /// This represents the result of a constraint check. This result is produced by the <see cref="SingleItemConstraint"/> and <see cref="ItemPairConstraint"/> checks.
    /// The scheduler uses this result during the scheduling algorithm to create a correct schedule.
    /// </summary>
    public sealed class ConstraintDecision
    {
        /// <summary>
        /// Indicaties whether this is a hard constraint or a soft constraint.
        /// </summary>
        public bool HardConstraint { get; private set; }

        /// <summary>
        /// Idicates if the constraint is fulfilled or violated.
        /// </summary>
        public bool Fulfilled { get; private set; }

        /// <summary>
        /// If the constraint is not fulfilled then this value indicates how "big" the violation is.
        /// </summary>
        public int ViolationValue { get; private set; }

        /// <summary>
        /// Creates a new result object for the scheduler.
        /// </summary>
        /// <param name="hardConstraint">If set to <c>true</c> then this result should be seen as a hard constraint. If set to <c>false</c> then this result is seen as a soft constraint and the scheduler can choose to ignore it.</param>
        /// <param name="isFulfilled">If set to <c>true</c> then the constraint is not violated. If <c>false</c> then the constraint is violated and the scheduler should try to solve the violation.</param>
        /// <param name="violationValue">This value determines how "big" the constraint violation is. The bigger the violation the more the scheduler is obliged to solve it.</param>
        public ConstraintDecision(bool hardConstraint, bool isFulfilled, int violationValue)
        {
            HardConstraint = hardConstraint;
            Fulfilled = isFulfilled;
            ViolationValue = violationValue;
        }
    }
}
