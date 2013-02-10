using System;
using System.Collections.Generic;
using System.Linq;
using Schedule4Net.Constraint;
using Schedule4Net.Core.Exception;

namespace Schedule4Net.Core
{
    public class ViolationsManager
    {
        public List<SingleItemConstraint> SingleConstraints
        {
            get { return new List<SingleItemConstraint>(_singleConstraints); }
        }

        public List<ItemPairConstraint> PairConstraints
        {
            get { return new List<ItemPairConstraint>(_pairConstraints); }
        }

        public IDictionary<ItemToSchedule, ISet<ConstraintPartner>> ConstraintMap
        {
            get { return new Dictionary<ItemToSchedule, ISet<ConstraintPartner>>(_constraintMap); }
        }

        private readonly IList<SingleItemConstraint> _singleConstraints;
        private readonly IList<ItemPairConstraint> _pairConstraints;

        /**
         * The constraint map defines which items are connected to other items through one or more constraints.
         */
        internal readonly IDictionary<ItemToSchedule, ISet<ConstraintPartner>> _constraintMap;

        /**
         * The violationsTree is an ordered set of all the constraint violators (ordered by their violation value). It is similar to an ordered
         * list, but it guarantees that an item can be contained at most once. In addition, it provides efficient log(n) operations to add and
         * remove items.
         */
        private readonly C5.TreeSet<Violator> violationsTree;
        private readonly IDictionary<ItemToSchedule, Violator> violationsMapping;

        //private bool usingPrediction = true;
        //private Predictor predictor;

        /**
         * Creates a new instance of the manager that uses the given constraints to determine schedule violations.
         * 
         * @param singleConstraints
         *            all the constraints that apply to single items
         * @param pairConstraints
         *            all the constraints that apply to a pair of items
         */
        public ViolationsManager(IEnumerable<SingleItemConstraint> singleConstraints, IEnumerable<ItemPairConstraint> pairConstraints)
        {
            _singleConstraints = new List<SingleItemConstraint>(singleConstraints);
            _pairConstraints = new List<ItemPairConstraint>(pairConstraints);
            _constraintMap = new Dictionary<ItemToSchedule, ISet<ConstraintPartner>>();
            violationsTree = new C5.TreeSet<Violator>();
            violationsMapping = new Dictionary<ItemToSchedule, Violator>();
        }

        /**
         * Initializes the manager with the given {@link SchedulePlan}. This is used to determine which items violate which constraints.
         * 
         * @param plan
         *            the plan containing scheduled items
         */
        public void Initialize(SchedulePlan plan)
        {
            _constraintMap.Clear();
            violationsTree.Clear();
            UpdateConstraints();
            List<ItemToSchedule> items = plan.ScheduledItems.Select(item => item.ItemToSchedule).ToList();

            if (items.Count == 0) return;
            InitializeConstraintMap(items);
            InitializeViolationTree(plan);
            //predictor = new Predictor(plan, _constraintMap);
        }

        private void UpdateConstraints()
        {
            foreach (UpdateableConstraint constraint in _singleConstraints.OfType<UpdateableConstraint>())
            {
                constraint.UpdateConstraint();
            }
            foreach (UpdateableConstraint constraint in _pairConstraints.OfType<UpdateableConstraint>())
            {
                constraint.UpdateConstraint();
            }
        }

        private void InitializeViolationTree(SchedulePlan plan)
        {
            ISet<ScheduledItem> fixedItems = plan.FixedItems;
            foreach (ScheduledItem item in plan.ScheduledItems)
            {
                if (fixedItems.Contains(item))
                {
                    continue;
                }
                ItemToSchedule itemToSchedule = item.ItemToSchedule;
                if (_constraintMap.ContainsKey(itemToSchedule))
                {
                    ISet<ConstraintPartner> pairs = _constraintMap[itemToSchedule];
                    if (pairs.Count > 0)
                    {
                        CheckPairConstraints(item, plan, pairs, false);
                    }
                }

                Violator violator = new Violator(item, this);
                violationsTree.Add(violator);
                violationsMapping.Add(itemToSchedule, violator);
            }
        }

        private void InitializeConstraintMap(List<ItemToSchedule> itemsToSchedule)
        {
            for (int outer = 0; outer < itemsToSchedule.Count; outer++)
            {
                ItemToSchedule itemOuter = itemsToSchedule[outer];
                for (int inner = itemsToSchedule.Count - 1; inner > 0; inner--)
                {
                    ItemToSchedule itemInner = itemsToSchedule[inner];
                    if (itemOuter == itemInner)
                    {
                        break;
                    }
                    IList<ItemPairConstraint> constraints = new List<ItemPairConstraint>(_pairConstraints.Count);
                    foreach (ItemPairConstraint constraint in _pairConstraints.Where(constraint => constraint.NeedsChecking(itemOuter, itemInner)))
                    {
                        constraints.Add(constraint);
                    }

                    if (constraints.Count <= 0) continue;
                    ViolationsContainer container = new ViolationsContainer(new List<ConstraintDecision>(_pairConstraints.Count));
                    AddPair(itemOuter, itemInner, container, constraints);
                    AddPair(itemInner, itemOuter, container, constraints);
                }

                if (!_constraintMap.ContainsKey(itemOuter))
                {
                    _constraintMap.Add(itemOuter, new HashSet<ConstraintPartner>());
                }
            }
        }

        private void AddPair(ItemToSchedule item1, ItemToSchedule item2, ViolationsContainer container, IList<ItemPairConstraint> constraints)
        {
            ISet<ConstraintPartner> pairs = _constraintMap.ContainsKey(item1) ? _constraintMap[item1] : new HashSet<ConstraintPartner>();
            pairs.Add(new ConstraintPartner(item2, container, constraints));
            _constraintMap.Add(item1, pairs);
        }

        /**
         * This method checks if the item provided with {@code newItem} can be rescheduled in the {@link SchedulePlan} {@code plan}. This method
         * is merely checking if such a rescheduling would be possible and what it would mean for the constriant violation values of the
         * involved items. This method does not automatically reschedule the item if it is possible to do so. It returns an items that contains
         * all information necessary to efficiently reschedule the item and update all constraint ciolations accordingly.
         * 
         * @param newItem
         *            the new scheduled item to be checked against the given plan
         * @param plan
         *            the plan the item should be rescheduled in
         * @return a {@link ViolatorUpdate} object containing all the information about the rescheduling and the changes to the constraint
         *         violations
         * @throws ViolatorUpdateInvalid
         *             is thrown when the rescheduling is not possible because rescheduling would lead to bigger contraint violations
         */
        public ViolatorUpdate tryViolatorUpdate(ScheduledItem newItem, SchedulePlan plan)
        {
            ItemToSchedule itemToSchedule = newItem.ItemToSchedule;
            Violator violator = violationsMapping[itemToSchedule];

            ViolatorValues newValues = new ViolatorValues();
            CalculateSingleConstraintValues(newItem, violator, newValues);

            //if (usingPrediction)

            //     {
            //    ConflictPrediction prediction = predictor.predictConflicts(newItem);
            //    checkUpdateValid(violator, newValues.HardViolationsValue + prediction.getDefinedHardConflictValue(), newValues.SoftViolationsValue);
            //}

            IList<PartnerUpdate> partnerUpdates;
            if (_constraintMap.ContainsKey(itemToSchedule))
            {
                ISet<ConstraintPartner> partners = _constraintMap[itemToSchedule];
                partnerUpdates = new List<PartnerUpdate>(partners.Count);

                foreach (ConstraintPartner partner in partners)
                {
                    ScheduledItem partnerItem = plan.GetScheduledItem(partner.PartnerItem);
                    ViolatorValues newPartnerValues = new ViolatorValues();
                    CalculatePairConstraintValues(newItem, violator, newValues, partner, partnerItem, newPartnerValues);
                    UpdatePartnerViolator(partnerUpdates, partner, partnerItem, newPartnerValues);
                }
            }
            else
            {
                partnerUpdates = new List<PartnerUpdate>(0);
            }

            Violator updatedViolator = new Violator(newItem, newValues.HardViolationsValue, newValues.SoftViolationsValue, this);
            return new ViolatorUpdate(updatedViolator, partnerUpdates);
        }

        private void UpdatePartnerViolator(IList<PartnerUpdate> partnerUpdates, ConstraintPartner partner, ScheduledItem partnerItem, ViolatorValues newPartnerValues)
        {
            if (violationsMapping.ContainsKey(partner.PartnerItem))
            {
                Violator partnerViolator = violationsMapping[partner.PartnerItem];
                ViolatorValues oldParterValues = partner.ViolationsContainer.values;
                int newHardValue = partnerViolator.HardViolationsValue +
                                   (newPartnerValues.HardViolationsValue - oldParterValues.HardViolationsValue);
                int newSoftValue = partnerViolator.SoftViolationsValue +
                                   (newPartnerValues.SoftViolationsValue - oldParterValues.SoftViolationsValue);
                Violator updatedPartner = new Violator(partnerItem, newHardValue, newSoftValue, this);
                partnerUpdates.Add(new PartnerUpdate(partner, newPartnerValues, partnerViolator, updatedPartner));
            }
        }

        private void CalculatePairConstraintValues(ScheduledItem newItem, Violator violator, ViolatorValues newValues,
            ConstraintPartner partner, ScheduledItem partnerItem, ViolatorValues newPartnerValues)
        {
            foreach (ItemPairConstraint constraint in partner.Constraints)
            {
                ConstraintDecision decision = constraint.Check(newItem, partnerItem);
                if (!decision.Fulfilled)
                {
                    if (decision.HardConstraint)
                    {
                        newValues.HardViolationsValue += decision.ViolationValue;
                        newPartnerValues.HardViolationsValue += decision.ViolationValue;
                    }
                    else
                    {
                        newValues.SoftViolationsValue += decision.ViolationValue;
                        newPartnerValues.SoftViolationsValue += decision.ViolationValue;
                    }

                    CheckUpdateValid(violator, newValues.HardViolationsValue, newValues.SoftViolationsValue);
                }
            }
        }

        private void CalculateSingleConstraintValues(ScheduledItem newItem, Violator violator, ViolatorValues newValues)
        {
            foreach (SingleItemConstraint constraint in _singleConstraints)
            {
                ConstraintDecision decision = constraint.Check(newItem);
                if (!decision.Fulfilled)
                {
                    if (decision.HardConstraint)
                    {
                        newValues.HardViolationsValue += decision.ViolationValue;
                    }
                    else
                    {
                        newValues.SoftViolationsValue += decision.ViolationValue;
                    }
                }

                CheckUpdateValid(violator, newValues.HardViolationsValue, newValues.SoftViolationsValue);
            }
        }

        public class PartnerUpdate
        {
            public ConstraintPartner Partner { get; private set; }
            public ViolatorValues NewContainerValues { get; private set; }
            public Violator OldViolator { get; private set; }
            public Violator UpdatedViolator { get; private set; }

            public PartnerUpdate(ConstraintPartner partner, ViolatorValues newContainerValues, Violator oldViolator, Violator updatedViolator)
            {
                Partner = partner;
                NewContainerValues = newContainerValues;
                OldViolator = oldViolator;
                UpdatedViolator = updatedViolator;
            }

        }

        public void updateViolator(ViolatorUpdate update)
        {
            Violator newViolator = update.UpdatedViolator;
            ItemToSchedule itemToSchedule = newViolator.ScheduledItem.ItemToSchedule;
            Violator oldViolator = violationsMapping[itemToSchedule];

            foreach (PartnerUpdate partnerUpdate in update.PartnerUpdates)
            {
                partnerUpdate.Partner.ViolationsContainer.updateValues(partnerUpdate.NewContainerValues);
                if (violationsTree.Remove(partnerUpdate.OldViolator))
                {
                    violationsTree.Add(partnerUpdate.UpdatedViolator);
                }

                violationsMapping.Add(partnerUpdate.UpdatedViolator.ScheduledItem.ItemToSchedule, partnerUpdate.UpdatedViolator);
            }

            // XXX maybe needed for fixed items?
            // if (!violationsTree.remove(oldViolator)) {
            // violationsTree.add(newViolator);
            // }

            violationsTree.Remove(oldViolator);
            violationsTree.Add(newViolator);

            violationsMapping.Add(itemToSchedule, newViolator);

            //predictor.itemWasMoved(itemToSchedule);
        }

        private void CheckPairConstraints(ScheduledItem scheduledItem, SchedulePlan plan, IEnumerable<ConstraintPartner> partners,
            bool updateConnected, ViolatorValues newValues, Violator violator)
        {
            foreach (ConstraintPartner partner in partners)
            {
                ScheduledItem partnerItem = plan.GetScheduledItem(partner.PartnerItem);
                ViolationsContainer container = partner.ViolationsContainer;

                ViolatorValues oldParterValues = null;
                if (updateConnected)
                {
                    oldParterValues = container.values;
                }

                IList<ConstraintDecision> violations = new List<ConstraintDecision>(partner.Constraints.Count);
                foreach (ItemPairConstraint constraint in partner.Constraints)
                {
                    ConstraintDecision decision = constraint.Check(scheduledItem, partnerItem);
                    if (!decision.Fulfilled)
                    {
                        violations.Add(decision);
                        if (newValues != null)
                        {
                            if (decision.HardConstraint)
                            {
                                newValues.HardViolationsValue += decision.ViolationValue;
                            }
                            else
                            {
                                newValues.SoftViolationsValue += decision.ViolationValue;
                            }

                            CheckUpdateValid(violator, newValues.HardViolationsValue, newValues.SoftViolationsValue);
                        }
                    }
                }
                container.updateValues(violations);

                if (updateConnected)
                {
                    /*
                     * update the violations tree for the partner node
                     */
                    UpdatePartner(partner, partnerItem, container, oldParterValues);
                }
            }
        }

        private void UpdatePartner(ConstraintPartner partner, ScheduledItem partnerItem, ViolationsContainer container,
            ViolatorValues oldParterValues)
        {
            if (!violationsMapping.ContainsKey(partner.PartnerItem)) return;

            Violator partnerViolator = violationsMapping[partner.PartnerItem];
            ViolatorValues newParterValues = container.values;
            if (!violationsTree.Remove(partnerViolator)) { throw new SchedulingException("Fixed item?"); }
            int newHardValue = partnerViolator.HardViolationsValue + (newParterValues.HardViolationsValue - oldParterValues.HardViolationsValue);
            int newSoftValue = partnerViolator.SoftViolationsValue + (newParterValues.SoftViolationsValue - oldParterValues.SoftViolationsValue);
            Violator updatedViolator = new Violator(partnerItem, newHardValue, newSoftValue, this);
            violationsTree.Add(updatedViolator);
            violationsMapping.Add(partnerItem.ItemToSchedule, updatedViolator);
        }

        private void CheckUpdateValid(Violator violator, int newHardViolationsValue, int newSoftViolationsValue)
        {
            if (newHardViolationsValue > violator.HardViolationsValue
                || (newHardViolationsValue == violator.HardViolationsValue && newSoftViolationsValue > violator.SoftViolationsValue)) { throw new ViolatorUpdateInvalid(); }
        }

        private void CheckPairConstraints(ScheduledItem scheduledItem, SchedulePlan plan, IEnumerable<ConstraintPartner> partners, bool updateConnected)
        {
            try
            {
                CheckPairConstraints(scheduledItem, plan, partners, updateConnected, null, null);
            }
            catch (ViolatorUpdateInvalid e)
            {
                // XXX this should never happen!!
                throw new ApplicationException("Internal program error", e);
            }
        }

        /**
         * Returns the {@link Violator} with the biggest constraint violation value that is smaller than the value of {@code upperBound}. If
         * {@code upperBound} is null then the biggest possible violator is returned. If there is no violator available then {@code null} is
         * returned. This method is guaranteed to run in O(log(n)).
         * 
         * @param upperBound
         *            If {@code null} then the biggest possible violator is returned, otherwise a violator with a value smaller than
         *            {@code upperBound} is returned.
         * @return the biggest possible violator or {@code null} if there is none.
         */
        public Violator getBiggestViolator(Violator upperBound)
        {
            if (violationsTree.Count == 0) return null;
            if (upperBound == null) return violationsTree.Last();
            Violator returnValue;
            violationsTree.TryPredecessor(upperBound, out returnValue);
            return returnValue;
        }

        public ICollection<ScheduledItem> getHardViolatedItems(ScheduledItem itemToCheck, SchedulePlan plan)
        {
            ICollection<ScheduledItem> violatedItems = new List<ScheduledItem>();
            foreach (ConstraintPartner constraintPartner in _constraintMap[itemToCheck.ItemToSchedule])
            {
                ScheduledItem constraintItem = plan.GetScheduledItem(constraintPartner.PartnerItem);
                foreach (ItemPairConstraint constraint in constraintPartner.Constraints)
                {
                    ConstraintDecision decision = constraint.Check(itemToCheck, constraintItem);
                    if (!decision.Fulfilled && decision.HardConstraint)
                    {
                        violatedItems.Add(constraintItem);
                    }
                }
            }
            return violatedItems;
        }

        public class ConstraintPartner
        {
            public ViolationsContainer ViolationsContainer { get; private set; }
            public ItemToSchedule PartnerItem { get; private set; }
            public IList<ItemPairConstraint> Constraints { get; private set; }

            public ConstraintPartner(ItemToSchedule partnerItem, ViolationsContainer violationsContainer, IList<ItemPairConstraint> constraints)
            {
                PartnerItem = partnerItem;
                ViolationsContainer = violationsContainer;
                Constraints = constraints;
            }
        }

        public class ViolationsContainer
        {
            protected internal readonly ViolatorValues values;

            public ViolationsContainer(IList<ConstraintDecision> violations)
            {
                values = new ViolatorValues();
                updateValues(violations);
            }

            public void updateValues(ViolatorValues newContainerValues)
            {
                values.HardViolationsValue = newContainerValues.HardViolationsValue;
                values.SoftViolationsValue = newContainerValues.SoftViolationsValue;
            }

            public void updateValues(IList<ConstraintDecision> violations)
            {
                values.HardViolationsValue = 0;
                values.SoftViolationsValue = 0;
                foreach (ConstraintDecision decision in violations)
                {
                    if (!decision.Fulfilled)
                    {
                        if (decision.HardConstraint)
                        {
                            values.HardViolationsValue += decision.ViolationValue;
                        }
                        else
                        {
                            values.SoftViolationsValue += decision.ViolationValue;
                        }
                    }
                }
            }
        }

        public ViolatorValues checkViolationsForPlan(SchedulePlan plan)
        {
            ViolatorValues planValues = new ViolatorValues();

            // TODO: pair-violations are counted twice
            foreach (ScheduledItem itemToCheck in plan.ScheduledItems)
            {
                foreach (SingleItemConstraint constraint in _singleConstraints)
                {
                    ConstraintDecision decision = constraint.Check(itemToCheck);
                    if (decision.Fulfilled) continue;
                    if (decision.HardConstraint)
                    {
                        planValues.HardViolationsValue += decision.ViolationValue;
                    }
                    else
                    {
                        planValues.SoftViolationsValue += decision.ViolationValue;
                    }
                }

                ISet<ConstraintPartner> partners = _constraintMap[itemToCheck.ItemToSchedule];
                foreach (ConstraintPartner partner in partners)
                {
                    ScheduledItem partnerItem = plan.GetScheduledItem(partner.PartnerItem);
                    foreach (ItemPairConstraint constraint in partner.Constraints)
                    {
                        ConstraintDecision decision = constraint.Check(itemToCheck, partnerItem);
                        if (!decision.Fulfilled)
                        {
                            if (decision.HardConstraint)
                            {
                                planValues.HardViolationsValue += decision.ViolationValue;
                            }
                            else
                            {
                                planValues.SoftViolationsValue += decision.ViolationValue;
                            }
                        }
                    }
                }
            }

            return planValues;
        }

        public ViolatorValues checkViolationsForItem(ScheduledItem itemToCheck, SchedulePlan plan)
        {
            ViolatorValues values = new ViolatorValues();

            foreach (SingleItemConstraint constraint in _singleConstraints)
            {
                ConstraintDecision decision = constraint.Check(itemToCheck);
                if (!decision.Fulfilled)
                {
                    if (decision.HardConstraint)
                    {
                        values.HardViolationsValue += decision.ViolationValue;
                    }
                    else
                    {
                        values.SoftViolationsValue += decision.ViolationValue;
                    }
                }
            }

            ISet<ConstraintPartner> partners = _constraintMap[itemToCheck.ItemToSchedule];
            foreach (ConstraintPartner partner in partners)
            {
                ScheduledItem partnerItem = plan.GetScheduledItem(partner.PartnerItem);
                if (partnerItem == null)
                {
                    // the partnerItem can be null if it has been removed from the plan
                    continue;
                }
                foreach (ItemPairConstraint constraint in partner.Constraints)
                {
                    ConstraintDecision decision = constraint.Check(itemToCheck, partnerItem);
                    if (!decision.Fulfilled)
                    {
                        if (decision.HardConstraint)
                        {
                            values.HardViolationsValue += decision.ViolationValue;
                        }
                        else
                        {
                            values.SoftViolationsValue += decision.ViolationValue;
                        }
                    }
                }
            }
            return values;
        }

        public void planHasBeenUpdated(SchedulePlan oldPlan, SchedulePlan newPlan)
        {
            // TODO: improve the update
            violationsTree.Clear();
            InitializeViolationTree(newPlan);

            //predictor.planHasBeenUpdated(oldPlan, newPlan);
        }
    }
}
