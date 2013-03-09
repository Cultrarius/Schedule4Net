using System;
using System.Collections.Generic;
using Schedule4Net.Constraint.Impl;

namespace Schedule4Net
{
    [Serializable]
    public class SwitchLaneItem : ItemToSchedule
    {
        private readonly IEnumerable<IDictionary<Lane, int>> _optionalDurations;

        public IEnumerable<IDictionary<Lane, int>> OptionalDurations
        {
            get { return _optionalDurations == null ? null : new List<IDictionary<Lane, int>>(_optionalDurations); }
        }

        /// <summary>
        /// Creates a new item that is able to switch to other lanes.
        /// </summary>
        /// <param name="id">The unique id of the item.</param>
        /// <param name="durations">The duration that this item requires on each lane.</param>
        /// <param name="requiredItems">The items that are required by this item. This parameter can be used by some constraints (e.g. <see cref="DependenciesConstraint" />) and to create a good start configuration of the plan.</param>
        /// <param name="optionalDurations">The durations that the scheduler can use if they are better fitting to create a schedule.</param>
        /// <exception cref="System.ArgumentException">The optional durations must not be null or empty.</exception>
        public SwitchLaneItem(int id, IDictionary<Lane, int> durations, IEnumerable<ItemToSchedule> requiredItems, IList<IDictionary<Lane, int>> optionalDurations) : base(id, durations, requiredItems)
        {
            if (optionalDurations == null || optionalDurations.Count == 0)
            {
                throw new ArgumentException("The optional durations must not be null or empty. Consider using a normal ItemToSchedule instead."); 
            }
            _optionalDurations = optionalDurations;
        }

        public SwitchLaneItem SwitchDurations(IDictionary<Lane, int> newDurations)
        {
            IList<IDictionary<Lane, int>> newOptionalDurations = GetNewOptionalDurations(newDurations);
            return GetNewItemWithSwitchedDurations(new Dictionary<Lane, int>(newDurations), newOptionalDurations);
        }

        protected virtual SwitchLaneItem GetNewItemWithSwitchedDurations(IDictionary<Lane, int> newDurations, IList<IDictionary<Lane, int>> newOptionalDurations)
        {
            return new SwitchLaneItem(Id, newDurations, RequiredItems, newOptionalDurations);
        }

        private IList<IDictionary<Lane, int>> GetNewOptionalDurations(IDictionary<Lane, int> newDurations)
        {
            IList<IDictionary<Lane, int>> newOptionalDurations = new List<IDictionary<Lane, int>> {Durations};
            bool foundNewDuration = false;
            foreach (IDictionary<Lane, int> optionalDuration in _optionalDurations)
            {
                if (foundNewDuration) 
                    newOptionalDurations.Add(optionalDuration);
                if (DictEquals(optionalDuration, newDurations))
                {
                    foundNewDuration = true;
                    continue;
                }
                newOptionalDurations.Add(optionalDuration);
            }
            if (!foundNewDuration)
            {
                throw new ArgumentException("Unable to find new durations in current optional durations!");
            }
            return newOptionalDurations;
        }

        private static bool DictEquals<TK, TV>(IDictionary<TK, TV> d1, IDictionary<TK, TV> d2)
        {
            if (d1.Count != d2.Count)
                return false;

            foreach (KeyValuePair<TK, TV> pair in d1)
            {
                if (!d2.ContainsKey(pair.Key))
                    return false;

                if (!Equals(d2[pair.Key], pair.Value))
                    return false;
            }

            return true;
        }
    }
}
