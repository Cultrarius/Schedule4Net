namespace Schedule4Net.Constraint
{
    public class ConstraintDecision
    {
        public bool HardConstraint { get; private set; }
        public bool Fulfilled { get; private set; }
        public int ViolationValue { get; private set; }

        public ConstraintDecision(bool hardConstraint, bool isFulfilled, int violationValue)
        {
            HardConstraint = hardConstraint;
            Fulfilled = isFulfilled;
            ViolationValue = violationValue;
        }
    }
}
