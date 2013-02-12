using System;
using System.Collections.Generic;
using System.Linq;
using Schedule4Net.Constraint;
using Schedule4Net.Core;
using Schedule4Net.Core.Exception;

namespace Schedule4Net
{
    public class HeuristicRepairScheduling
    {
        private SchedulePlan _plan;
        private readonly ViolationsManager _violationsManager;
        private readonly ConfigurationsManager _configurationsManager;
        private readonly List<IList<ScheduledItem>> _snapshots;
        public bool CachingResultPlan { get; private set; }
        
        /// <summary>
        /// the number of backsteps the scheduler had to take while trying to solve the scheduling problem.
        /// This property is only interesting for debug reasons, so do not bother with it.
        /// </summary>
        public int Backsteps { get; set; }

        /**
         * Creates a new instance of the scheduler using the constraints of the given {@link ViolationsManager}.
         * 
         * @param manager
         *            the manager holding the constraints used to create all future schedules
         */
        public HeuristicRepairScheduling(ViolationsManager manager)
        {
            CachingResultPlan = true;
            _violationsManager = manager;
            _configurationsManager = new ConfigurationsManager(_violationsManager);
            _snapshots = new List<IList<ScheduledItem>>();
        }

        /**
         * Creates a new instance of the scheduler using the given constraints to schedule items.
         * 
         * @param singleConstraints
         *            all the constraints that apply to single items
         * @param pairConstraints
         *            all the constraints that apply to a pair of items
         */
        public HeuristicRepairScheduling(IEnumerable<SingleItemConstraint> singleConstraints, IEnumerable<ItemPairConstraint> pairConstraints)
            : this(new ViolationsManager(singleConstraints, pairConstraints))
        {
        }
        

        /**
         * The main entry point for scheduling. This method will move all items provided as parameters as it sees fit. If some of the items must
         * be fixed and should not be moved then provide them as an additional collection.
         * 
         * @param itemsToSchedule
         *            all the items that shall be scheduled. These do not contain the fixed scheduled items.
         * @return
         */
        public SchedulePlan Schedule(IList<ItemToSchedule> itemsToSchedule)
        {
            return Schedule(itemsToSchedule, new List<ScheduledItem>());
        }

        /**
         * The main entry point for scheduling.
         * 
         * @param itemsToSchedule
         *            all the items that shall be scheduled. These do not contain the fixed scheduled items.
         * @param fixedItems
         *            the items in the scheduling plan, that must not be moved by the scheduler. For example, this might be tasks that have
         *            already been started but must be reflected in the schedule.
         * @return the plan created by scheduling the given items by using the constraints from the {@link ViolationsManager}.
         */
        public SchedulePlan Schedule(IList<ItemToSchedule> itemsToSchedule, IList<ScheduledItem> fixedItems)
        {
            Backsteps = 0;
            _snapshots.Clear();
            CreateStartPlan(itemsToSchedule, fixedItems);
            if (itemsToSchedule.Count == 0) return _plan;
            _violationsManager.Initialize(_plan);
            SatisfyConstraints();
            return _plan;
        }

        /**
         * This method is more or less the "core" of the scheduler. It moves the items violating one or more constraints around until all
         * constraints are satisfied.
         */
        private void SatisfyConstraints()
        {
            bool hardConstraintsSatisfied = false;
            Violator violator = _violationsManager.GetBiggestViolator(null);

            if (violator != null && violator.HardViolationsValue == 0)
            {
                hardConstraintsSatisfied = true;
                if (violator.SoftViolationsValue == 0)
                {
                    violator = null;
                }
            }

            while (violator != null)
            {
                _configurationsManager.ResetConfigurations(violator, _plan);

                if (_plan.CanBeMoved(violator.ScheduledItem))
                {
                    bool foundConfiguration = false;
                    foreach (int possibleStart in _plan.GetExistingStartValues())
                    {
                        if (foundConfiguration
                            && _plan.Makespan < (violator.ScheduledItem.ItemToSchedule.MaxDuration + possibleStart))
                        {
                            // all following start values would not be accepted over the current best one
                            break;
                        }
                        foundConfiguration |= _configurationsManager.AddConfiguration(violator, _plan, possibleStart);
                    }
                }

                bool wasPossible = _configurationsManager.ApplyBestConfiguration(_plan);

                if (!wasPossible)
                {
                    _configurationsManager.ApplyReferenceConfiguration(_plan);
                    Backsteps++;
                    violator = _violationsManager.GetBiggestViolator(violator);

                    if (violator == null && hardConstraintsSatisfied)
                    {
                        /*
                         * the hard constraints are satisfied and the soft constraints can not be refined any further without breaking something
                         */
                        break;
                    }
                    if (violator == null)
                    {
                        /*
                         * a suitable place could not be found, not even at the end of the plan. The reason could be that some constraints are
                         * violated - after moving the item to the end of the plan - even though they are not violated now. The plan is captured
                         * in a local optimum and has to be lifted out of it in order to proceed its search.
                         */
                        EscapeFromLocalOptimum();
                    }
                    else
                    {
                        continue;
                    }
                }

                _snapshots.Add(_plan.ScheduledItems);
                violator = _violationsManager.GetBiggestViolator(null);
                if (violator == null || (!hardConstraintsSatisfied && violator.HardViolationsValue == 0))
                {
                    hardConstraintsSatisfied = true;
                }
            }
        }

        /**
         * This method is called if the scheduler has at least one hard constraint violated, but cannot find any possible move to improve the
         * situation. Most of the time this is a local optimum created by a chain of dependent items that are scheduled in the wrong order.
         */
        private void EscapeFromLocalOptimum()
        {
            Violator violator = _violationsManager.GetBiggestViolator(null);
            _configurationsManager.ResetPlanConfigurations();
            _configurationsManager.AddPlanConfiguration(_plan);

            TryToMoveRequiredItems(violator);
            SchedulePlan bestPlan = _configurationsManager.GetBestPlanConfiguration();
            if (bestPlan == _plan)
            {
                TryToMoveRigth(violator);
                bestPlan = _configurationsManager.GetBestPlanConfiguration();
            }
            if (bestPlan == _plan)
            {
                TryToMoveLeft(violator);
                bestPlan = _configurationsManager.GetBestPlanConfiguration();
            }

            if (bestPlan == _plan)
            {
                throw new SchedulingException(
                    "Unable to esacape from local optimum in scheduling plan. The scheduling can not be completed! Plan: " + _plan);
            }

            _violationsManager.PlanHasBeenUpdated(_plan, bestPlan);
            _plan = bestPlan;

        }

        private void TryToMoveRequiredItems(Violator violator)
        {
            SchedulePlan newPlan = _plan.Clone() as SchedulePlan;
            if (newPlan == null) { throw new SchedulingException("Unable to copy schedule plan!"); }
            IDictionary<ItemToSchedule, DependencyNode> dependencyLevels = new Dictionary<ItemToSchedule, DependencyNode>();
            addToTree(violator.ScheduledItem.ItemToSchedule, dependencyLevels, newPlan, 0);

            C5.TreeSet<DependencyNode> dependencyTree = new C5.TreeSet<DependencyNode>();
            dependencyTree.AddAll(dependencyLevels.Values);

            foreach (DependencyNode dependencyNode in dependencyTree)
            {
                newPlan.Unschedule(dependencyNode.ScheduledItem);
            }

            foreach (DependencyNode dependencyNode in dependencyTree)
            {
                ViolatorValues bestValues = null;
                ScheduledItem bestItem = null;
                foreach (int possibleStart in newPlan.GetExistingStartValues())
                {
                    ScheduledItem newItem = new ScheduledItem(dependencyNode.ScheduledItem.ItemToSchedule, possibleStart);
                    ViolatorValues violatorValues = _violationsManager.CheckViolationsForItem(newItem, newPlan);
                    if (bestValues == null
                        || (violatorValues.HardViolationsValue < bestValues.HardViolationsValue)
                        || (violatorValues.HardViolationsValue == bestValues.HardViolationsValue && violatorValues.SoftViolationsValue < bestValues.SoftViolationsValue))
                    {
                        bestValues = violatorValues;
                        bestItem = newItem;
                    }
                }
                newPlan.Schedule(bestItem);
            }

            _configurationsManager.AddPlanConfiguration(newPlan);
        }

        private void addToTree(ItemToSchedule item, IDictionary<ItemToSchedule, DependencyNode> dependencyLevels, SchedulePlan newPlan, int level)
        {
            DependencyNode node;
            if (dependencyLevels.ContainsKey(item))
            {
                node = new DependencyNode(newPlan.GetScheduledItem(item), level);
                dependencyLevels.Add(item, node);
            }
            node = dependencyLevels[item];
            if (node.Level < level)
            {
                node.Level = level;
            }
            foreach (ScheduledItem scheduled in newPlan.GetDependentItems(item))
            {
                if (newPlan.CanBeMoved(scheduled))
                {
                    addToTree(scheduled.ItemToSchedule, dependencyLevels, newPlan, level + 1);
                }
            }
        }

        /**
         * This class is used to build the tree when trying to escape a local optimum.
         * 
         * @author Michael
         * 
         */
        private class DependencyNode : IComparable<DependencyNode>
        {
            internal ScheduledItem ScheduledItem { get; private set; }
            internal int Level { get; set; }

            public DependencyNode(ScheduledItem scheduledItem, int level)
            {
                ScheduledItem = scheduledItem;
                Level = level;
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
                DependencyNode other = obj as DependencyNode;
                return other != null && ScheduledItem.Equals(other.ScheduledItem);
            }

            public int CompareTo(DependencyNode o)
            {
                int result = (Level < o.Level ? -1 : (Level == o.Level ? 0 : 1));
                if (result == 0)
                {
                    result = (ScheduledItem.Start < o.ScheduledItem.Start ? -1 : (ScheduledItem.Start == o.ScheduledItem
                        .Start ? 0 : 1));
                }
                return result;
            }
        }

        private void TryToMoveLeft(Violator violator)
        {
            /*
             * shift the complete plan to the rigth and move the items to the left (before the start of the current plan)
             */
            SchedulePlan toStartPlan = _plan.Clone() as SchedulePlan;
            if (toStartPlan == null) { throw new SchedulingException("Unable to copy schedule plan!"); }
            toStartPlan.ShiftAll(_plan.Makespan);
            ISet<ScheduledItem> items = new HashSet<ScheduledItem>();
            items.Add(toStartPlan.GetScheduledItem(violator.ScheduledItem.ItemToSchedule));

            ShiftAndLock(items, new HashSet<ScheduledItem>(), toStartPlan, -_plan.Makespan);
            _configurationsManager.AddPlanConfiguration(toStartPlan);

        }

        private void TryToMoveRigth(Violator violator)
        {
            /*
             * move the items to the rigth (to the end of the current plan)
             */
            SchedulePlan toEndPlan = _plan.Clone() as SchedulePlan;
            ISet<ScheduledItem> items = new HashSet<ScheduledItem>();
            items.Add(violator.ScheduledItem);

            ShiftAndLock(items, new HashSet<ScheduledItem>(), toEndPlan, _plan.Makespan);
            _configurationsManager.AddPlanConfiguration(toEndPlan);

        }

        private void ShiftAndLock(ISet<ScheduledItem> items, ISet<ScheduledItem> lockedItems, SchedulePlan toEndPlan, int shiftValue)
        {

            // retrieve all items which items are violated rigth now by the items to shift
            ISet<ScheduledItem> violatedItems = new HashSet<ScheduledItem>();
            foreach (ScheduledItem itemToAdd in items.SelectMany(itemToShift => _violationsManager.GetHardViolatedItems(itemToShift, toEndPlan)))
            {
                violatedItems.Add(itemToAdd);
            }

            // shift the items that need it and lock them
            ISet<ScheduledItem> shiftedItems = new HashSet<ScheduledItem>();
            foreach (ScheduledItem itemToShift in items)
            {
                ScheduledItem item = toEndPlan.MoveScheduledItem(itemToShift.ItemToSchedule,
                                                                 itemToShift.Start + shiftValue);
                shiftedItems.Add(item);
                lockedItems.Add(item);
            }

            // check which items are violated after the shift
            ISet<ScheduledItem> newViolatedItems = new HashSet<ScheduledItem>();
            foreach (ScheduledItem itemToAdd in shiftedItems.SelectMany(shiftedItem => _violationsManager.GetHardViolatedItems(shiftedItem, toEndPlan)))
            {
                newViolatedItems.Add(itemToAdd);
            }

            // check if other, additional items are violated because of the shift
            foreach (ScheduledItem toRemove in violatedItems)
            {
                newViolatedItems.Remove(toRemove);
            }
            if (newViolatedItems.Count != 0)
            {

                // check if one of the newly violated items has already been shifted before and is locked now
                ISet<ScheduledItem> checkCollection = new HashSet<ScheduledItem>(newViolatedItems);
                checkCollection.IntersectWith(lockedItems);
                if (checkCollection.Count != 0)
                {
                    throw new SchedulingException(
                        "The current plan can not be scheduled because it most likely contains a circular constraint of some kind. Dumping variable assignments. "
                        + "lockedItems: " + lockedItems + ", newViolatedItems: " + newViolatedItems + ", toEndPlan: " + toEndPlan
                        + ", original plan: " + _plan);
                }

                // recursively shift the new violated items and lock them
                ShiftAndLock(newViolatedItems, lockedItems, toEndPlan, shiftValue);
            }
        }

        /**
         * Creates the start plan for the scheduler. This is a very important step, because the better the start plan, the faster will the
         * scheduling algorithm solve it. However, creating a good start plan is almost as hard as scheduling all of the items altogether.
         * 
         * @param itemsToSchedule
         *            all of the items that need to be scheduled and should be aligned by this method
         * @param fixedItems
         *            these items must be included in the plan, but must not be moved as they already have a fixed place
         */
        private void CreateStartPlan(IList<ItemToSchedule> itemsToSchedule, IList<ScheduledItem> fixedItems)
        {
            SchedulePlan oldPlan = CachingResultPlan ? _plan : null;
            _plan = new SchedulePlan();

            // @formatter:off
            /*
             * possibilities:
             * - place them all at 0 (all overlapping) 
             * - place them as they come to the current possible end (<---- currently implemented)
             * - sort them according to duration summary and place them to the current possible end (big to small) 
             * - as before, but inverse (small to big) 
             * - shuffle them and place them to the current possible end
             */
            // @formatter:on

            foreach (ScheduledItem fixedItem in fixedItems)
            {
                _plan.Schedule(fixedItem);
                _plan.FixateItem(fixedItem);
            }

            IDictionary<Lane, int> maximumValues = new Dictionary<Lane, int>();
            ISet<ItemToSchedule> scheduledFromOldPlan = new HashSet<ItemToSchedule>();
            // initialize the new plan from the old one - if one small changes are necessary then this will greatly speed up
            // the computation
            if (oldPlan != null && CachingResultPlan)
            {
                IDictionary<int, ItemToSchedule> newItemsMap = new Dictionary<int, ItemToSchedule>();
                foreach (ItemToSchedule item in itemsToSchedule)
                {
                    newItemsMap.Add(item.Id, item);
                }

                List<ScheduledItem> oldScheduledItems = oldPlan.ScheduledItems;
                foreach (ScheduledItem oldScheduledItem in oldScheduledItems)
                {
                    ItemToSchedule oldItem = oldScheduledItem.ItemToSchedule;
                    ItemToSchedule newItem = newItemsMap[oldItem.Id];
                    if (oldItem.Equals(newItem))
                    {
                        ScheduledItem scheduledItem = _plan.Add(newItem, oldScheduledItem.Start);
                        updateMaxLaneValues(maximumValues, scheduledItem);
                        scheduledFromOldPlan.Add(newItem);
                    }
                }
            }

            foreach (ItemToSchedule itemToSchedule in itemsToSchedule)
            {
                if (scheduledFromOldPlan.Contains(itemToSchedule))
                {
                    continue;
                }
                int start = getPossibleStart(maximumValues, itemToSchedule);
                ScheduledItem scheduledItem = _plan.Add(itemToSchedule, start);
                updateMaxLaneValues(maximumValues, scheduledItem);
            }

            // take a snapshot
            _snapshots.Add(_plan.ScheduledItems);
        }

        private void updateMaxLaneValues(IDictionary<Lane, int> maximumValues, ScheduledItem scheduledItem)
        {
            foreach (Lane lane in scheduledItem.ItemToSchedule.Lanes)
            {
                maximumValues.Add(lane, scheduledItem.Start + scheduledItem.ItemToSchedule.GetDuration(lane));
            }
        }

        private int getPossibleStart(IDictionary<Lane, int> maximumValues, ItemToSchedule itemToSchedule)
        {
            int start = 0;
            foreach (Lane lane in itemToSchedule.Lanes)
            {
                if (!maximumValues.ContainsKey(lane)) continue;
                int currentMaximum = maximumValues[lane];
                if (currentMaximum > start)
                {
                    start = currentMaximum;
                }
            }
            return start;
        }

        /**
         * After each successful movement operation the scheduler will take a snapshot of the current configuration of all the scheduled items.
         * This method returns a list of these snapshots in a chronological order. This is very helpful for debugging and to visualize the
         * workings of the scheduler.
         * 
         * @return a list of all the snapshots taken during the scheduling process
         */
        public IList<IList<ScheduledItem>> GetSnapshots()
        {
            return new List<IList<ScheduledItem>>(_snapshots);
        }

        /**
         * @return the number of backsteps the scheduler had to take while trying to solve the scheduling problem. This method is only
         *         interesting for debug reasons, so do not bother with it.
         */
        //public int getBacksteps()
        //{
        //    return Backsteps;
        //}

        /**
         * @return <code>true</code> if the scheduler reuses the previous result to start the calculations with, <code>false</code> otherwise
         */
        //public bool isCachingResultPlan()
        //{
        //    return CachingResultPlan;
        //}

        /**
         * This method controls if the scheduler should cache each result to determine the starting plan of the next scheduling run. By default,
         * this value is set to true. If each scheduling run contains vastly different items to schedule then this should be set to false.
         * 
         * @param cachingResultPlan
         *            <code>true</code> if the scheduler should reuse the previous result to start the calculations with, <code>false</code>
         *            otherwise
         */
        //public void setCachingResultPlan(bool cachingResultPlan)
        //{
        //    this.CachingResultPlan = cachingResultPlan;
        //}

        /**
         * This method clears the cached result plan so the next scheduling run will not be based on it. Please note that caching can be
         * disabled altogether with the {@code setCachingResultPlan} method.
         */
        public void ClearCachedResultPlan()
        {
            _plan = null;
        }
    }
}
