using System;

namespace Schedule4Net
{
    /// <summary>
    /// This utility-class provides some static methods that are used by various parts of the scheduling algorithm.
    /// </summary>
    class ScheduleUtil
    {
        private ScheduleUtil() { }

        /// <summary>
        /// Returns for two given items the value that states how much they overlap each other.
        /// </summary>
        /// <param name="startItem1">The start of the first item.</param>
        /// <param name="endItem1">The end of the first item.</param>
        /// <param name="startItem2">The start of the second item.</param>
        /// <param name="endItem2">The end of the second item.</param>
        /// <returns>The overlapping value of the two items</returns>
        /// <example>If, for example, there are the following two items:
        /// item1: start = 0, end = 10
        /// item2: start = 6, end = 20
        /// Then they overlap each other in the range from 6 to 10 and therefore have an overlapping value of 4.</example>
        public static int GetOverlappingValue(int startItem1, int endItem1, int startItem2, int endItem2)
        {
            if (startItem1 == startItem2)
            {
                // item1 starts together with item2
                return Math.Min(endItem1 - startItem1, endItem2 - startItem2);
            }
            if (startItem1 < startItem2 && endItem1 > startItem2)
            {
                // item2 starts in the middle of item1
                return Math.Min(endItem1, endItem2) - startItem2;
            }
            if (startItem1 > startItem2 && endItem2 > startItem1)
            {
                // item1 starts in the middle of item2
                return Math.Min(endItem1, endItem2) - startItem1;
            }
            return 0;
        }

        public static int GetMinimumDistanceToEnd(ScheduledItem item1, ScheduledItem item2)
        {
            return item2.Start - (item1.Start + item1.ItemToSchedule.MaxDuration);
        }
    }
}
