namespace Schedule4Net.Constraint
{
    public sealed class ConstraintPrediction
    {
        public Prediction ConflictsWhenBefore { get; private set; }
        public Prediction ConflictsWhenTogether { get; private set; }
        public Prediction ConflictsWhenAfter { get; private set; }
        public int PredictedConflictValue { get; private set; }

        public ConstraintPrediction(Prediction conflictsWhenBefore, Prediction conflictsWhenTogether, Prediction conflictsWhenAfter, int predictedConflictValue)
        {
            ConflictsWhenBefore = conflictsWhenBefore;
            ConflictsWhenTogether = conflictsWhenTogether;
            ConflictsWhenAfter = conflictsWhenAfter;
            PredictedConflictValue = predictedConflictValue;
        }

        public enum Prediction
        {
            Conflict, NoConflict, Unknown
        }
    }
}
