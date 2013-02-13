namespace Schedule4Net.Constraint
{
    /// <summary>
    /// All constraints implementing this interface have the chance to update themselves before every scheduling run.
    /// For example, it could get the latest information from a database or set some internal clock.
    /// </summary>
    public interface UpdateableConstraint
    {
        /// <summary>
        /// This method is called before every scheduling run to give the constraint the possibility to update itself (e.g. getting information from a database).
        /// </summary>
        void UpdateConstraint();
    }
}
