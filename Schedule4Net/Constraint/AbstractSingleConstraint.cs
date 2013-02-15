using System;

namespace Schedule4Net.Constraint
{
    public abstract class AbstractSingleConstraint<T> : SingleItemConstraint where T : ItemToSchedule
    {
        public ConstraintDecision Check(ScheduledItem item)
        {
            T it = item as T;
            if (it == null)
            {
                throw new ArgumentNullException("Unable to cast provided item to desired type " + typeof(T));
            }
            return Check(it);
        }

        protected abstract ConstraintDecision Check(T item);
    }
}
