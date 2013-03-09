using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Schedule4Net;
using Schedule4Net.Constraint;
using Schedule4Net.Constraint.Impl;
using System.Diagnostics;
using Schedule4Net.Core.Exception;

namespace Schedule4NetTest
{
    [TestClass]
    public class SwitchLaneTest
    {
        private Scheduler scheduling;

        private Scheduler noConstraintScheduling;
        private List<SingleItemConstraint> singleConstraints;
        private List<ItemPairConstraint> pairConstraints;

        #region Additional test attributes

        [TestInitialize]
        public void MyTestInitialize()
        {
            singleConstraints = new List<SingleItemConstraint> { new StartNowConstraint() };

            pairConstraints = new List<ItemPairConstraint>
                {
                    new NoOverlappingConstraint(),
                    new DependenciesConstraint(),
                    new DebugTestConstraint()
                };

            noConstraintScheduling = new Scheduler(new List<SingleItemConstraint>(),
                                                                   new List<ItemPairConstraint>());
            scheduling = new Scheduler(singleConstraints, pairConstraints);
        }

        #endregion

        private static bool AllConstraintsSatisfied(SchedulePlan plan, IList<SingleItemConstraint> testedSingleConstraints, IList<ItemPairConstraint> testedPairConstraints)
        {
            List<ScheduledItem> scheduledItems = plan.ScheduledItems;
            foreach (ScheduledItem item1 in scheduledItems)
            {
                foreach (SingleItemConstraint constraint in testedSingleConstraints)
                {
                    ConstraintDecision decision = constraint.Check(item1);
                    if (!decision.HardConstraint || decision.Fulfilled) continue;
                    Debug.WriteLine("Constraint violated! Item: " + item1 + ", Constraint: " + constraint.GetType());
                    return false;
                }

                foreach (ScheduledItem item2 in scheduledItems)
                {
                    if (item1 == item2)
                    {
                        continue;
                    }

                    foreach (ItemPairConstraint constraint in testedPairConstraints)
                    {
                        ConstraintDecision decision = constraint.Check(item1, item2);
                        if (!decision.HardConstraint || decision.Fulfilled) continue;
                        Debug.WriteLine("Constraint violated! Item1: " + item1 + ", Item2: " + item2 + ", Constraint: " + constraint.GetType());
                        return false;
                    }
                }
            }

            return true;
        }

        [TestMethod]
        public void TestScheduleSimpleSwitch()
        {
            Lane lane1 = new Lane(1);
            Lane lane2 = new Lane(2);

            IList<IDictionary<Lane, int>> optionalDurations = new List<IDictionary<Lane, int>> { new Dictionary<Lane, int> { { lane2, 100 } } };
            SwitchLaneItem item1 = new SwitchLaneItem(1, new Dictionary<Lane, int> { { lane1, 100 } }, new List<ItemToSchedule>(), optionalDurations);
            SwitchLaneItem item2 = new SwitchLaneItem(2, new Dictionary<Lane, int> { { lane1, 100 } }, new List<ItemToSchedule>(), optionalDurations);
            IList<ItemToSchedule> items = new List<ItemToSchedule> { item1, item2 };

            SchedulePlan result = scheduling.Schedule(items);

            Assert.AreEqual(2, result.ScheduledItems.Count);
            Assert.AreEqual(100, result.Makespan);
            Assert.IsTrue(AllConstraintsSatisfied(result, singleConstraints, pairConstraints));
        }
    }
}