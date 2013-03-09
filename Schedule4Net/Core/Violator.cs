using System;
using System.Collections.Generic;
using Schedule4Net.Constraint;

namespace Schedule4Net.Core
{
    internal class Violator : IComparable<Violator>
    {
        public int HardViolationsValue { get; set; }
        public int SoftViolationsValue { get; set; }
        public ViolationsManager Manager { get; private set; }
        public ScheduledItem ScheduledItem { get; internal set; }

        protected internal Violator(ScheduledItem scheduledItem, ViolationsManager manager)
        {
            ScheduledItem = scheduledItem;
            Manager = manager;
            CheckSingleConstraints();
            GetPairConstraintDecisions();
        }

        protected internal Violator(ScheduledItem scheduledItem, int hardViolationsValue, int softViolationsValue, ViolationsManager manager)
        {
            Manager = manager;
            ScheduledItem = scheduledItem;
            HardViolationsValue = hardViolationsValue;
            SoftViolationsValue = softViolationsValue;
        }

        private void Aggregate(ConstraintDecision decision)
        {
            if (decision.Fulfilled) return;
            if (decision.HardConstraint)
            {
                HardViolationsValue += decision.ViolationValue;
            }
            else
            {
                SoftViolationsValue += decision.ViolationValue;
            }
        }

        private void GetPairConstraintDecisions()
        {
            ISet<ViolationsManager.ConstraintPartner> partners = Manager.ConstraintMap[ScheduledItem.ItemToSchedule];
            if (partners != null && partners.Count != 0)
            {
                CheckPartnerConstraints(partners);
            }
        }

        private void CheckPartnerConstraints(IEnumerable<ViolationsManager.ConstraintPartner> partners)
        {
            foreach (ViolationsManager.ConstraintPartner partner in partners)
            {
                ViolatorValues partnerValues = partner.ViolationsContainer.Values;
                HardViolationsValue += partnerValues.HardViolationsValue;
                SoftViolationsValue += partnerValues.SoftViolationsValue;
            }
        }

        private void CheckSingleConstraints()
        {
            foreach (SingleItemConstraint constraint in Manager.SingleConstraints)
            {
                Aggregate(constraint.Check(ScheduledItem));
            }
        }

        public override int GetHashCode()
        {
            return ScheduledItem.GetHashCode();
        }

        public override bool Equals(Object obj)
        {
            if (this == obj)
                return true;
            if (obj == null)
                return false;
            Violator other = obj as Violator;
            return other != null && ScheduledItem.ItemToSchedule.Id == other.ScheduledItem.ItemToSchedule.Id;
        }

        public int CompareTo(Violator o)
        {
            int result = (HardViolationsValue < o.HardViolationsValue ? -1 : (HardViolationsValue == o.HardViolationsValue ? 0 : 1));
            if (result == 0)
            {
                result = (SoftViolationsValue < o.SoftViolationsValue ? -1 : (SoftViolationsValue == o.SoftViolationsValue ? 0 : 1));
            }
            if (result == 0)
            {
                int summary = ScheduledItem.ItemToSchedule.DurationSummary;
                int otherSummary = o.ScheduledItem.ItemToSchedule.DurationSummary;
                result = (summary > otherSummary ? -1 : (summary == otherSummary ? 0 : 1));
            }
            if (result == 0)
            {
                int id = ScheduledItem.ItemToSchedule.Id;
                int otherId = o.ScheduledItem.ItemToSchedule.Id;
                result = (id < otherId ? -1 : (id == otherId ? 0 : 1));
            }
            return result;
        }
    }
}
