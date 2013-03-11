using System;
using System.Collections.Generic;

namespace Schedule4Net.Core
{
    internal class ConfigurationsManager
    {
        private PlanConfiguration _bestPlanConfiguration;
        private readonly ViolationsManager _violationsManager;
        private Violator _referenceViolator;
        private Configuration _referenceConfiguration;
        private Configuration _bestConfiguration;

        public ConfigurationsManager(ViolationsManager violationsManager)
        {
            _violationsManager = violationsManager;
        }

        public bool AddConfiguration(Violator violator, SchedulePlan plan, int start)
        {
            if (_referenceConfiguration.Violator.ScheduledItem.Start == start)
            {
                return false;
            }

            ScheduledItem newItem;
            if (violator.ScheduledItem.ItemToSchedule.MaxDuration + violator.ScheduledItem.Start == plan.Makespan)
            {
                newItem = plan.MoveScheduledItem(violator.ScheduledItem.ItemToSchedule, start);
            }
            else
            {
                newItem = new ScheduledItem(violator.ScheduledItem.ItemToSchedule, start);
            }

            ViolatorUpdate violatorUpdate;
            if (!_violationsManager.TryViolatorUpdate(newItem, plan, out violatorUpdate))
            {
                // the update failed since the new item conflicts against more constraints than the reference
                // if possible, try to switch the item to other lane(s)
                var switchItem = violator.ScheduledItem.ItemToSchedule as SwitchLaneItem;
                if (switchItem == null) { return false; }
                return TryDurationSwitch(plan, start, switchItem);
            }
            AddPossibleConfiguration(plan, violatorUpdate);
            return true;
        }

        private bool TryDurationSwitch(SchedulePlan plan, int start, SwitchLaneItem switchItem)
        {
            bool switchedDurations = false;
            var optionalDurations = switchItem.OptionalDurations;
            foreach (IDictionary<Lane, int> optionalDuration in optionalDurations)
            {
                SwitchLaneItem switched = switchItem.SwitchDurations(optionalDuration);
                ScheduledItem newItem = new ScheduledItem(switched, start);
                ViolatorUpdate violatorUpdate;
                if (!_violationsManager.TryViolatorUpdate(newItem, plan, out violatorUpdate)) continue;
                AddPossibleConfiguration(plan, violatorUpdate);
                switchedDurations = true;
            }
            return switchedDurations;
        }

        private void AddPossibleConfiguration(SchedulePlan plan, ViolatorUpdate violatorUpdate)
        {
            int hardValue = violatorUpdate.UpdatedViolator.HardViolationsValue;
            int softValue = violatorUpdate.UpdatedViolator.SoftViolationsValue;

            int referenceHardValue = _referenceViolator.HardViolationsValue;
            if (referenceHardValue > hardValue || (referenceHardValue == hardValue && _referenceViolator.SoftViolationsValue > softValue))
            {
                ScheduledItem updated = violatorUpdate.UpdatedViolator.ScheduledItem;
                //TODO: the scheduleplan must calculate this, otherwise it will be a wrong result!
                int newMakespan = Math.Max(plan.Makespan, updated.Start + updated.ItemToSchedule.MaxDuration);
                var newConfiguration = new Configuration(violatorUpdate, newMakespan);
                if (_bestConfiguration == null || newConfiguration.CompareTo(_bestConfiguration) == -1)
                {
                    _bestConfiguration = newConfiguration;
                }
            }
        }

        public bool ApplyBestConfiguration(SchedulePlan plan)
        {
            if (_bestConfiguration == null)
            {
                return false;
            }

            ScheduledItem newItem = _bestConfiguration.Violator.ScheduledItem;
            ScheduledItem oldItem = plan.GetScheduledItem(newItem.ItemToSchedule);
            plan.ExchangeScheduledItem(oldItem, newItem);
            _violationsManager.UpdateViolator(_bestConfiguration.ViolatorUpdate);

            return true;
        }

        public void ApplyReferenceConfiguration(SchedulePlan plan)
        {
            ScheduledItem oldItem = _referenceConfiguration.Violator.ScheduledItem;
            plan.MoveScheduledItem(oldItem.ItemToSchedule, oldItem.Start);
        }

        private class Configuration : IComparable<Configuration>
        {
            private readonly int _planMakespan;
            private readonly int _durationSummary;
            internal Violator Violator { get; private set; }
            internal ViolatorUpdate ViolatorUpdate { get; private set; }

            public Configuration(Violator violator, int planMakespan)
            {
                Violator = violator;
                _planMakespan = planMakespan;
                _durationSummary = violator.ScheduledItem.ItemToSchedule.DurationSummary;
                ViolatorUpdate = null;
            }

            public Configuration(ViolatorUpdate violatorUpdate, int planMakespan)
            {
                ViolatorUpdate = violatorUpdate;
                Violator = violatorUpdate.UpdatedViolator;
                _planMakespan = planMakespan;
                _durationSummary = Violator.ScheduledItem.ItemToSchedule.DurationSummary;
            }

            public int CompareTo(Configuration o)
            {
                int result = (_planMakespan < o._planMakespan ? -1 : (_planMakespan == o._planMakespan ? 0 : 1));
                if (result == 0)
                {
                    result = (Violator.HardViolationsValue < o.Violator.HardViolationsValue ? -1
                            : (Violator.HardViolationsValue == o.Violator.HardViolationsValue ? 0 : 1));
                }
                if (result == 0)
                {
                    result = (Violator.SoftViolationsValue < o.Violator.SoftViolationsValue ? -1
                            : (Violator.SoftViolationsValue == o.Violator.SoftViolationsValue ? 0 : 1));
                }
                if (result == 0)
                {
                    result = (_durationSummary < o._durationSummary ? -1 : (_durationSummary == o._durationSummary ? 0 : 1));
                }
                return result;
            }
        }

        public void ResetConfigurations(Violator violator, SchedulePlan plan)
        {
            _referenceViolator = violator;
            _referenceConfiguration = new Configuration(violator, plan.Makespan);
            _bestConfiguration = null;
        }

        public void ResetPlanConfigurations()
        {
            _bestPlanConfiguration = null;
        }

        public void AddPlanConfiguration(SchedulePlan plan)
        {
            ViolatorValues planValues = _violationsManager.CheckViolationsForPlan(plan);
            PlanConfiguration newConfiguration = new PlanConfiguration(plan, planValues);
            if (_bestPlanConfiguration == null || newConfiguration.CompareTo(_bestPlanConfiguration) == -1)
            {
                _bestPlanConfiguration = newConfiguration;
            }
        }

        public SchedulePlan GetBestPlanConfiguration()
        {
            return _bestPlanConfiguration.Plan;
        }

        private class PlanConfiguration : IComparable<PlanConfiguration>
        {
            internal SchedulePlan Plan { get; private set; }
            private readonly int _hardViolationsSum;
            private readonly int _softViolationsSum;

            public PlanConfiguration(SchedulePlan plan, ViolatorValues planValues)
            {
                Plan = plan;
                _hardViolationsSum = planValues.HardViolationsValue;
                _softViolationsSum = planValues.SoftViolationsValue;
            }

            public int CompareTo(PlanConfiguration o)
            {
                int result = (_hardViolationsSum < o._hardViolationsSum ? -1 : (_hardViolationsSum == o._hardViolationsSum ? 0 : 1));
                if (result == 0)
                {
                    result = (Plan.Makespan < o.Plan.Makespan ? -1 : (Plan.Makespan == o.Plan.Makespan ? 0 : 1));
                }
                if (result == 0)
                {
                    result = (_softViolationsSum < o._softViolationsSum ? -1 : (_softViolationsSum == o._softViolationsSum ? 0 : 1));
                }
                return result;
            }
        }
    }
}
