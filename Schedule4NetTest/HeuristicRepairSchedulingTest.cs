using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Schedule4Net;
using Schedule4Net.Constraint;
using Schedule4Net.Constraint.Impl;
using System.Diagnostics;

namespace Schedule4NetTest
{
    [TestClass]
    public class HeuristicRepairSchedulingTest
    {
        private HeuristicRepairScheduling scheduling;
        //private ViolationsManager manager;

        private HeuristicRepairScheduling noConstraintScheduling;
        //private ViolationsManager noConstraintManager;
        private List<SingleItemConstraint> singleConstraints;
        private List<ItemPairConstraint> pairConstraints;

        #region Additional test attributes

        //
        //You can use the following additional attributes as you write your tests:
        //
        //Use ClassInitialize to run code before running the first test in the class
        //[ClassInitialize()]
        //public static void MyClassInitialize(TestContext testContext)
        //{
        //}
        //
        //Use ClassCleanup to run code after all tests in a class have run
        //[ClassCleanup()]
        //public static void MyClassCleanup()
        //{
        //}
        //
        //Use TestInitialize to run code before running each test
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

            noConstraintScheduling = new HeuristicRepairScheduling(new List<SingleItemConstraint>(),
                                                                   new List<ItemPairConstraint>());

            //manager = new ViolationsManager(singleConstraints, pairConstraints);
            scheduling = new HeuristicRepairScheduling(singleConstraints, pairConstraints);
        }

        //
        //Use TestCleanup to run code after each test has run
        //[TestCleanup()]
        //public void MyTestCleanup()
        //{
        //}
        //

        #endregion

        private bool AllConstraintsSatisfied(SchedulePlan plan)
        {
            List<ScheduledItem> scheduledItems = plan.ScheduledItems;
            foreach (ScheduledItem item1 in scheduledItems)
            {
                foreach (SingleItemConstraint constraint in singleConstraints)
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

                    foreach (ItemPairConstraint constraint in pairConstraints)
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
        public void TestScheduleNothing1()
        {
            SchedulePlan result = noConstraintScheduling.Schedule(new List<ItemToSchedule>());

            Assert.AreEqual(0, result.ScheduledItems.Count);
            Assert.AreEqual(0, result.FixedItems.Count);
            Assert.AreEqual(0, result.Makespan);
        }

        [TestMethod]
        public void TestScheduleSimpleWithConstraint1()
        {
            // just one item
            List<ScheduledItem> fixedItems = new List<ScheduledItem>();

            Dictionary<Lane, int> durations = new Dictionary<Lane, int> { { new Lane(0), 42 } };
            List<ItemToSchedule> items = new List<ItemToSchedule>();
            ItemToSchedule itemToSchedule = new ItemToSchedule(1, durations, items);
            items.Add(itemToSchedule);

            SchedulePlan result = scheduling.Schedule(items, fixedItems);

            Assert.AreEqual(1, result.ScheduledItems.Count);
            Assert.AreEqual(itemToSchedule, result.ScheduledItems[0].ItemToSchedule);
            Assert.AreEqual(42, result.Makespan);
            Assert.IsTrue(AllConstraintsSatisfied(result));
        }

        [TestMethod]
        public void TestScheduleLocalOptimum2() {
        // A very simple local optimum that can be escaped by using the dependencies
        List<ScheduledItem> fixedItems = new List<ScheduledItem>();
        List<ItemToSchedule> items = new List<ItemToSchedule>();

        Dictionary<Lane, int> durations = new Dictionary<Lane, int>();
        Lane lane0 = new Lane(0);
        Lane lane1 = new Lane(1);

        // Test 1
        durations.Clear();
        durations.Add(lane0, 200);
        ItemToSchedule unit1 = new ItemToSchedule(1, durations, new List<ItemToSchedule>());
        items.Add(unit1);

        // Test 2
        durations.Clear();
        durations.Add(lane1, 200);
        ItemToSchedule unit2 = new ItemToSchedule(2, durations, new List<ItemToSchedule>());
        items.Add(unit2);

        // Test 13 (req. Test 1)
        durations.Clear();
        durations.Add(lane0, 200);
        ICollection<ItemToSchedule> required = new List<ItemToSchedule>();
        required.Add(unit1);
        ItemToSchedule unit13 = new ItemToSchedule(13, durations, required);
        items.Add(unit13);

        // Test 23 (req. Test 2)
        durations.Clear();
        durations.Add(lane1, 200);
        required = new List<ItemToSchedule> {unit2};
            ItemToSchedule unit23 = new ItemToSchedule(23, durations, required);
        items.Add(unit23);

        // Test 4 (req. Test 13)
        durations.Clear();
        durations.Add(lane0, 200);
        required = new List<ItemToSchedule> {unit13};
            ItemToSchedule unit4 = new ItemToSchedule(4, durations, required);
        items.Add(unit4);

        // Test 5 (req. Test 23)
        durations.Clear();
        durations.Add(lane1, 200);
        required = new List<ItemToSchedule> {unit23};
            ItemToSchedule unit5 = new ItemToSchedule(5, durations, required);
        items.Add(unit5);

        SchedulePlan result = scheduling.Schedule(items, fixedItems);

        Assert.AreEqual(items.Count + fixedItems.Count, result.ScheduledItems.Count);
        Assert.AreEqual(800, result.Makespan);
        Assert.IsTrue(AllConstraintsSatisfied(result));
    }
    }
}