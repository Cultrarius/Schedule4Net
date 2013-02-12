using System;
using System.Collections.Generic;
using System.Linq;
using C5;
using Schedule4Net.Constraint;
using Schedule4Net.Core.Exception;

namespace Schedule4Net.Core
{
    /// <summary>
    /// This class is responsible for the management of all constraints and their violations by scheduled items.
    /// It tries to manage constraint violations in an efficient way and also handles updates of the <see cref="SchedulePlan"/>.
    /// </summary>
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

        /// <summary>
        /// The constraint map defines which items are connected to other items through one or more constraints.
        /// </summary>
        public System.Collections.Generic.IDictionary<ItemToSchedule, ISet<ConstraintPartner>> ConstraintMap
        {
            get { return new Dictionary<ItemToSchedule, ISet<ConstraintPartner>>(_constraintMap); }
        }

        /// <summary>
        /// The violationsTree is an ordered set of all the constraint violators (ordered by their violation value).
        /// It is similar to an ordered list, but it guarantees that an item can be contained at most once.
        /// In addition, it provides efficient log(n) operations to add and remove items.
        /// </summary>
        protected TreeSet<Violator> ViolationsTree
        {
            get { return _violationsTree; }
        }

        protected System.Collections.Generic.IDictionary<ItemToSchedule, Violator> ViolationsMapping
        {
            get { return _violationsMapping; }
        }

        internal readonly System.Collections.Generic.IList<SingleItemConstraint> _singleConstraints;
        internal readonly System.Collections.Generic.IList<ItemPairConstraint> _pairConstraints;
        internal readonly System.Collections.Generic.IDictionary<ItemToSchedule, ISet<ConstraintPartner>> _constraintMap;
        private readonly TreeSet<Violator> _violationsTree;
        private readonly System.Collections.Generic.IDictionary<ItemToSchedule, Violator> _violationsMapping;

        /// <summary>
        /// Creates a new instance of the manager that uses the given constraints to determine schedule violations.
        /// </summary>
        /// <param name="singleConstraints">All the constraints that apply to single items.</param>
        /// <param name="pairConstraints">All the constraints that apply to a pair of items.</param>
        public ViolationsManager(IEnumerable<SingleItemConstraint> singleConstraints, IEnumerable<ItemPairConstraint> pairConstraints)
        {
            _singleConstraints = new List<SingleItemConstraint>(singleConstraints);
            _pairConstraints = new List<ItemPairConstraint>(pairConstraints);
            _constraintMap = new Dictionary<ItemToSchedule, ISet<ConstraintPartner>>();
            _violationsTree = new TreeSet<Violator>();
            _violationsMapping = new Dictionary<ItemToSchedule, Violator>();
        }

        /// <summary>
        /// Initializes the manager with the given <see cref="SchedulePlan"/>. This is used to determine which items violate which constraints.
        /// </summary>
        /// <param name="plan">The plan containing scheduled items.</param>
        public void Initialize(SchedulePlan plan)
        {
            _constraintMap.Clear();
            ViolationsTree.Clear();
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
                ViolationsTree.Add(violator);
                ViolationsMapping.Add(itemToSchedule, violator);
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
                    System.Collections.Generic.IList<ItemPairConstraint> constraints = new List<ItemPairConstraint>(_pairConstraints.Count);
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
                    _constraintMap.Add(itemOuter, new System.Collections.Generic.HashSet<ConstraintPartner>());
                }
            }
        }

        private void AddPair(ItemToSchedule item1, ItemToSchedule item2, ViolationsContainer container, System.Collections.Generic.IList<ItemPairConstraint> constraints)
        {
            ISet<ConstraintPartner> pairs = _constraintMap.ContainsKey(item1) ? _constraintMap[item1] : new System.Collections.Generic.HashSet<ConstraintPartner>();
            pairs.Add(new ConstraintPartner(item2, container, constraints));
            _constraintMap.Add(item1, pairs);
        }

        /// <summary>
        /// This method checks if the provided item can be rescheduled in the provided <see cref="SchedulePlan"/>.
        /// This method is merely checking if such a rescheduling would be possible and what it would mean for the constriant violation values of the involved items.
        /// This method does not automatically reschedule the item if it is possible to do so.
        /// It returns an items that contains all information necessary to efficiently reschedule the item and update all constraint ciolations accordingly.
        /// </summary>
        /// <param name="newItem">The new scheduled item to be checked against the given plan.</param>
        /// <param name="plan">The plan the item should be rescheduled in.</param>
        /// <returns>A <see cref="ViolatorUpdate"/> object containing all the information about the rescheduling and the changes to the constraint violations</returns>
        /// <exception cref="ViolatorUpdateInvalid">Is thrown when the rescheduling is not possible because rescheduling would lead to bigger contraint violations</exception>
        public ViolatorUpdate TryViolatorUpdate(ScheduledItem newItem, SchedulePlan plan)
        {
            ItemToSchedule itemToSchedule = newItem.ItemToSchedule;
            Violator violator = ViolationsMapping[itemToSchedule];

            ViolatorValues newValues = new ViolatorValues();
            CalculateSingleConstraintValues(newItem, violator, newValues);

            //if (usingPrediction)

            //     {
            //    ConflictPrediction prediction = predictor.predictConflicts(newItem);
            //    checkUpdateValid(violator, newValues.HardViolationsValue + prediction.getDefinedHardConflictValue(), newValues.SoftViolationsValue);
            //}

            System.Collections.Generic.IList<PartnerUpdate> partnerUpdates;
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

        private void UpdatePartnerViolator(System.Collections.Generic.IList<PartnerUpdate> partnerUpdates, ConstraintPartner partner, ScheduledItem partnerItem, ViolatorValues newPartnerValues)
        {
            if (!ViolationsMapping.ContainsKey(partner.PartnerItem)) return;
            Violator partnerViolator = ViolationsMapping[partner.PartnerItem];
            ViolatorValues oldParterValues = partner.ViolationsContainer.Values;
            int newHardValue = partnerViolator.HardViolationsValue +
                               (newPartnerValues.HardViolationsValue - oldParterValues.HardViolationsValue);
            int newSoftValue = partnerViolator.SoftViolationsValue +
                               (newPartnerValues.SoftViolationsValue - oldParterValues.SoftViolationsValue);
            Violator updatedPartner = new Violator(partnerItem, newHardValue, newSoftValue, this);
            partnerUpdates.Add(new PartnerUpdate(partner, newPartnerValues, partnerViolator, updatedPartner));
        }

        private void CalculatePairConstraintValues(ScheduledItem newItem, Violator violator, ViolatorValues newValues,
            ConstraintPartner partner, ScheduledItem partnerItem, ViolatorValues newPartnerValues)
        {
            foreach (ItemPairConstraint constraint in partner.Constraints)
            {
                ConstraintDecision decision = constraint.Check(newItem, partnerItem);
                if (decision.Fulfilled) continue;
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

        public void UpdateViolator(ViolatorUpdate update)
        {
            Violator newViolator = update.UpdatedViolator;
            ItemToSchedule itemToSchedule = newViolator.ScheduledItem.ItemToSchedule;
            Violator oldViolator = ViolationsMapping[itemToSchedule];

            foreach (PartnerUpdate partnerUpdate in update.PartnerUpdates)
            {
                partnerUpdate.Partner.ViolationsContainer.UpdateValues(partnerUpdate.NewContainerValues);
                if (ViolationsTree.Remove(partnerUpdate.OldViolator))
                {
                    ViolationsTree.Add(partnerUpdate.UpdatedViolator);
                }

                ViolationsMapping.Add(partnerUpdate.UpdatedViolator.ScheduledItem.ItemToSchedule, partnerUpdate.UpdatedViolator);
            }

            // XXX maybe needed for fixed items?
            // if (!violationsTree.remove(oldViolator)) {
            // violationsTree.add(newViolator);
            // }

            ViolationsTree.Remove(oldViolator);
            ViolationsTree.Add(newViolator);

            ViolationsMapping.Add(itemToSchedule, newViolator);

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
                    oldParterValues = container.Values;
                }

                System.Collections.Generic.IList<ConstraintDecision> violations = new List<ConstraintDecision>(partner.Constraints.Count);
                foreach (ItemPairConstraint constraint in partner.Constraints)
                {
                    ConstraintDecision decision = constraint.Check(scheduledItem, partnerItem);
                    if (decision.Fulfilled) continue;
                    violations.Add(decision);
                    if (newValues == null) continue;
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
                container.UpdateValues(violations);

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
            if (!ViolationsMapping.ContainsKey(partner.PartnerItem)) return;

            Violator partnerViolator = ViolationsMapping[partner.PartnerItem];
            ViolatorValues newParterValues = container.Values;
            if (!ViolationsTree.Remove(partnerViolator)) { throw new SchedulingException("Fixed item?"); }
            int newHardValue = partnerViolator.HardViolationsValue + (newParterValues.HardViolationsValue - oldParterValues.HardViolationsValue);
            int newSoftValue = partnerViolator.SoftViolationsValue + (newParterValues.SoftViolationsValue - oldParterValues.SoftViolationsValue);
            Violator updatedViolator = new Violator(partnerItem, newHardValue, newSoftValue, this);
            ViolationsTree.Add(updatedViolator);
            ViolationsMapping.Add(partnerItem.ItemToSchedule, updatedViolator);
        }

        private static void CheckUpdateValid(Violator violator, int newHardViolationsValue, int newSoftViolationsValue)
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

        /// <summary>
        /// Returns the <see cref="Violator"/> with the biggest constraint violation value that is smaller than the value of upperBound.
        /// If upperBound is null then the biggest possible violator is returned.
        /// If there is no violator available then null is returned.
        /// </summary>
        /// <param name="upperBound">If null then the biggest possible violator is returned, otherwise a violator with a value smaller than upperBound is returned.</param>
        /// <returns>The biggest possible violator or null if there is none.</returns>
        /// <remarks>This method is guaranteed to run in O(log(n)).</remarks>
        public Violator GetBiggestViolator(Violator upperBound)
        {
            if (ViolationsTree.Count == 0) return null;
            if (upperBound == null) return ViolationsTree.Last();
            Violator returnValue;
            ViolationsTree.TryPredecessor(upperBound, out returnValue);
            return returnValue;
        }

        public System.Collections.Generic.ICollection<ScheduledItem> GetHardViolatedItems(ScheduledItem itemToCheck, SchedulePlan plan)
        {
            System.Collections.Generic.ICollection<ScheduledItem> violatedItems = new List<ScheduledItem>();
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
            public System.Collections.Generic.IList<ItemPairConstraint> Constraints { get; private set; }

            public ConstraintPartner(ItemToSchedule partnerItem, ViolationsContainer violationsContainer, System.Collections.Generic.IList<ItemPairConstraint> constraints)
            {
                PartnerItem = partnerItem;
                ViolationsContainer = violationsContainer;
                Constraints = constraints;
            }
        }

        public class ViolationsContainer
        {
            protected internal ViolatorValues Values { get; private set; }

            public ViolationsContainer(System.Collections.Generic.IList<ConstraintDecision> violations)
            {
                Values = new ViolatorValues();
                UpdateValues(violations);
            }

            public void UpdateValues(ViolatorValues newContainerValues)
            {
                Values.HardViolationsValue = newContainerValues.HardViolationsValue;
                Values.SoftViolationsValue = newContainerValues.SoftViolationsValue;
            }

            public void UpdateValues(System.Collections.Generic.IList<ConstraintDecision> violations)
            {
                Values.HardViolationsValue = 0;
                Values.SoftViolationsValue = 0;
                foreach (ConstraintDecision decision in violations)
                {
                    if (!decision.Fulfilled)
                    {
                        if (decision.HardConstraint)
                        {
                            Values.HardViolationsValue += decision.ViolationValue;
                        }
                        else
                        {
                            Values.SoftViolationsValue += decision.ViolationValue;
                        }
                    }
                }
            }
        }

        public ViolatorValues CheckViolationsForPlan(SchedulePlan plan)
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

        public ViolatorValues CheckViolationsForItem(ScheduledItem itemToCheck, SchedulePlan plan)
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

        public void PlanHasBeenUpdated(SchedulePlan oldPlan, SchedulePlan newPlan)
        {
            // TODO: improve the update
            ViolationsTree.Clear();
            InitializeViolationTree(newPlan);

            //predictor.planHasBeenUpdated(oldPlan, newPlan);
        }
    }
}
