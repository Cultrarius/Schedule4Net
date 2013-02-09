﻿using System;
using System.Collections.Generic;

namespace Schedule4Net
{
    internal class ItemToSchedule : IComparable<ItemToSchedule>
    {
        private readonly IDictionary<Lane, int> _durations;
        private readonly List<ItemToSchedule> _requiredItems;
        private readonly ICollection<Lane> _lanes;

        /// <summary>
        /// The result of this property is the same as looping through all affected lanes, retrieving the corresponding duration and then selecting the longest one.
        /// </summary>
        /// <value>
        /// The maximum duration that this item requires on any lane
        /// </value>
        public int MaxDuration { get; private set; }

        /// <summary>
        /// Gets the lanes that this item is active on (has a duration bigger than zero).
        /// </summary>
        public ICollection<Lane> Lanes
        {
            get { return new List<Lane>(_lanes); }
        }

        /// <summary>
        /// The result of this method is the same as looping through all affected lanes, retrieving the corresponding duration and then summarizing them.
        /// </summary>
        /// <value>
        /// The summary of all durations of all affected lanes
        /// </value>
        public int DurationSummary { get; private set; }

        /// <summary>
        /// Returns the id of this item. The id is a unique identifier used to distinguish different items.
        /// </summary>
        /// <value>
        /// The id of the item
        /// </value>
        public int Id { get; private set; }

        /// <summary>
        /// This parameter can be used by some constraints and to create a good start configuration of the plan, but it is not necessarily set.
        /// </summary>
        /// <value>
        /// The items required by this item
        /// </value>
        public List<ItemToSchedule> RequiredItems
        {
            get { return new List<ItemToSchedule>(_requiredItems); }
        }

        public IDictionary<Lane, int> Durations
        {
            get { return new Dictionary<Lane, int>(_durations); }
        }

        /// <summary>
        /// Creates a new item.
        /// </summary>
        /// <param name="id">The unique id of the item.</param>
        /// <param name="durations">The duration that this item requires on each lane.</param>
        /// <param name="requiredItems">The items that are required by this item. This parameter can be used by some constraints and to create a good start configuration of the plan.</param>
        /// <exception cref="System.ArgumentException">
        /// This exception is raised if one of the following is true:
        /// - durations is null or empty
        /// - one of the durations is smaller or equal to 0
        /// </exception>
        public ItemToSchedule(int id, IDictionary<Lane, int> durations, IEnumerable<ItemToSchedule> requiredItems)
        {
            if (durations == null || durations.Count == 0)
            {
                throw new ArgumentException("Every item to schedule must have at least one duration for a lane.");
            }

            Id = id;
            _requiredItems = new List<ItemToSchedule>(requiredItems);
            _durations = new Dictionary<Lane, int>(durations);
            _lanes = new List<Lane>(durations.Keys);

            foreach (var duration in durations.Values)
            {
                if (duration <= 0)
                {
                    throw new ArgumentException("Every item to schedule must have a minimum duration greater 0");
                }
                if (duration > MaxDuration)
                {
                    MaxDuration = duration;
                }
                DurationSummary += duration;
            }
        }

        /// <summary>
        /// Returns the duration this item requires on the given lane. This is faster than accessing the <c>Durations</c> property directly.
        /// </summary>
        /// <param name="lane">The lane the duration should be retrieved for.</param>
        /// <returns>The duration on the lane</returns>
        public int GetDuration(Lane lane)
        {
            return Durations[lane];
        }

        public override int GetHashCode()
        {
            return Id;
        }

        public override bool Equals(Object obj)
        {
            if (this == obj)
                return true;
            if (obj == null)
                return false;
            ItemToSchedule other = obj as ItemToSchedule;
            return other != null && Id == other.Id;
        }

        public int CompareTo(ItemToSchedule other)
        {
            int thisVal = Id;
            int anotherVal = other.Id;
            return (thisVal < anotherVal ? -1 : (thisVal == anotherVal ? 0 : 1));
        }

        public override String ToString()
        {
            return "[id: " + Id + ", durations: " + Durations + ", required: " + RequiredItems + "]";
        }
    }
}
