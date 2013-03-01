using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Schedule4Net.Constraint;
using Schedule4Net.Constraint.Impl;
using Schedule4Net.Core;
using Schedule4Net.Core.Exception;
using System.Threading.Tasks;

namespace Schedule4Net
{
    /// <summary>
    /// This class implements a scheduling algorithm to quickly solve a constrained scheduling problem.
    /// This does so by using a heuristic approach as described in the following paper: <a href="http://cds.cern.ch/record/1463647">cds.cern.ch</a>
    /// Basically, it tries to move the scheduled objects around until all the constraints are fulfilled.
    /// It is not guaranteed to find a solution if one exists, nor is it guaranteed to do so fast.
    /// But for most cases, this algorithm performs a lot better than classical search algorithms.
    /// </summary>
    public class HeuristicRepairScheduling
    {
        private SchedulePlan _plan;
        private readonly ViolationsManager _violationsManager;
        private readonly ConfigurationsManager _configurationsManager;
        private readonly List<IList<ScheduledItem>> _snapshots;
        private ConcurrentQueue<SchedulePlan> _planQueue;

        /// <summary>
        /// After each successful movement operation the scheduler will take a snapshot of the current configuration of all the scheduled items.
        /// This property contains a list of these snapshots in a chronological order. This is very helpful for debugging and to visualize the workings of the scheduler.
        /// </summary>
        public IList<IList<ScheduledItem>> Snapshots { get { return new List<IList<ScheduledItem>>(_snapshots); } }

        /// <summary>
        /// Is true if the scheduler reuses the previous result to start the calculations with, false otherwise
        /// </summary>
        public bool CachingResultPlan { get; set; }

        /// <summary>
        /// the number of backsteps the scheduler had to take while trying to solve the scheduling problem.
        /// This property is only interesting for debug reasons, so do not bother with it.
        /// </summary>
        public int Backsteps { get; private set; }

        /// <summary>
        /// If <c>true</c> then the scheduler will try to schedule independent clusters of <see cref="ItemToSchedule"/> separately from one another.
        /// The constraints used by the scheduler _must_ be thread-safe if this feature is used.
        /// Also note that the result may vary from non-parallel scheduling since the input-order of items is not preserved.
        /// </summary>
        public bool ParllelScheduling { get; set; }

        /// <summary>
        /// Creates a new instance of the scheduler using the constraints of the given <see cref="ViolationsManager"/>.
        /// </summary>
        /// <param name="manager">The manager holding the constraints used to create all future schedules.</param>
        internal HeuristicRepairScheduling(ViolationsManager manager)
        {
            CachingResultPlan = true;
            _violationsManager = manager;
            _configurationsManager = new ConfigurationsManager(_violationsManager);
            _snapshots = new List<IList<ScheduledItem>>();
        }

        /// <summary>
        /// Creates a new instance of the scheduler using the given constraints to schedule items.
        /// </summary>
        /// <param name="singleConstraints">All the constraints that apply to single items.</param>
        /// <param name="pairConstraints">All the constraints that apply to a pair of items.</param>
        public HeuristicRepairScheduling(IEnumerable<SingleItemConstraint> singleConstraints, IEnumerable<ItemPairConstraint> pairConstraints)
            : this(new ViolationsManager(singleConstraints, pairConstraints))
        {
        }

        /// <summary>
        /// Creates a new instance of the scheduler using the following standard constraints:
        /// - StartNowConstraint: scheduled items start as soon as possible
        /// - NoOverlappingConstraint: the times of scheduled items must not overlap on a single lane
        /// - DependenciesConstraint: checks if any <see cref="ItemToSchedule"/> is dependent on other items
        /// </summary>
        public HeuristicRepairScheduling()
            : this(new ViolationsManager(new List<SingleItemConstraint>
                {
                    new StartNowConstraint()
                }, new List<ItemPairConstraint>
                {
                    new NoOverlappingConstraint(),
                    new DependenciesConstraint()
                }))
        {
        }


        /// <summary>
        /// The main entry point for scheduling. This method will move all items provided as parameters as it sees fit to create a schedule.
        /// If some of the items must be fixed and should not be moved then provide them as an additional collection.
        /// </summary>
        /// <param name="itemsToSchedule">All the items that shall be scheduled. These do not contain the fixed scheduled items.</param>
        /// <returns>The plan created by scheduling the given items</returns>
        public SchedulePlan Schedule(IList<ItemToSchedule> itemsToSchedule)
        {
            return Schedule(itemsToSchedule, new List<ScheduledItem>());
        }

        /// <summary>
        /// The main entry point for scheduling. This method will move the items provided as parameters as it sees fit to create a schedule.
        /// </summary>
        /// <param name="itemsToSchedule">All the items that shall be scheduled. These do not contain the fixed scheduled items.</param>
        /// <param name="fixedItems">The items in the scheduling plan, that must not be moved by the scheduler. For example, this might be tasks that have already been started but must be reflected in the schedule.</param>
        /// <returns>The plan created by scheduling the given items</returns>
        public SchedulePlan Schedule(IList<ItemToSchedule> itemsToSchedule, IList<ScheduledItem> fixedItems)
        {
            Backsteps = 0;
            _snapshots.Clear();
            CreateStartPlan(itemsToSchedule, fixedItems);
            if (itemsToSchedule.Count == 0) return _plan;
            _violationsManager.Initialize(_plan);
            if (ParllelScheduling)
            {
                SchedulePlanInParallel();
            }
            else
            {
                SatisfyConstraints();
            }
            return _plan;
        }

        private void SchedulePlanInParallel()
        {
            ISet<ISet<ItemToSchedule>> clusters = FindClusters();
            if (clusters.Count == 1)
            {
                // they are all connected anyway, so no need to make unnecessary overhead; just schedule them sequentially
                SatisfyConstraints();
            }
            else
            {
                _planQueue = new ConcurrentQueue<SchedulePlan>();
                try
                {
                    Parallel.ForEach(clusters, ScheduleCluster);
                }
                catch (AggregateException e)
                {
                    throw new SchedulingException("An Exception occured during the parallel scheduling", e);
                }
                MergeResultPlans();
            }
        }

        private void MergeResultPlans()
        {
            _plan = new SchedulePlan();
            SchedulePlan plan;
            while (_planQueue.TryDequeue(out plan))
            {
                foreach (ScheduledItem scheduledItem in plan.ScheduledItems)
                {
                    _plan.Schedule(scheduledItem);
                }
            }
        }

        private void ScheduleCluster(ISet<ItemToSchedule> cluster)
        {
            var scheduler = new HeuristicRepairScheduling(_violationsManager.SingleConstraints, _violationsManager.PairConstraints)
                {
                    CachingResultPlan = CachingResultPlan,
                    ParllelScheduling = false,
                    _plan = _plan // set the plan in case of caching
                };
            var fixedCluster = new List<ScheduledItem>();
            foreach (ScheduledItem fixedItem in _plan.FixedItems)
            {
                if (!cluster.Contains(fixedItem.ItemToSchedule)) continue;
                cluster.Remove(fixedItem.ItemToSchedule);
                fixedCluster.Add(fixedItem);
            }
            _planQueue.Enqueue(scheduler.Schedule(new List<ItemToSchedule>(cluster), fixedCluster));
        }

        private ISet<ISet<ItemToSchedule>> FindClusters()
        {
            ISet<ISet<ItemToSchedule>> clusters = new HashSet<ISet<ItemToSchedule>>();
            foreach (ItemToSchedule item in _violationsManager.ConstraintMap.Keys)
            {
                if (IsAlreadyInCluster(item, clusters)) continue;
                ISet<ItemToSchedule> cluster = new HashSet<ItemToSchedule>();
                AddToCluster(cluster, item);
                if (!clusters.Add(cluster))
                {
                    throw new SchedulingException("Unable to add cluster! " + cluster);
                }
            }
            return clusters;
        }

        private void AddToCluster(ISet<ItemToSchedule> cluster, ItemToSchedule item)
        {
            cluster.Add(item);
            foreach (ViolationsManager.ConstraintPartner partner in _violationsManager.ConstraintMap[item])
            {
                if (cluster.Contains(partner.PartnerItem)) continue;
                AddToCluster(cluster, partner.PartnerItem);
            }
        }

        private static bool IsAlreadyInCluster(ItemToSchedule item, IEnumerable<ISet<ItemToSchedule>> clusters)
        {
            return clusters.Any(cluster => cluster.Contains(item));
        }

        /// <summary>
        /// This method is more or less the "core" of the scheduler. It moves the items violating one or more constraints around until all constraints are satisfied.
        /// </summary>
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
                    foreach (int possibleStart in _plan.StartValues)
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

        /// <summary>
        /// This method is called if the scheduler has at least one hard constraint violated, but cannot find any possible move to improve the situation.
        /// Most of the time this is a local optimum created by a chain of dependent items that are scheduled in the wrong order.
        /// </summary>
        /// <exception cref="SchedulingException">If unable to esacape from a local optimum.</exception>
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
            var newPlan = _plan.Clone() as SchedulePlan;
            if (newPlan == null) { throw new SchedulingException("Unable to copy schedule plan!"); }
            IDictionary<ItemToSchedule, DependencyNode> dependencyLevels = new Dictionary<ItemToSchedule, DependencyNode>();
            AddToTree(violator.ScheduledItem.ItemToSchedule, dependencyLevels, newPlan, 0);

            using (var dependencyTree = new C5.TreeSet<DependencyNode>())
            {
                dependencyTree.AddAll(dependencyLevels.Values);

                foreach (DependencyNode dependencyNode in dependencyTree)
                {
                    newPlan.Unschedule(dependencyNode.ScheduledItem);
                }

                foreach (DependencyNode dependencyNode in dependencyTree)
                {
                    ViolatorValues bestValues = null;
                    ScheduledItem bestItem = null;
                    foreach (int possibleStart in newPlan.StartValues)
                    {
                        var newItem = new ScheduledItem(dependencyNode.ScheduledItem.ItemToSchedule,
                                                                  possibleStart);
                        ViolatorValues violatorValues = _violationsManager.CheckViolationsForItem(newItem, newPlan);
                        if (bestValues == null
                            || (violatorValues.HardViolationsValue < bestValues.HardViolationsValue)
                            ||
                            (violatorValues.HardViolationsValue == bestValues.HardViolationsValue &&
                             violatorValues.SoftViolationsValue < bestValues.SoftViolationsValue))
                        {
                            bestValues = violatorValues;
                            bestItem = newItem;
                        }
                    }
                    newPlan.Schedule(bestItem);
                }
            }
            _configurationsManager.AddPlanConfiguration(newPlan);
        }

        private static void AddToTree(ItemToSchedule item, IDictionary<ItemToSchedule, DependencyNode> dependencyLevels, SchedulePlan newPlan, int level)
        {
            DependencyNode node;
            if (!dependencyLevels.ContainsKey(item))
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
                    AddToTree(scheduled.ItemToSchedule, dependencyLevels, newPlan, level + 1);
                }
            }
        }

        /// <summary>
        /// This class is used to build the tree when trying to escape a local optimum.
        /// </summary>
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
            var toStartPlan = _plan.Clone() as SchedulePlan;
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
            var toEndPlan = _plan.Clone() as SchedulePlan;
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
            if (newViolatedItems.Count == 0) return;

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

        /// <summary>
        /// Creates the start plan for the scheduler. This is a very important step, because the better the start plan, the faster will the scheduling algorithm solve it.
        /// However, creating a good start plan is almost as hard as scheduling all of the items altogether.
        /// </summary>
        /// <param name="itemsToSchedule">All of the items that need to be scheduled and should be aligned by this method.</param>
        /// <param name="fixedItems">These items must be included in the plan, but must not be moved as they already have a fixed place.</param>
        private void CreateStartPlan(IList<ItemToSchedule> itemsToSchedule, IList<ScheduledItem> fixedItems)
        {
            SchedulePlan oldPlan = CachingResultPlan ? _plan : null;
            _plan = new SchedulePlan();

            /*
             * possibilities:
             * - place them all at 0 (all overlapping) 
             * - place them as they come to the current possible end (<---- currently implemented)
             * - sort them according to duration summary and place them to the current possible end (big to small) 
             * - as before, but inverse (small to big) 
             * - shuffle them and place them to the current possible end
             */

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

                IList<ScheduledItem> oldScheduledItems = oldPlan.ScheduledItems;
                foreach (ScheduledItem oldScheduledItem in oldScheduledItems)
                {
                    ItemToSchedule oldItem = oldScheduledItem.ItemToSchedule;
                    if (!newItemsMap.ContainsKey(oldItem.Id)) continue;
                    ItemToSchedule newItem = newItemsMap[oldItem.Id];
                    if (!oldItem.Equals(newItem)) continue;
                    ScheduledItem scheduledItem = _plan.Add(newItem, oldScheduledItem.Start);
                    UpdateMaxLaneValues(maximumValues, scheduledItem);
                    scheduledFromOldPlan.Add(newItem);
                }
            }

            foreach (ItemToSchedule itemToSchedule in itemsToSchedule)
            {
                if (scheduledFromOldPlan.Contains(itemToSchedule))
                {
                    continue;
                }
                int start = GetPossibleStart(maximumValues, itemToSchedule);
                ScheduledItem scheduledItem = _plan.Add(itemToSchedule, start);
                UpdateMaxLaneValues(maximumValues, scheduledItem);
            }

            // take a snapshot
            _snapshots.Add(_plan.ScheduledItems);
        }

        private static void UpdateMaxLaneValues(IDictionary<Lane, int> maximumValues, ScheduledItem scheduledItem)
        {
            foreach (Lane lane in scheduledItem.ItemToSchedule.Lanes)
            {
                maximumValues.Remove(lane);
                maximumValues.Add(lane, scheduledItem.Start + scheduledItem.ItemToSchedule.GetDuration(lane));
            }
        }

        private static int GetPossibleStart(IDictionary<Lane, int> maximumValues, ItemToSchedule itemToSchedule)
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

        /// <summary>
        /// This method clears the cached result plan so the next scheduling run will not be based on it.
        /// Please note that caching can be disabled altogether by setting the <see cref="CachingResultPlan"/> property to false.
        /// </summary>
        public void ClearCachedResultPlan()
        {
            _plan = null;
        }
    }
}
