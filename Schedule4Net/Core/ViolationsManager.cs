﻿using System;
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
    internal class ViolationsManager : IDisposable
    {
        internal readonly System.Collections.Generic.IList<SingleItemConstraint> SingleConstraints;
        internal readonly System.Collections.Generic.IList<ItemPairConstraint> PairConstraints;

        /// <summary>
        /// The constraint map defines which items are connected to other items through one or more constraints.
        /// </summary>
        internal readonly System.Collections.Generic.IDictionary<ItemToSchedule, ISet<ConstraintPartner>> ConstraintMap;

        /// <summary>
        /// The violationsTree is an ordered set of all the constraint violators (ordered by their violation value).
        /// It is similar to an ordered list, but it guarantees that an item can be contained at most once.
        /// In addition, it provides efficient log(n) operations to add and remove items.
        /// </summary>
        private readonly TreeSet<Violator> _violationsTree;
        private readonly System.Collections.Generic.IDictionary<ItemToSchedule, Violator> _violationsMapping;
        private Predictor _predictor;
        internal bool UsingPrediction = true;

        /// <summary>
        /// Creates a new instance of the manager that uses the given constraints to determine schedule violations.
        /// </summary>
        /// <param name="singleConstraints">All the constraints that apply to single items.</param>
        /// <param name="pairConstraints">All the constraints that apply to a pair of items.</param>
        public ViolationsManager(IEnumerable<SingleItemConstraint> singleConstraints, IEnumerable<ItemPairConstraint> pairConstraints)
        {
            SingleConstraints = new List<SingleItemConstraint>(singleConstraints);
            PairConstraints = new List<ItemPairConstraint>(pairConstraints);
            ConstraintMap = new Dictionary<ItemToSchedule, ISet<ConstraintPartner>>();
            _violationsTree = new TreeSet<Violator>();
            _violationsMapping = new Dictionary<ItemToSchedule, Violator>();
        }

        /// <summary>
        /// Initializes the manager with the given <see cref="SchedulePlan"/>. This is used to determine which items violate which constraints.
        /// </summary>
        /// <param name="plan">The plan containing scheduled items.</param>
        public void Initialize(SchedulePlan plan)
        {
            ConstraintMap.Clear();
            _violationsTree.Clear();
            UpdateConstraints();
            List<ItemToSchedule> items = plan.ScheduledItems.Select(item => item.ItemToSchedule).ToList();

            if (items.Count == 0) return;
            InitializeConstraintMap(items);
            InitializeViolationTree(plan);
            _predictor = new Predictor(plan, ConstraintMap);
        }

        private void UpdateConstraints()
        {
            foreach (UpdateableConstraint constraint in SingleConstraints.OfType<UpdateableConstraint>())
            {
                constraint.UpdateConstraint();
            }
            foreach (UpdateableConstraint constraint in PairConstraints.OfType<UpdateableConstraint>())
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
                if (ConstraintMap.ContainsKey(itemToSchedule))
                {
                    ISet<ConstraintPartner> pairs = ConstraintMap[itemToSchedule];
                    if (pairs.Count > 0)
                    {
                        if (!CheckPairConstraints(item, plan, pairs, false))
                        {
                            throw new SchedulingException("Unable to initialize violation tree!");
                        }
                    }
                }

                Violator violator = new Violator(item, this);
                _violationsTree.Add(violator);
                _violationsMapping.Remove(itemToSchedule);
                _violationsMapping.Add(itemToSchedule, violator);
            }
        }

        private void InitializeConstraintMap(System.Collections.Generic.IList<ItemToSchedule> itemsToSchedule)
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
                    System.Collections.Generic.IList<ItemPairConstraint> constraints = new List<ItemPairConstraint>(PairConstraints.Count);
                    foreach (ItemPairConstraint constraint in PairConstraints.Where(constraint => constraint.NeedsChecking(itemOuter, itemInner)))
                    {
                        constraints.Add(constraint);
                    }

                    if (constraints.Count <= 0) continue;
                    ViolationsContainer container = new ViolationsContainer(new List<ConstraintDecision>(PairConstraints.Count));
                    AddPair(itemOuter, itemInner, container, constraints);
                    AddPair(itemInner, itemOuter, container, constraints);
                }

                if (!ConstraintMap.ContainsKey(itemOuter))
                {
                    ConstraintMap.Add(itemOuter, new System.Collections.Generic.HashSet<ConstraintPartner>());
                }
            }
        }

        private void AddPair(ItemToSchedule item1, ItemToSchedule item2, ViolationsContainer container, System.Collections.Generic.IList<ItemPairConstraint> constraints)
        {
            ISet<ConstraintPartner> pairs = ConstraintMap.ContainsKey(item1) ? ConstraintMap[item1] : new System.Collections.Generic.HashSet<ConstraintPartner>();
            pairs.Add(new ConstraintPartner(item2, container, constraints));
            ConstraintMap.Remove(item1);
            ConstraintMap.Add(item1, pairs);
        }

        /// <summary>
        /// This method checks if the provided item can be rescheduled in the provided <see cref="SchedulePlan" />.
        /// This method is merely checking if such a rescheduling would be possible and what it would mean for the constriant violation values of the involved items.
        /// This method does not automatically reschedule the item if it is possible to do so.
        /// It returns an items that contains all information necessary to efficiently reschedule the item and update all constraint violations accordingly.
        /// </summary>
        /// <param name="newItem">The new scheduled item to be checked against the given plan.</param>
        /// <param name="plan">The plan the item should be rescheduled in.</param>
        /// <param name="update">If the method is successful then this is set to a <see cref="ViolatorUpdate" /> object containing all the information about the rescheduling and the changes to the constraint violations.</param>
        /// <returns>
        /// <c>True</c> if the operation was successful, <c>false</c> otherwise
        /// </returns>
        public bool TryViolatorUpdate(ScheduledItem newItem, SchedulePlan plan, out ViolatorUpdate update)
        {
            update = null;
            ItemToSchedule itemToSchedule = newItem.ItemToSchedule;
            Violator violator = _violationsMapping[itemToSchedule];

            ViolatorValues newValues = new ViolatorValues();
            if (!CalculateSingleConstraintValues(newItem, violator, newValues))
            {
                return false;
            }

            if (UsingPrediction && !(itemToSchedule is SwitchLaneItem))
            {
                Predictor.ConflictPrediction prediction = _predictor.PredictConflicts(newItem);
                if (!CheckUpdateValid(violator, newValues.HardViolationsValue + prediction.GetDefinedHardConflictValue(), newValues.SoftViolationsValue))
                {
                    return false;
                }
            }

            System.Collections.Generic.IList<PartnerUpdate> partnerUpdates = new List<PartnerUpdate>(0);
            ISet<ConstraintPartner> partners;
            
            if (ConstraintMap.TryGetValue(itemToSchedule, out partners))
            {
                partnerUpdates = new List<PartnerUpdate>(partners.Count);

                foreach (ConstraintPartner partner in partners)
                {
                    ScheduledItem partnerItem = plan.GetScheduledItem(partner.PartnerItem);
                    ViolatorValues newPartnerValues = new ViolatorValues();
                    if (!CalculatePairConstraintValues(newItem, violator, newValues, partner, partnerItem, newPartnerValues))
                    {
                        return false;
                    }
                    UpdatePartnerViolator(partnerUpdates, partner, partnerItem, newPartnerValues);
                }
            }

            Violator updatedViolator = new Violator(newItem, newValues.HardViolationsValue, newValues.SoftViolationsValue, this);
            update = new ViolatorUpdate(updatedViolator, partnerUpdates);
            return true;
        }

        private void UpdatePartnerViolator(System.Collections.Generic.IList<PartnerUpdate> partnerUpdates, ConstraintPartner partner, ScheduledItem partnerItem, ViolatorValues newPartnerValues)
        {
            if (!_violationsMapping.ContainsKey(partner.PartnerItem)) return;
            Violator partnerViolator = _violationsMapping[partner.PartnerItem];
            ViolatorValues oldParterValues = partner.ViolationsContainer.Values;
            int newHardValue = partnerViolator.HardViolationsValue +
                               (newPartnerValues.HardViolationsValue - oldParterValues.HardViolationsValue);
            int newSoftValue = partnerViolator.SoftViolationsValue +
                               (newPartnerValues.SoftViolationsValue - oldParterValues.SoftViolationsValue);
            Violator updatedPartner = new Violator(partnerItem, newHardValue, newSoftValue, this);
            partnerUpdates.Add(new PartnerUpdate(partner, newPartnerValues, partnerViolator, updatedPartner));
        }

        private static bool CalculatePairConstraintValues(ScheduledItem newItem, Violator violator, ViolatorValues newValues,
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

                if (!CheckUpdateValid(violator, newValues.HardViolationsValue, newValues.SoftViolationsValue))
                {
                    return false;
                }
            }
            return true;
        }

        private bool CalculateSingleConstraintValues(ScheduledItem newItem, Violator violator, ViolatorValues newValues)
        {
            foreach (SingleItemConstraint constraint in SingleConstraints)
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

                if (!CheckUpdateValid(violator, newValues.HardViolationsValue, newValues.SoftViolationsValue))
                {
                    return false;
                }
            }
            return true;
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
            Violator oldViolator = _violationsMapping[itemToSchedule];

            foreach (PartnerUpdate partnerUpdate in update.PartnerUpdates)
            {
                partnerUpdate.Partner.ViolationsContainer.UpdateValues(partnerUpdate.NewContainerValues);
                if (_violationsTree.Remove(partnerUpdate.OldViolator))
                {
                    _violationsTree.Add(partnerUpdate.UpdatedViolator);
                }
                ItemToSchedule key = partnerUpdate.UpdatedViolator.ScheduledItem.ItemToSchedule;
                _violationsMapping.Remove(key);
                _violationsMapping.Add(partnerUpdate.UpdatedViolator.ScheduledItem.ItemToSchedule, partnerUpdate.UpdatedViolator);
            }

            // XXX maybe needed for fixed items?
            // if (!violationsTree.remove(oldViolator)) {
            // violationsTree.add(newViolator);
            // }

            _violationsTree.Remove(oldViolator);
            _violationsTree.Add(newViolator);

            _violationsMapping.Remove(itemToSchedule);
            _violationsMapping.Add(itemToSchedule, newViolator);

            _predictor.ItemWasMoved(itemToSchedule);
        }

        private bool CheckPairConstraints(ScheduledItem scheduledItem, SchedulePlan plan, IEnumerable<ConstraintPartner> partners, bool updateConnected, ViolatorValues newValues = null, Violator violator = null)
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

                    if (!CheckUpdateValid(violator, newValues.HardViolationsValue, newValues.SoftViolationsValue))
                    {
                        return false;
                    }
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
            return true;
        }

        private void UpdatePartner(ConstraintPartner partner, ScheduledItem partnerItem, ViolationsContainer container,
            ViolatorValues oldParterValues)
        {
            if (!_violationsMapping.ContainsKey(partner.PartnerItem)) return;

            Violator partnerViolator = _violationsMapping[partner.PartnerItem];
            ViolatorValues newParterValues = container.Values;
            if (!_violationsTree.Remove(partnerViolator)) { throw new SchedulingException("Fixed item?"); }
            int newHardValue = partnerViolator.HardViolationsValue + (newParterValues.HardViolationsValue - oldParterValues.HardViolationsValue);
            int newSoftValue = partnerViolator.SoftViolationsValue + (newParterValues.SoftViolationsValue - oldParterValues.SoftViolationsValue);
            Violator updatedViolator = new Violator(partnerItem, newHardValue, newSoftValue, this);
            _violationsTree.Add(updatedViolator);
            _violationsMapping.Add(partnerItem.ItemToSchedule, updatedViolator);
        }

        private static bool CheckUpdateValid(Violator violator, int newHardViolationsValue, int newSoftViolationsValue)
        {
            return newHardViolationsValue <= violator.HardViolationsValue && (newHardViolationsValue != violator.HardViolationsValue || newSoftViolationsValue <= violator.SoftViolationsValue);
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
            if (_violationsTree.Count == 0) return null;
            if (upperBound == null) return _violationsTree.Last();
            Violator returnValue;
            _violationsTree.TryPredecessor(upperBound, out returnValue);
            return returnValue;
        }

        public System.Collections.Generic.ICollection<ScheduledItem> GetHardViolatedItems(ScheduledItem itemToCheck, SchedulePlan plan)
        {
            System.Collections.Generic.ICollection<ScheduledItem> violatedItems = new List<ScheduledItem>();
            foreach (ConstraintPartner constraintPartner in ConstraintMap[itemToCheck.ItemToSchedule])
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
                    if (decision.Fulfilled) continue;
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

        public ViolatorValues CheckViolationsForPlan(SchedulePlan plan)
        {
            ViolatorValues planValues = new ViolatorValues();

            // TODO: pair-violations are counted twice
            foreach (ScheduledItem itemToCheck in plan.ScheduledItems)
            {
                foreach (SingleItemConstraint constraint in SingleConstraints)
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

                ISet<ConstraintPartner> partners = ConstraintMap[itemToCheck.ItemToSchedule];
                foreach (ConstraintPartner partner in partners)
                {
                    ScheduledItem partnerItem = plan.GetScheduledItem(partner.PartnerItem);
                    foreach (ItemPairConstraint constraint in partner.Constraints)
                    {
                        ConstraintDecision decision = constraint.Check(itemToCheck, partnerItem);
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
                }
            }

            return planValues;
        }

        public ViolatorValues CheckViolationsForItem(ScheduledItem itemToCheck, SchedulePlan plan)
        {
            ViolatorValues values = new ViolatorValues();

            foreach (SingleItemConstraint constraint in SingleConstraints)
            {
                ConstraintDecision decision = constraint.Check(itemToCheck);
                if (decision.Fulfilled) continue;
                if (decision.HardConstraint)
                {
                    values.HardViolationsValue += decision.ViolationValue;
                }
                else
                {
                    values.SoftViolationsValue += decision.ViolationValue;
                }
            }

            ISet<ConstraintPartner> partners = ConstraintMap[itemToCheck.ItemToSchedule];
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
                    if (decision.Fulfilled) continue;
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
            return values;
        }

        public void PlanHasBeenUpdated(SchedulePlan oldPlan, SchedulePlan newPlan)
        {
            // TODO: improve the update
            _violationsTree.Clear();
            InitializeViolationTree(newPlan);

            _predictor.PlanHasBeenUpdated(oldPlan, newPlan);
        }

        public void Dispose()
        {
            _violationsTree.Dispose();
        }
    }
}
