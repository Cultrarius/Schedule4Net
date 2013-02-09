using System;

namespace Schedule4Net
{
    /// <summary>
    /// This class is used by the scheduling algorithm to represent one executing unit for the scheduled items.
    /// This could, for example, be a processor that executes tasks or a roboter that executes movement-commands.
    /// </summary>
    /// <seealso cref="ItemToSchedule"/>
    [Serializable]
    class Lane
    {
        /// <summary>
        /// Returns the number of this lane. The number is a unique identifier used to distinguish different lanes.
        /// </summary>
        /// <value>
        /// The number of this lane.
        /// </value>
        public int Number { get; private set; }

        /// <summary>
        /// Creates a new lane with the given number.
        /// </summary>
        /// <param name="number">The number the new lane has. This is a unique identifier to distinguish this lane from other lanes.</param>
        public Lane(int number)
        {
            Number = number;
        }

        public override int GetHashCode()
        {
            return Number;
        }

        public override bool Equals(Object obj)
        {
            if (this == obj)
                return true;
            if (obj == null)
                return false;
            Lane other = obj as Lane;
            return other != null && Number == other.Number;
        }

        public override String ToString()
        {
            return "Lane " + Number;
        }
    }
}
