using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Schedule4Net;
using Schedule4Net.Constraint;
using Schedule4Net.Constraint.Impl;

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
            singleConstraints = new List<SingleItemConstraint> {new StartNowConstraint()};

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

        [TestMethod]
        public void TestScheduleNothing1()
        {
            SchedulePlan result = noConstraintScheduling.Schedule(new List<ItemToSchedule>());

            Assert.AreEqual(0, result.ScheduledItems.Count);
            Assert.AreEqual(0, result.FixedItems.Count);
            Assert.AreEqual(0, result.Makespan);
        }
    }
}