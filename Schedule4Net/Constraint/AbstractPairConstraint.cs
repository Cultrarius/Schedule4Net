﻿using System;

namespace Schedule4Net.Constraint
{
    public abstract class AbstractPairConstraint<T> : ItemPairConstraint where T : ScheduledItem
    {
        public ConstraintDecision Check(ScheduledItem item1, ScheduledItem item2)
        {
            T it1 = item1 as T;
            T it2 = item2 as T;
            if (it1 == null || it2 == null)
            {
                throw new ArgumentNullException("Unable to cast provided items to desired type " + typeof(T));
            }
            return CheckConstraint(it1, it2);
        }

        protected abstract ConstraintDecision CheckConstraint(T item1, T item2);

        public virtual bool NeedsChecking(ItemToSchedule item1, ItemToSchedule item2)
        {
            return item1.GetType() == item2.GetType();
        }

        public virtual ConstraintPrediction PredictDecision(ItemToSchedule movedItem, ItemToSchedule fixItem)
        {
            return new ConstraintPrediction(ConstraintPrediction.Prediction.Unknown, ConstraintPrediction.Prediction.Unknown, ConstraintPrediction.Prediction.Unknown, 0);
        }
    }
}
