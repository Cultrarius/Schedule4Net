using System;
using System.Collections.Generic;
using Schedule4Net.Constraint.Impl;

namespace Schedule4Net
{
    /// <summary>
    /// Like the <see cref="ItemToSchedule"/> this represents the entity that the scheduling algorithm has to create the schedule from.
    /// However, where the normal <c>ItemToSchedule</c> is linked to a specific set of <see cref="Lane"/>s, this item also has optional lane durations.
    /// The scheduler might at any point decide to switch the item to one of the provided optional durations.
    /// For example, this can be used to model a workprocess that can run on one of several possible machines.
    /// </summary>
    /// <remarks>
    /// Please note that the use of this item comes at the cost of increased scheduling complexity.
    /// Also note that the scheduler will try the optional duration if, and only if, a rescheduling on the current lane is not possible.
    /// So, as long as there is the possibility to stay on the current lane, the scheduler will not use the optional durations.
    /// </remarks>
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

        /// <summary>
        /// Switches the currently active durations to one of the optional ones and returns the resulting item.
        /// </summary>
        /// <param name="newDurations">The new durations to switch to. This _must_ be one of the currently optional durations, otherwise an exception is thrown.</param>
        /// <returns>The newly constructed item. The item that the method is invoked on cannot be changed since it is immutable.</returns>
        /// <exception cref="System.ArgumentException">This exception is thrown if the given durations are not present in the optional durations of this item.</exception>
        public SwitchLaneItem SwitchDurations(IDictionary<Lane, int> newDurations)
        {
            IList<IDictionary<Lane, int>> newOptionalDurations = GetNewOptionalDurations(newDurations);
            return GetNewItemWithSwitchedDurations(new Dictionary<Lane, int>(newDurations), newOptionalDurations);
        }

        /// <summary>
        /// Subclasses should overwrite this method and provide a new object of their own class.
        /// </summary>
        /// <param name="newDurations">The new durations of the item.</param>
        /// <param name="newOptionalDurations">The new optional durations of the item.</param>
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
