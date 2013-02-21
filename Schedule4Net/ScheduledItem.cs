using System;
using System.Collections.Generic;

namespace Schedule4Net
{
    /// <summary>
    /// This class represents an <see cref="ItemToSchedule"/> that was scheduled to start at a specific point of time.
    /// </summary>
    /// <remarks>Objects of this class are immutable and can therefore not be modified after creation.</remarks>
    [Serializable]
    public class ScheduledItem : IComparable<ScheduledItem>
    {
        /// <summary>
        /// Gets the start time of the scheduled item
        /// </summary>
        public int Start { get; private set; }
        public ItemToSchedule ItemToSchedule { get; private set; }
        private readonly IDictionary<Lane, int> _ends;
        public IDictionary<Lane, int> Ends
        {
            get { return new Dictionary<Lane, int>(_ends); }
        }

        /// <summary>
        /// Creates a new scheduled item with the specified start time
        /// </summary>
        /// <param name="itemToSchedule">The item that should be scheduled.</param>
        /// <param name="start">The start time of the new scheduled item.</param>
        public ScheduledItem(ItemToSchedule itemToSchedule, int start)
        {
            ItemToSchedule = itemToSchedule;
            Start = start;
            _ends = new Dictionary<Lane, int>();
            foreach (Lane lane in itemToSchedule.Lanes)
            {
                _ends.Add(lane, start + itemToSchedule.GetDuration(lane));
            }
        }

        /// <summary>
        /// Creates a new scheduled item with a start time of 0.
        /// </summary>
        /// <param name="itemToSchedule">The item that should be scheduled.</param>
        public ScheduledItem(ItemToSchedule itemToSchedule)
            : this(itemToSchedule, 0)
        {
        }

        /// <summary>
        /// Creates a new scheduled item from this one with a new start value.
        /// This method returns a new object beacuse instances of this class are immutable.
        /// </summary>
        /// <param name="newStart">The new start time of the scheduled item.</param>
        /// <returns>A new scheduled item like this one, but with a new start value</returns>
        internal ScheduledItem ChangeStart(int newStart)
        {
            return new ScheduledItem(ItemToSchedule, newStart);
        }

        /**
         * Returns the end time of this scheduled item for a given lane. This is the same as adding the duration of the
         * ItemToSchedule to the start time of the scheduled item.
         * 
         * @param lane the lane the end value should be retireved for
         * @return the end value of this scheduled item
         */
        /// <summary>
        /// Returns the end time of this scheduled item for a given lane.
        /// This is the same as adding the duration of the ItemToSchedule to the start time of the scheduled item.
        /// This is faster than accessing the <c>Ends</c> property directly.
        /// </summary>
        /// <param name="lane">The lane the end value should be retrieved for.</param>
        /// <returns>The end value of this scheduled item</returns>
        public int GetEnd(Lane lane)
        {
            return _ends[lane];
        }

        public override int GetHashCode()
        {
            return ItemToSchedule.Id;
        }

        public override bool Equals(Object obj)
        {
            if (this == obj)
                return true;
            if (obj == null)
                return false;
            ScheduledItem other = obj as ScheduledItem;
            return other != null && ItemToSchedule.Id == other.ItemToSchedule.Id;
        }

        public override String ToString()
        {
            return "[Start: " + Start + ", Item: " + ItemToSchedule + "]";
        }

        public int CompareTo(ScheduledItem other)
        {
            int result = (Start < other.Start ? -1 : (Start == other.Start ? 0 : 1));
            if (result == 0)
            {
                result = (ItemToSchedule.Id < other.ItemToSchedule.Id ? -1 : (ItemToSchedule.Id == other.ItemToSchedule.Id ? 0 : 1));
            }
            return result;
        }
    }
}
