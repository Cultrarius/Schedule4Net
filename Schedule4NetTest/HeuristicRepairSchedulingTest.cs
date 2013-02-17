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
        public void TestScheduleNothing2()
        {
            SchedulePlan result = noConstraintScheduling.Schedule(new List<ItemToSchedule>(), new List<ScheduledItem>());

            Assert.IsTrue(result.ScheduledItems.Count == 0);
            Assert.IsTrue(result.FixedItems.Count == 0);
            Assert.AreEqual(0, result.Makespan);
        }

        [TestMethod]
        public void TestScheduleJustFixed1()
        {
            List<ScheduledItem> fixedItems = new List<ScheduledItem>();
            IDictionary<Lane, int> durations = new Dictionary<Lane, int>();
            durations.Add(new Lane(0), 42);
            ItemToSchedule itemToSchedule = new ItemToSchedule(1, durations, new List<ItemToSchedule>());
            ScheduledItem fixedItem = new ScheduledItem(itemToSchedule, 0);
            fixedItems.Add(fixedItem);
            SchedulePlan result = noConstraintScheduling.Schedule(new List<ItemToSchedule>(), fixedItems);

            Assert.AreEqual(1, result.ScheduledItems.Count);
            Assert.IsTrue(result.ScheduledItems.Contains(fixedItem));
            Assert.AreEqual(42, result.Makespan);
        }

        [TestMethod]
        public void TestScheduleJustFixed2()
        {
            List<ScheduledItem> fixedItems = new List<ScheduledItem>();
            IDictionary<Lane, int> durations = new Dictionary<Lane, int>();
            durations.Add(new Lane(0), 42);
            ItemToSchedule itemToSchedule = new ItemToSchedule(1, durations, new List<ItemToSchedule>());
            ScheduledItem fixedItem = new ScheduledItem(itemToSchedule, 10);
            fixedItems.Add(fixedItem);
            SchedulePlan result = noConstraintScheduling.Schedule(new List<ItemToSchedule>(), fixedItems);

            Assert.AreEqual(1, result.ScheduledItems.Count);
            Assert.IsTrue(result.ScheduledItems.Contains(fixedItem));
            Assert.AreEqual(52, result.Makespan);
        }

        [TestMethod]
        public void TestScheduleJustFixed3()
        {
            List<ScheduledItem> fixedItems = new List<ScheduledItem>();
            IDictionary<Lane, int> durations = new Dictionary<Lane, int>();
            durations.Add(new Lane(0), 42);
            durations.Add(new Lane(1), 50);
            ItemToSchedule itemToSchedule = new ItemToSchedule(1, durations, new List<ItemToSchedule>());
            ScheduledItem fixedItem = new ScheduledItem(itemToSchedule, 10);
            fixedItems.Add(fixedItem);
            SchedulePlan result = noConstraintScheduling.Schedule(new List<ItemToSchedule>(), fixedItems);

            Assert.AreEqual(1, result.ScheduledItems.Count);
            Assert.IsTrue(result.ScheduledItems.Contains(fixedItem));
            Assert.AreEqual(60, result.Makespan);
        }

        [TestMethod]
        public void TestScheduleJustFixed4()
        {
            List<ScheduledItem> fixedItems = new List<ScheduledItem>();
            IDictionary<Lane, int> durations = new Dictionary<Lane, int>();
            durations.Add(new Lane(0), 42);
            durations.Add(new Lane(1), 50);
            ItemToSchedule itemToSchedule1 = new ItemToSchedule(1, durations, new List<ItemToSchedule>());
            ItemToSchedule itemToSchedule2 = new ItemToSchedule(2, durations, new List<ItemToSchedule>());

            ScheduledItem fixedItem1 = new ScheduledItem(itemToSchedule1, 0);
            ScheduledItem fixedItem2 = new ScheduledItem(itemToSchedule2, 50);
            fixedItems.Add(fixedItem1);
            fixedItems.Add(fixedItem2);
            SchedulePlan result = noConstraintScheduling.Schedule(new List<ItemToSchedule>(), fixedItems);

            Assert.AreEqual(2, result.ScheduledItems.Count);
            Assert.IsTrue(result.ScheduledItems.Contains(fixedItem1));
            Assert.IsTrue(result.ScheduledItems.Contains(fixedItem2));
            Assert.AreEqual(100, result.Makespan);
        }

        [TestMethod]
        public void TestScheduleNoConstraint1()
        {
            List<ScheduledItem> fixedItems = new List<ScheduledItem>();
            IDictionary<Lane, int> durations = new Dictionary<Lane, int>();
            durations.Add(new Lane(0), 42);
            List<ItemToSchedule> items = new List<ItemToSchedule>();
            ItemToSchedule itemToSchedule = new ItemToSchedule(1, durations, items);
            items.Add(itemToSchedule);
            SchedulePlan result = noConstraintScheduling.Schedule(items, fixedItems);

            Assert.AreEqual(1, result.ScheduledItems.Count);
            Assert.AreEqual(itemToSchedule, result.ScheduledItems[0].ItemToSchedule);
            Assert.AreEqual(42, result.Makespan);
        }

        [TestMethod]
        public void TestScheduleNoConstraint2Times()
        {
            List<ScheduledItem> fixedItems = new List<ScheduledItem>();
            IDictionary<Lane, int> durations = new Dictionary<Lane, int>();
            durations.Add(new Lane(0), 42);
            List<ItemToSchedule> items = new List<ItemToSchedule>();
            ItemToSchedule itemToSchedule = new ItemToSchedule(1, durations, items);
            items.Add(itemToSchedule);
            noConstraintScheduling.Schedule(items, fixedItems);
            SchedulePlan result = noConstraintScheduling.Schedule(items, fixedItems);

            Assert.AreEqual(1, result.ScheduledItems.Count);
            Assert.AreEqual(itemToSchedule, result.ScheduledItems[0].ItemToSchedule);
            Assert.AreEqual(42, result.Makespan);
        }

        [TestMethod]
        [ExpectedException(typeof(SchedulingException))]
        public void TestScheduleUnsatisfiable()
        {
            // One fixed items leads to an unsatisfiable situation
            List<ScheduledItem> fixedItems = new List<ScheduledItem>();

            IList<ItemToSchedule> items = new List<ItemToSchedule>();

            IDictionary<Lane, int> durations = new Dictionary<Lane, int>();
            durations.Add(new Lane(0), 100);
            ItemToSchedule item1 = new ItemToSchedule(1, durations, new List<ItemToSchedule>());
            items.Add(item1);

            durations.Clear();
            durations.Add(new Lane(0), 100);
            IList<ItemToSchedule> required = new List<ItemToSchedule>();
            required.Add(item1);
            ItemToSchedule item2 = new ItemToSchedule(11, durations, required);
            fixedItems.Add(new ScheduledItem(item2));

            scheduling.Schedule(items, fixedItems);
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
        public void TestScheduleSimpleWithConstraint2()
        {
            // two items, one has to be moved to be scheduled after the other one
            List<ScheduledItem> fixedItems = new List<ScheduledItem>();
            IList<ItemToSchedule> items = new List<ItemToSchedule>();

            IDictionary<Lane, int> durations = new Dictionary<Lane, int>();
            durations.Add(new Lane(0), 100);
            items.Add(new ItemToSchedule(1, durations, new List<ItemToSchedule>()));

            durations.Clear();
            durations.Add(new Lane(1), 100);
            items.Add(new ItemToSchedule(11, durations, new List<ItemToSchedule>()));

            SchedulePlan result = scheduling.Schedule(items, fixedItems);

            Assert.AreEqual(2, result.ScheduledItems.Count);
            Assert.AreEqual(200, result.Makespan);
            Assert.IsTrue(AllConstraintsSatisfied(result));
        }

        [TestMethod]
        public void TestScheduleSimpleWithConstraint3()
        {
            // some different items, none of them collide, but one can be moved to the front, reducing the makespan
            List<ScheduledItem> fixedItems = new List<ScheduledItem>();
            List<ItemToSchedule> items = new List<ItemToSchedule>();

            IDictionary<Lane, int> durations = new Dictionary<Lane, int>();
            durations.Add(new Lane(0), 22);
            items.Add(new ItemToSchedule(1, durations, new List<ItemToSchedule>()));

            durations.Clear();
            durations.Add(new Lane(1), 130);
            items.Add(new ItemToSchedule(2, durations, new List<ItemToSchedule>()));

            durations.Clear();
            durations.Add(new Lane(1), 240);
            durations.Add(new Lane(2), 140);
            items.Add(new ItemToSchedule(3, durations, new List<ItemToSchedule>()));

            durations.Clear();
            durations.Add(new Lane(2), 70);
            items.Add(new ItemToSchedule(4, durations, new List<ItemToSchedule>()));

            durations.Clear();
            durations.Add(new Lane(2), 80);
            items.Add(new ItemToSchedule(5, durations, new List<ItemToSchedule>()));
            durations.Clear();
            durations.Add(new Lane(3), 300);
            items.Add(new ItemToSchedule(6, durations, new List<ItemToSchedule>()));

            SchedulePlan result = scheduling.Schedule(items, fixedItems);

            Assert.AreEqual(items.Count + fixedItems.Count, result.ScheduledItems.Count);
            Assert.AreEqual(370, result.Makespan);
            Assert.IsTrue(AllConstraintsSatisfied(result));
        }

        [TestMethod]
        public void TestScheduleSimpleWithConstraint3Rescheduled()
        {
            // some different items, none of them collide, but one can be moved to the front, reducing the makespan
            List<ScheduledItem> fixedItems = new List<ScheduledItem>();
            List<ItemToSchedule> items = new List<ItemToSchedule>();

            IDictionary<Lane, int> durations = new Dictionary<Lane, int>();
            durations.Add(new Lane(0), 22);
            items.Add(new ItemToSchedule(1, durations, new List<ItemToSchedule>()));

            durations.Clear();
            durations.Add(new Lane(1), 130);
            items.Add(new ItemToSchedule(2, durations, new List<ItemToSchedule>()));

            durations.Clear();
            durations.Add(new Lane(1), 240);
            durations.Add(new Lane(2), 140);
            items.Add(new ItemToSchedule(3, durations, new List<ItemToSchedule>()));

            durations.Clear();
            durations.Add(new Lane(2), 70);
            items.Add(new ItemToSchedule(4, durations, new List<ItemToSchedule>()));

            durations.Clear();
            durations.Add(new Lane(2), 80);
            items.Add(new ItemToSchedule(5, durations, new List<ItemToSchedule>()));
            durations.Clear();
            durations.Add(new Lane(3), 300);
            items.Add(new ItemToSchedule(6, durations, new List<ItemToSchedule>()));

            SchedulePlan result1 = scheduling.Schedule(items, fixedItems);
            SchedulePlan result2 = scheduling.Schedule(items, fixedItems);

            Assert.AreEqual(items.Count + fixedItems.Count, result2.ScheduledItems.Count);
            Assert.AreEqual(370, result2.Makespan);
            CollectionAssert.AreEqual(result1.ScheduledItems, result2.ScheduledItems);
            Assert.IsTrue(AllConstraintsSatisfied(result2));
        }

        [TestMethod]
        public void TestScheduleSimpleWithConstraint4()
        {
            // some different items, most of them collide, some should be switched, but without increasing the makespan
            List<ScheduledItem> fixedItems = new List<ScheduledItem>();
            List<ItemToSchedule> items = new List<ItemToSchedule>();

            IDictionary<Lane, int> durations = new Dictionary<Lane, int>();

            // 1
            durations.Add(new Lane(0), 200);
            durations.Add(new Lane(1), 400);
            items.Add(new ItemToSchedule(1, durations, new List<ItemToSchedule>()));

            // 2
            durations.Clear();
            durations.Add(new Lane(0), 200);
            items.Add(new ItemToSchedule(2, durations, new List<ItemToSchedule>()));

            // 3
            durations.Clear();
            durations.Add(new Lane(1), 200);
            items.Add(new ItemToSchedule(3, durations, new List<ItemToSchedule>()));

            // 3
            durations.Clear();
            durations.Add(new Lane(2), 400);
            items.Add(new ItemToSchedule(13, durations, new List<ItemToSchedule>()));

            // 2
            durations.Clear();
            durations.Add(new Lane(2), 200);
            items.Add(new ItemToSchedule(12, durations, new List<ItemToSchedule>()));

            // 1
            durations.Clear();
            durations.Add(new Lane(3), 200);
            items.Add(new ItemToSchedule(11, durations, new List<ItemToSchedule>()));

            // 2
            durations.Clear();
            durations.Add(new Lane(3), 200);
            items.Add(new ItemToSchedule(22, durations, new List<ItemToSchedule>()));

            SchedulePlan result = scheduling.Schedule(items, fixedItems);

            Assert.AreEqual(items.Count + fixedItems.Count, result.ScheduledItems.Count);
            Assert.AreEqual(600, result.Makespan);
            Assert.IsTrue(AllConstraintsSatisfied(result));
        }

        [TestMethod]
        public void TestScheduleSimpleWithConstraint5()
        {
            // some different items, most of them collide, there is some moving around required
            List<ScheduledItem> fixedItems = new List<ScheduledItem>();
            List<ItemToSchedule> items = new List<ItemToSchedule>();

            IDictionary<Lane, int> durations = new Dictionary<Lane, int>();

            // 1
            durations.Clear();
            durations.Add(new Lane(0), 100);
            items.Add(new ItemToSchedule(1, durations, new List<ItemToSchedule>()));

            // 1
            durations.Clear();
            durations.Add(new Lane(1), 100);
            items.Add(new ItemToSchedule(11, durations, new List<ItemToSchedule>()));

            // 1
            durations.Clear();
            durations.Add(new Lane(2), 100);
            items.Add(new ItemToSchedule(21, durations, new List<ItemToSchedule>()));

            // 1
            durations.Clear();
            durations.Add(new Lane(3), 100);
            items.Add(new ItemToSchedule(31, durations, new List<ItemToSchedule>()));

            // 2
            durations.Clear();
            durations.Add(new Lane(0), 100);
            items.Add(new ItemToSchedule(2, durations, new List<ItemToSchedule>()));

            // 2
            durations.Clear();
            durations.Add(new Lane(1), 200);
            items.Add(new ItemToSchedule(12, durations, new List<ItemToSchedule>()));

            // 2
            durations.Clear();
            durations.Add(new Lane(2), 200);
            items.Add(new ItemToSchedule(22, durations, new List<ItemToSchedule>()));

            // 2
            durations.Clear();
            durations.Add(new Lane(3), 100);
            durations.Add(new Lane(4), 100);
            items.Add(new ItemToSchedule(32, durations, new List<ItemToSchedule>()));

            // 3
            durations.Clear();
            durations.Add(new Lane(0), 100);
            items.Add(new ItemToSchedule(3, durations, new List<ItemToSchedule>()));

            // 3
            durations.Clear();
            durations.Add(new Lane(3), 100);
            items.Add(new ItemToSchedule(33, durations, new List<ItemToSchedule>()));

            // 3
            durations.Clear();
            durations.Add(new Lane(4), 200);
            items.Add(new ItemToSchedule(13, durations, new List<ItemToSchedule>()));

            SchedulePlan result = scheduling.Schedule(items, fixedItems);

            Assert.AreEqual(items.Count + fixedItems.Count, result.ScheduledItems.Count);
            Assert.IsTrue(700 >= result.Makespan);
            Assert.IsTrue(AllConstraintsSatisfied(result));
        }

        [TestMethod]
        public void TestScheduleSimpleWithConstraint6()
        {
            // some different items, most of them collide, some should be switched, but without increasing the makespan
            List<ScheduledItem> fixedItems = new List<ScheduledItem>();
            List<ItemToSchedule> items = new List<ItemToSchedule>();

            IDictionary<Lane, int> durations = new Dictionary<Lane, int>();

            // 1
            durations.Clear();
            durations.Add(new Lane(0), 100);
            durations.Add(new Lane(1), 200);
            items.Add(new ItemToSchedule(1, durations, new List<ItemToSchedule>()));

            // 1
            durations.Clear();
            durations.Add(new Lane(3), 100);
            items.Add(new ItemToSchedule(11, durations, new List<ItemToSchedule>()));

            // 3
            durations.Clear();
            durations.Add(new Lane(1), 100);
            items.Add(new ItemToSchedule(3, durations, new List<ItemToSchedule>()));

            // 3
            durations.Clear();
            durations.Add(new Lane(2), 200);
            items.Add(new ItemToSchedule(33, durations, new List<ItemToSchedule>()));

            // 2
            durations.Clear();
            durations.Add(new Lane(0), 100);
            items.Add(new ItemToSchedule(2, durations, new List<ItemToSchedule>()));

            // 2
            durations.Clear();
            durations.Add(new Lane(2), 100);
            items.Add(new ItemToSchedule(12, durations, new List<ItemToSchedule>()));

            // 2
            durations.Clear();
            durations.Add(new Lane(3), 100);
            items.Add(new ItemToSchedule(22, durations, new List<ItemToSchedule>()));

            SchedulePlan result = scheduling.Schedule(items, fixedItems);

            Assert.AreEqual(items.Count + fixedItems.Count, result.ScheduledItems.Count);
            Assert.AreEqual(300, result.Makespan);
            Assert.IsTrue(AllConstraintsSatisfied(result));
        }

        [TestMethod]
        public void TestScheduleSimpleWithConstraint7()
        {
            // a lot of different items, most of them collide, there is some moving around required
            List<ScheduledItem> fixedItems = new List<ScheduledItem>();
            List<ItemToSchedule> items = new List<ItemToSchedule>();

            IDictionary<Lane, int> durations = new Dictionary<Lane, int>();
            Lane lane0 = new Lane(0);
            Lane lane1 = new Lane(1);
            Lane lane2 = new Lane(2);
            Lane lane3 = new Lane(3);
            Lane lane4 = new Lane(4);
            Lane lane5 = new Lane(5);
            Lane lane6 = new Lane(6);

            // 1
            durations.Clear();
            durations.Add(lane0, 100);
            durations.Add(lane1, 200);
            durations.Add(lane6, 100);
            ItemToSchedule unit1 = new ItemToSchedule(1, durations, new List<ItemToSchedule>());
            items.Add(unit1);

            // 1
            durations.Clear();
            durations.Add(lane3, 100);
            ItemToSchedule unit2 = new ItemToSchedule(11, durations, new List<ItemToSchedule>());
            items.Add(unit2);

            // 1
            durations.Clear();
            durations.Add(lane4, 100);
            items.Add(new ItemToSchedule(21, durations, new List<ItemToSchedule>()));

            // 1
            durations.Clear();
            durations.Add(lane5, 100);
            items.Add(new ItemToSchedule(31, durations, new List<ItemToSchedule>()));

            // 3
            durations.Clear();
            durations.Add(lane0, 100);
            IList<ItemToSchedule> required1 = new List<ItemToSchedule>();
            required1.Add(unit1);
            items.Add(new ItemToSchedule(3, durations, required1));

            // 3
            durations.Clear();
            durations.Add(lane3, 100);
            IList<ItemToSchedule> required2 = new List<ItemToSchedule>();
            required2.Add(unit2);
            items.Add(new ItemToSchedule(13, durations, required2));

            // 3
            durations.Clear();
            durations.Add(lane2, 100);
            items.Add(new ItemToSchedule(33, durations, new List<ItemToSchedule>()));

            // 2
            durations.Clear();
            durations.Add(lane0, 100);
            items.Add(new ItemToSchedule(2, durations, new List<ItemToSchedule>()));

            // 2
            durations.Clear();
            durations.Add(lane3, 200);
            items.Add(new ItemToSchedule(22, durations, new List<ItemToSchedule>()));

            // 2
            durations.Clear();
            durations.Add(lane2, 200);
            durations.Add(lane6, 200);
            items.Add(new ItemToSchedule(12, durations, new List<ItemToSchedule>()));

            // 4
            durations.Clear();
            durations.Add(lane1, 200);
            items.Add(new ItemToSchedule(14, durations, new List<ItemToSchedule>()));

            // 4
            durations.Clear();
            durations.Add(lane4, 100);
            items.Add(new ItemToSchedule(24, durations, new List<ItemToSchedule>()));

            // 4
            durations.Clear();
            durations.Add(lane3, 100);
            items.Add(new ItemToSchedule(44, durations, new List<ItemToSchedule>()));

            // 4
            durations.Clear();
            durations.Add(lane5, 100);
            items.Add(new ItemToSchedule(34, durations, new List<ItemToSchedule>()));

            // 5
            durations.Clear();
            durations.Add(lane5, 100);
            items.Add(new ItemToSchedule(15, durations, new List<ItemToSchedule>()));

            // 5
            durations.Clear();
            durations.Add(lane6, 300);
            items.Add(new ItemToSchedule(25, durations, new List<ItemToSchedule>()));

            SchedulePlan result = scheduling.Schedule(items, fixedItems);

            Assert.AreEqual(items.Count + fixedItems.Count, result.ScheduledItems.Count);
            Assert.IsTrue(700 >= result.Makespan);
            Assert.IsTrue(AllConstraintsSatisfied(result));
        }

        [TestMethod]
        public void TestScheduleSimpleWithConstraint8()
        {
            // schedules the same 4 test for 6 different lanes. Each of the 4 tests collides with a test on the other lane
            // and has to be scheduled separately.
            List<ScheduledItem> fixedItems = new List<ScheduledItem>();
            List<ItemToSchedule> items = new List<ItemToSchedule>();

            IDictionary<Lane, int> durations = new Dictionary<Lane, int>();
            Lane lane0 = new Lane(0);
            Lane lane1 = new Lane(1);
            Lane lane2 = new Lane(2);
            Lane lane3 = new Lane(3);
            Lane lane4 = new Lane(4);
            Lane lane5 = new Lane(5);
            Lane lane6 = new Lane(6);

            // 1
            durations.Clear();
            durations.Add(lane0, 100);
            items.Add(new ItemToSchedule(1, durations, new List<ItemToSchedule>()));

            // 1
            durations.Clear();
            durations.Add(lane1, 100);
            items.Add(new ItemToSchedule(11, durations, new List<ItemToSchedule>()));

            // 1
            durations.Clear();
            durations.Add(lane2, 100);
            items.Add(new ItemToSchedule(21, durations, new List<ItemToSchedule>()));

            // 1
            durations.Clear();
            durations.Add(lane3, 100);
            items.Add(new ItemToSchedule(31, durations, new List<ItemToSchedule>()));

            // 1
            durations.Clear();
            durations.Add(lane4, 100);
            items.Add(new ItemToSchedule(41, durations, new List<ItemToSchedule>()));

            // 1
            durations.Clear();
            durations.Add(lane5, 100);
            items.Add(new ItemToSchedule(51, durations, new List<ItemToSchedule>()));

            // 1
            durations.Clear();
            durations.Add(lane6, 100);
            items.Add(new ItemToSchedule(61, durations, new List<ItemToSchedule>()));

            // 2
            durations.Clear();
            durations.Add(lane0, 100);
            items.Add(new ItemToSchedule(2, durations, new List<ItemToSchedule>()));

            // 2
            durations.Clear();
            durations.Add(lane1, 100);
            items.Add(new ItemToSchedule(12, durations, new List<ItemToSchedule>()));

            // 2
            durations.Clear();
            durations.Add(lane2, 100);
            items.Add(new ItemToSchedule(22, durations, new List<ItemToSchedule>()));

            // 2
            durations.Clear();
            durations.Add(lane3, 100);
            items.Add(new ItemToSchedule(32, durations, new List<ItemToSchedule>()));

            // 2
            durations.Clear();
            durations.Add(lane4, 100);
            items.Add(new ItemToSchedule(42, durations, new List<ItemToSchedule>()));

            // 2
            durations.Clear();
            durations.Add(lane5, 100);
            items.Add(new ItemToSchedule(52, durations, new List<ItemToSchedule>()));

            // 2
            durations.Clear();
            durations.Add(lane6, 100);
            items.Add(new ItemToSchedule(62, durations, new List<ItemToSchedule>()));

            // 3
            durations.Clear();
            durations.Add(lane0, 100);
            items.Add(new ItemToSchedule(3, durations, new List<ItemToSchedule>()));

            // 3
            durations.Clear();
            durations.Add(lane1, 100);
            items.Add(new ItemToSchedule(13, durations, new List<ItemToSchedule>()));

            // 3
            durations.Clear();
            durations.Add(lane2, 100);
            items.Add(new ItemToSchedule(23, durations, new List<ItemToSchedule>()));

            // 3
            durations.Clear();
            durations.Add(lane3, 100);
            items.Add(new ItemToSchedule(33, durations, new List<ItemToSchedule>()));

            // 3
            durations.Clear();
            durations.Add(lane4, 100);
            items.Add(new ItemToSchedule(43, durations, new List<ItemToSchedule>()));

            // 3
            durations.Clear();
            durations.Add(lane5, 100);
            items.Add(new ItemToSchedule(53, durations, new List<ItemToSchedule>()));

            // 3
            durations.Clear();
            durations.Add(lane6, 100);
            items.Add(new ItemToSchedule(63, durations, new List<ItemToSchedule>()));

            // 4
            durations.Clear();
            durations.Add(lane0, 100);
            items.Add(new ItemToSchedule(4, durations, new List<ItemToSchedule>()));

            // 4
            durations.Clear();
            durations.Add(lane1, 100);
            items.Add(new ItemToSchedule(14, durations, new List<ItemToSchedule>()));

            // 4
            durations.Clear();
            durations.Add(lane2, 100);
            items.Add(new ItemToSchedule(24, durations, new List<ItemToSchedule>()));

            // 4
            durations.Clear();
            durations.Add(lane3, 100);
            items.Add(new ItemToSchedule(34, durations, new List<ItemToSchedule>()));

            // 4
            durations.Clear();
            durations.Add(lane4, 100);
            items.Add(new ItemToSchedule(44, durations, new List<ItemToSchedule>()));

            // 4
            durations.Clear();
            durations.Add(lane5, 100);
            items.Add(new ItemToSchedule(54, durations, new List<ItemToSchedule>()));

            // 4
            durations.Clear();
            durations.Add(lane6, 100);
            items.Add(new ItemToSchedule(64, durations, new List<ItemToSchedule>()));

            SchedulePlan result = scheduling.Schedule(items, fixedItems);

            Assert.AreEqual(items.Count + fixedItems.Count, result.ScheduledItems.Count);
            Assert.IsTrue(800 >= result.Makespan);
            Assert.IsTrue(AllConstraintsSatisfied(result));
        }

        [TestMethod]
        public void TestScheduleSimpleWithFixed()
        {
            // a lot of different items, most of them collide, there is some moving around required and fixed items as well
            List<ScheduledItem> fixedItems = new List<ScheduledItem>();
            List<ItemToSchedule> items = new List<ItemToSchedule>();

            IDictionary<Lane, int> durations = new Dictionary<Lane, int>();
            Lane lane0 = new Lane(0);
            Lane lane1 = new Lane(1);
            Lane lane2 = new Lane(2);
            Lane lane3 = new Lane(3);
            Lane lane4 = new Lane(4);
            Lane lane5 = new Lane(5);
            Lane lane6 = new Lane(6);

            // 1
            durations.Clear();
            durations.Add(lane0, 100);
            durations.Add(lane1, 200);
            durations.Add(lane6, 100);
            ItemToSchedule unit1 = new ItemToSchedule(1, durations, new List<ItemToSchedule>());
            fixedItems.Add(new ScheduledItem(unit1));

            // 1
            durations.Clear();
            durations.Add(lane3, 100);
            ItemToSchedule unit2 = new ItemToSchedule(11, durations, new List<ItemToSchedule>());
            items.Add(unit2);

            // 1
            durations.Clear();
            durations.Add(lane4, 100);
            items.Add(new ItemToSchedule(21, durations, new List<ItemToSchedule>()));

            // 1
            durations.Clear();
            durations.Add(lane5, 100);
            items.Add(new ItemToSchedule(31, durations, new List<ItemToSchedule>()));

            // 3
            durations.Clear();
            durations.Add(lane0, 100);
            IList<ItemToSchedule> required1 = new List<ItemToSchedule>();
            required1.Add(unit1);
            items.Add(new ItemToSchedule(3, durations, required1));

            // 3
            durations.Clear();
            durations.Add(lane3, 100);
            IList<ItemToSchedule> required2 = new List<ItemToSchedule>();
            required2.Add(unit2);
            items.Add(new ItemToSchedule(13, durations, required2));

            // 3
            durations.Clear();
            durations.Add(lane2, 100);
            items.Add(new ItemToSchedule(33, durations, new List<ItemToSchedule>()));

            // 2
            durations.Clear();
            durations.Add(lane0, 100);
            items.Add(new ItemToSchedule(2, durations, new List<ItemToSchedule>()));

            // 2
            durations.Clear();
            durations.Add(lane3, 200);
            items.Add(new ItemToSchedule(22, durations, new List<ItemToSchedule>()));

            // 2
            durations.Clear();
            durations.Add(lane2, 200);
            durations.Add(lane6, 200);
            ItemToSchedule item12 = new ItemToSchedule(12, durations, new List<ItemToSchedule>());
            fixedItems.Add(new ScheduledItem(item12, 100));

            // 4
            durations.Clear();
            durations.Add(lane1, 200);
            items.Add(new ItemToSchedule(14, durations, new List<ItemToSchedule>()));

            // 4
            durations.Clear();
            durations.Add(lane4, 100);
            items.Add(new ItemToSchedule(24, durations, new List<ItemToSchedule>()));

            // 4
            durations.Clear();
            durations.Add(lane3, 100);
            items.Add(new ItemToSchedule(44, durations, new List<ItemToSchedule>()));

            // 4
            durations.Clear();
            durations.Add(lane5, 100);
            items.Add(new ItemToSchedule(34, durations, new List<ItemToSchedule>()));

            // 5
            durations.Clear();
            durations.Add(lane5, 100);
            items.Add(new ItemToSchedule(15, durations, new List<ItemToSchedule>()));

            // 5
            durations.Clear();
            durations.Add(lane6, 300);
            items.Add(new ItemToSchedule(25, durations, new List<ItemToSchedule>()));

            SchedulePlan result = scheduling.Schedule(items, fixedItems);

            Assert.AreEqual(items.Count + fixedItems.Count, result.ScheduledItems.Count);
            Assert.IsTrue(700 >= result.Makespan);
            Assert.IsTrue(AllConstraintsSatisfied(result));
        }

        [TestMethod]
        public void TestScheduleLocalOptimum1()
        {
            // A very simple local optimum that can be escaped by using the dependencies
            List<ScheduledItem> fixedItems = new List<ScheduledItem>();
            List<ItemToSchedule> items = new List<ItemToSchedule>();

            IDictionary<Lane, int> durations = new Dictionary<Lane, int>();
            Lane lane0 = new Lane(0);
            Lane lane1 = new Lane(1);

            // Test 1
            durations.Clear();
            durations.Add(lane0, 400);
            ItemToSchedule unit1 = new ItemToSchedule(1, durations, new List<ItemToSchedule>());
            items.Add(unit1);

            // Test 11
            durations.Clear();
            durations.Add(lane1, 200);
            ItemToSchedule unit2 = new ItemToSchedule(11, durations, new List<ItemToSchedule>());

            // Test 2 (req Test 11)
            durations.Clear();
            durations.Add(lane1, 200);
            IList<ItemToSchedule> required = new List<ItemToSchedule>();
            required.Add(unit2);
            ItemToSchedule unit3 = new ItemToSchedule(2, durations, required);
            items.Add(unit3);

            items.Add(unit2);

            // Test 22 (req Test 11, Test 2)
            durations.Clear();
            durations.Add(lane1, 200);
            required = new List<ItemToSchedule> { unit2, unit3 };
            items.Add(new ItemToSchedule(22, durations, required));

            SchedulePlan result = scheduling.Schedule(items, fixedItems);

            Assert.AreEqual(items.Count + fixedItems.Count, result.ScheduledItems.Count);
            Assert.AreEqual(600, result.Makespan);
            Assert.IsTrue(AllConstraintsSatisfied(result));
        }

        [TestMethod]
        public void TestScheduleLocalOptimum2()
        {
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
            IList<ItemToSchedule> required = new List<ItemToSchedule>();
            required.Add(unit1);
            ItemToSchedule unit13 = new ItemToSchedule(13, durations, required);
            items.Add(unit13);

            // Test 23 (req. Test 2)
            durations.Clear();
            durations.Add(lane1, 200);
            required = new List<ItemToSchedule> { unit2 };
            ItemToSchedule unit23 = new ItemToSchedule(23, durations, required);
            items.Add(unit23);

            // Test 4 (req. Test 13)
            durations.Clear();
            durations.Add(lane0, 200);
            required = new List<ItemToSchedule> { unit13 };
            ItemToSchedule unit4 = new ItemToSchedule(4, durations, required);
            items.Add(unit4);

            // Test 5 (req. Test 23)
            durations.Clear();
            durations.Add(lane1, 200);
            required = new List<ItemToSchedule> { unit23 };
            ItemToSchedule unit5 = new ItemToSchedule(5, durations, required);
            items.Add(unit5);

            SchedulePlan result = scheduling.Schedule(items, fixedItems);

            Assert.AreEqual(items.Count + fixedItems.Count, result.ScheduledItems.Count);
            Assert.AreEqual(800, result.Makespan);
            Assert.IsTrue(AllConstraintsSatisfied(result));
        }

        private static List<ItemToSchedule> InitializeItemsToForTest(int lanes, int itemsPerLane)
        {
            List<ItemToSchedule> list = new List<ItemToSchedule>();
            IDictionary<Lane, int> durations = new Dictionary<Lane, int>();
            IDictionary<Lane, List<ItemToSchedule>> testsForLane = new Dictionary<Lane, List<ItemToSchedule>>();

            for (int i = 0; i < lanes; i++)
            {
                testsForLane.Add(new Lane(i), new List<ItemToSchedule>());
            }

            for (int i = 0; i < lanes * itemsPerLane; i++)
            {
                int counter = i % lanes;
                Lane lane = new Lane(counter);
                durations.Clear();
                durations.Add(lane, 100);
                List<ItemToSchedule> previousTests = testsForLane[lane];
                List<ItemToSchedule> requiredItems = new List<ItemToSchedule>(previousTests);
                ItemToSchedule test = new ItemToSchedule(counter * 10 + i / lanes, durations, requiredItems);
                previousTests.Add(test);
                list.Add(test);
            }

            return list;
        }

        [TestMethod]
        public void TestScheduleGeneratedLocalOptimum1()
        {
            // Creates a fixed number of tests for each lane, where each test depends on the previous ones. This inevitably
            // leads to some local optima (the more tests the more optima) that can be escaped by using the dependencies.
            List<ScheduledItem> fixedItems = new List<ScheduledItem>();
            List<ItemToSchedule> items = InitializeItemsToForTest(10, 3);

            SchedulePlan result = scheduling.Schedule(items, fixedItems);

            Assert.AreEqual(items.Count + fixedItems.Count, result.ScheduledItems.Count);
            Assert.AreEqual(1200, result.Makespan);
            Assert.IsTrue(AllConstraintsSatisfied(result));
        }

        [TestMethod]
        public void TestScheduleGeneratedLocalOptimum2()
        {
            // Creates a fixed number of tests for each lane, where each test depends on the previous ones. This inevitably
            // leads to some local optima (the more tests the more optima) that can be escaped by using the dependencies.
            List<ScheduledItem> fixedItems = new List<ScheduledItem>();
            List<ItemToSchedule> items = InitializeItemsToForTest(7, 7);

            SchedulePlan result = scheduling.Schedule(items, fixedItems);

            Assert.AreEqual(items.Count + fixedItems.Count, result.ScheduledItems.Count);
            Assert.AreEqual(1300, result.Makespan);
            Assert.IsTrue(AllConstraintsSatisfied(result));
        }

        [TestMethod]
        public void TestGenerateManyTests1()
        {
            // Creates a large number of tests and tries to schedule them. This should not make too many problems as there is just one item per lane.
            List<ScheduledItem> fixedItems = new List<ScheduledItem>();
            List<ItemToSchedule> items = InitializeItemsToForTest(100, 1);

            SchedulePlan result = scheduling.Schedule(items, fixedItems);

            Assert.AreEqual(items.Count + fixedItems.Count, result.ScheduledItems.Count);
            Assert.AreEqual(100 * 100, result.Makespan);
            Assert.IsTrue(AllConstraintsSatisfied(result));
        }

        [TestMethod]
        public void TestGenerateManyTestsTwice()
        {
            // Creates a large number of tests and tries to schedule them.
            // The same tests are scheduled a second time to see if the result caching works as expected.
            List<ScheduledItem> fixedItems = new List<ScheduledItem>();
            List<ItemToSchedule> items = InitializeItemsToForTest(150, 1);

            Stopwatch sw = new Stopwatch();
            sw.Start();
            SchedulePlan firstResult = scheduling.Schedule(items, fixedItems);
            sw.Stop();
            long firstRun = sw.ElapsedMilliseconds;
            sw.Reset();
            sw.Start();
            SchedulePlan secondResult = scheduling.Schedule(items, fixedItems);
            sw.Stop();
            long secondRun = sw.ElapsedMilliseconds;
            CollectionAssert.AreEquivalent(firstResult.ScheduledItems, secondResult.ScheduledItems);
            Assert.AreEqual(firstResult.Makespan, secondResult.Makespan);
            Assert.IsTrue(AllConstraintsSatisfied(firstResult));
            Assert.IsTrue(AllConstraintsSatisfied(secondResult));

            // the second run should be much faster because the scheduler cached the previous result
            Debug.WriteLine("First: " + firstRun + " Second: " + secondRun);
            Assert.IsTrue(firstRun > (secondRun * 1.5));
        }

        [TestMethod]
        public void testHarderLocalOptimum1() {
            // This local optimum cannot be solved by using the dependencies, but the items must be shifted.
            List<ScheduledItem> fixedItems = new List<ScheduledItem>();
            List<ItemToSchedule> items = new List<ItemToSchedule>();

            IDictionary<Lane, int> durations = new Dictionary<Lane, int>();
            Lane lane0 = new Lane(0);
            Lane lane1 = new Lane(1);

            // Test 1
            durations.Clear();
            durations.Add(lane0, 100);
            ItemToSchedule unit1 = new ItemToSchedule(1, durations, new List<ItemToSchedule>());
            items.Add(unit1);

            // Test 2
            durations.Clear();
            durations.Add(lane0, 100);
            ItemToSchedule unit2 = new ItemToSchedule(2, durations, new List<ItemToSchedule>());
            items.Add(unit2);

            // Test 11
            durations.Clear();
            durations.Add(lane1, 100);
            ItemToSchedule unit11 = new ItemToSchedule(11, durations, new List<ItemToSchedule>());
            items.Add(unit11);

            // Test 22
            durations.Clear();
            durations.Add(lane1, 100);
            ItemToSchedule unit22 = new ItemToSchedule(22, durations, new List<ItemToSchedule>());
            items.Add(unit22);

            // create a new special constraint

            TestConstraint1 newConstraint = new TestConstraint1
                {
                    unit1 = unit1,
                    unit11 = unit11,
                    unit2 = unit2,
                    unit22 = unit22
                };

            pairConstraints.Add(newConstraint);
            scheduling = new HeuristicRepairScheduling(singleConstraints, pairConstraints);

            SchedulePlan result = scheduling.Schedule(items, fixedItems);

            Assert.AreEqual(items.Count + fixedItems.Count, result.ScheduledItems.Count);
            Assert.AreEqual(400, result.Makespan);
            Assert.IsTrue(AllConstraintsSatisfied(result));
        }

        internal class TestConstraint1 : AbstractPairConstraint<ScheduledItem>
        {
            internal ItemToSchedule unit1;
            internal ItemToSchedule unit11;
            internal ItemToSchedule unit2;
            internal ItemToSchedule unit22;

            protected override ConstraintDecision CheckConstraint(ScheduledItem item1, ScheduledItem item2)
            {
                ScheduledItem itemA = null;
                ScheduledItem itemB = null;
                if (item1.ItemToSchedule == unit1 && item2.ItemToSchedule == unit2) {
                    itemA = item1;
                    itemB = item2;
                } else if (item2.ItemToSchedule == unit1 && item1.ItemToSchedule == unit2) {
                    itemA = item2;
                    itemB = item1;
                } else if (item1.ItemToSchedule == unit11 && item2.ItemToSchedule == unit22) {
                    itemA = item1;
                    itemB = item2;
                } else if (item2.ItemToSchedule == unit11 && item1.ItemToSchedule == unit22) {
                    itemA = item2;
                    itemB = item1;
                }

                if (itemA != null && itemB != null && (itemA.Start + itemA.ItemToSchedule.MaxDuration) != itemB.Start) {
                    return new ConstraintDecision(true, false, 100);
                }

                return new ConstraintDecision(true, true, 0);
            }
        }

        [TestMethod]
        public void testHarderLocalOptimum2() {
            // This local optimum cannot be solved by using the dependencies or a right-shift, but the items must be shifted
            // to the left.
            List<ScheduledItem> fixedItems = new List<ScheduledItem>();
            List<ItemToSchedule> items = new List<ItemToSchedule>();

            IDictionary<Lane, int> durations = new Dictionary<Lane, int>();
            Lane lane0 = new Lane(0);
            Lane lane1 = new Lane(1);

            // Test 1
            durations.Clear();
            durations.Add(lane0, 100);
            ItemToSchedule unit1 = new ItemToSchedule(1, durations, new List<ItemToSchedule>());
            items.Add(unit1);

            // Test 2
            durations.Clear();
            durations.Add(lane0, 100);
            IList<ItemToSchedule> req2 = new List<ItemToSchedule>();
            req2.Add(unit1);
            ItemToSchedule unit2 = new ItemToSchedule(2, durations, req2);
            items.Add(unit2);

            // Test 3 (has to be scheduled before test 1 and test 4)
            durations.Clear();
            durations.Add(lane0, 100);
            ItemToSchedule unit3 = new ItemToSchedule(3, durations, new List<ItemToSchedule>());
            items.Add(unit3);

            // Test 4
            durations.Clear();
            durations.Add(lane1, 100);
            ItemToSchedule unit4 = new ItemToSchedule(4, durations, new List<ItemToSchedule>());
            items.Add(unit4);

            // Test 5
            durations.Clear();
            durations.Add(lane1, 100);
            ItemToSchedule unit5 = new ItemToSchedule(5, durations, new List<ItemToSchedule>());
            items.Add(unit5);

            // create a new special constraint

            TestConstraint2 newConstraint = new TestConstraint2()
            {
                unit1 = unit1,
                unit3 = unit3,
                unit4 = unit4
            };

            pairConstraints.Add(newConstraint);
            scheduling = new HeuristicRepairScheduling(singleConstraints, pairConstraints);

            SchedulePlan result = scheduling.Schedule(items, fixedItems);

            Assert.AreEqual(items.Count + fixedItems.Count, result.ScheduledItems.Count);
            Assert.AreEqual(300, result.Makespan);
            Assert.IsTrue(AllConstraintsSatisfied(result));
        }

internal class TestConstraint2 : AbstractPairConstraint<ScheduledItem>
        {
            internal ItemToSchedule unit1;
            internal ItemToSchedule unit3;
            internal ItemToSchedule unit4;

            protected override ConstraintDecision CheckConstraint(ScheduledItem item1, ScheduledItem item2)
            {
                ScheduledItem itemA = null;
                    ScheduledItem itemB = null;
                    if (item1.ItemToSchedule == unit1 && item2.ItemToSchedule == unit3) {
                        itemA = item1;
                        itemB = item2;
                    } else if (item2.ItemToSchedule == unit1 && item1.ItemToSchedule == unit3) {
                        itemA = item2;
                        itemB = item1;
                    } else if (item1.ItemToSchedule == unit4 && item2.ItemToSchedule == unit3) {
                        itemA = item1;
                        itemB = item2;
                    } else if (item2.ItemToSchedule == unit4 && item1.ItemToSchedule == unit3) {
                        itemA = item2;
                        itemB = item1;
                    }

                    if (itemA != null && itemB != null && (itemB.Start + itemB.ItemToSchedule.MaxDuration) > itemA.Start) {
                        return new ConstraintDecision(true, false, 100);
                    }

                    return new ConstraintDecision(true, true, 0);
            }
        }
    }
}