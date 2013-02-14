using System;
using System.Collections.Generic;
using System.Linq;

namespace Schedule4Net
{
    public class SchedulePlan : ICloneable
    {
        private readonly IDictionary<int, ScheduledItem> _scheduledItems;
        private readonly IDictionary<int, int> _startValues;
        private readonly IDictionary<ItemToSchedule, ICollection<ItemToSchedule>> _dependentItems;
        private readonly ISet<ScheduledItem> _fixedItems;

        public List<ScheduledItem> ScheduledItems
        {
            get { return new List<ScheduledItem>(_scheduledItems.Values); }
        }

        public IDictionary<int, int> StartValues
        {
            get { return new Dictionary<int, int>(_startValues); }
        }

        public int Makespan { get; set; }

        public IDictionary<ItemToSchedule, ICollection<ItemToSchedule>> DependentItems
        {
            get { return new Dictionary<ItemToSchedule, ICollection<ItemToSchedule>>(_dependentItems); }
        }

        public ISet<ScheduledItem> FixedItems
        {
            get { return new HashSet<ScheduledItem>(_fixedItems); }
        }

        public SchedulePlan()
        {
            _scheduledItems = new Dictionary<int, ScheduledItem>();
            _startValues = new Dictionary<int, int>();
            _dependentItems = new Dictionary<ItemToSchedule, ICollection<ItemToSchedule>>();
            _fixedItems = new HashSet<ScheduledItem>();
        }

        private SchedulePlan(IDictionary<int, ScheduledItem> scheduledItems, IDictionary<int, int> startValues,
                IDictionary<ItemToSchedule, ICollection<ItemToSchedule>> dependentItems, IEnumerable<ScheduledItem> fixedItems)
        {
            _scheduledItems = new Dictionary<int, ScheduledItem>(scheduledItems);
            _startValues = new Dictionary<int, int>(startValues);
            _dependentItems = new Dictionary<ItemToSchedule, ICollection<ItemToSchedule>>(dependentItems);
            _fixedItems = new HashSet<ScheduledItem>(fixedItems);

            Makespan = 0;
            foreach (int newEnd in _startValues.Keys.Where(newEnd => newEnd > Makespan))
            {
                Makespan = newEnd;
            }
        }

        public ScheduledItem Add(ItemToSchedule itemToSchedule, int start)
        {
            if (_scheduledItems.ContainsKey(itemToSchedule.Id))
            {
                throw new ArgumentException("The plan already contains this item: " + itemToSchedule);
            }

            foreach (ItemToSchedule required in itemToSchedule.RequiredItems)
            {
                ICollection<ItemToSchedule> items = _dependentItems.ContainsKey(required) ? _dependentItems[required] : new HashSet<ItemToSchedule>();
                items.Add(itemToSchedule);
                _dependentItems.Remove(required);
                _dependentItems.Add(required, items);
            }

            foreach (Lane lane in itemToSchedule.Lanes.Where(lane => start + itemToSchedule.GetDuration(lane) > Makespan))
            {
                Makespan = start + itemToSchedule.GetDuration(lane);
            }

            ScheduledItem scheduledItem = new ScheduledItem(itemToSchedule, start);
            _scheduledItems.Add(scheduledItem.ItemToSchedule.Id, scheduledItem);
            AddToStartValues(scheduledItem);
            return scheduledItem;
        }

        public void FixateItem(ScheduledItem itemToFixate)
        {
            if (!_scheduledItems.Values.Contains(itemToFixate))
            {
                throw new ArgumentException("The plan does not contain this scheduled item (start value error): " + itemToFixate);
            }
            _fixedItems.Add(itemToFixate);
        }

        public bool CanBeMoved(ScheduledItem itemToMove)
        {
            return !_fixedItems.Contains(itemToMove);
        }

        private void AddToStartValues(ScheduledItem itemToAdd)
        {
            int start = itemToAdd.Start;
            int count = _startValues.ContainsKey(start) ? _startValues[start] + 1 : 1;
            _startValues.Remove(start);
            _startValues.Add(start, count);

            foreach (Lane lane in itemToAdd.ItemToSchedule.Lanes)
            {
                int end = itemToAdd.GetEnd(lane);
                count = _startValues.ContainsKey(end) ? _startValues[end] + 1 : 1;
                _startValues.Remove(end);
                _startValues.Add(end, count);
            }
        }

        private void RemoveFromStartValues(ScheduledItem itemToRemove)
        {
            DecreaseStartValue(itemToRemove, itemToRemove.Start);
            foreach (Lane lane in itemToRemove.ItemToSchedule.Lanes)
            {
                DecreaseStartValue(itemToRemove, itemToRemove.GetEnd(lane));
            }
        }

        private void DecreaseStartValue(ScheduledItem item, int startValue)
        {
            if (!_startValues.ContainsKey(startValue))
            {
                throw new ArgumentException("The plan does not contain this scheduled item (start value error): " + item);
            }
            int count = _startValues[startValue];
            if (count == 0)
            {
                throw new ArgumentException("The plan contains 0 entries for this scheduled item (start value error): " + item);
            }
            count--;
            _startValues.Remove(startValue);
            if (count != 0)
            {
                _startValues.Add(startValue, count);
            }
        }

        public SortedSet<int> GetExistingStartValues()
        {
            SortedSet<int> start = new SortedSet<int>(_startValues.Keys) { 0 };
            return start;
        }

        public ScheduledItem MoveScheduledItem(ItemToSchedule itemToMove, int newStart)
        {
            int itemId = itemToMove.Id;
            if (!_scheduledItems.ContainsKey(itemId))
            {
                throw new ArgumentException("The plan does not contain this scheduled item!");
            }
            ScheduledItem oldItem = _scheduledItems[itemId];
            if (_fixedItems.Contains(oldItem))
            {
                throw new ArgumentException("The item " + oldItem + " has been fixated and must not be moved!");
            }
            ScheduledItem newItem = oldItem.ChangeStart(newStart);

            // update start values
            RemoveFromStartValues(oldItem);
            AddToStartValues(newItem);

            _scheduledItems.Remove(itemId);
            _scheduledItems.Add(itemId, newItem);

            UpdateMakespan();

            return newItem;
        }

        public override String ToString()
        {
            return "Scheduling Plan: " + _scheduledItems;
        }

        public void ShiftAll(int shiftValue)
        {
            // TODO: check if the shift leads to negative values
            IDictionary<int, ScheduledItem> newScheduledItems = new Dictionary<int, ScheduledItem>();
            foreach (ScheduledItem oldItem in _scheduledItems.Values)
            {
                if (_fixedItems.Contains(oldItem))
                {
                    newScheduledItems.Add(oldItem.ItemToSchedule.Id, oldItem);
                }
                else
                {
                    ScheduledItem newItem = oldItem.ChangeStart(oldItem.Start + shiftValue);
                    newScheduledItems.Add(newItem.ItemToSchedule.Id, newItem);
                }
            }
            _scheduledItems.Clear();
            foreach (KeyValuePair<int, ScheduledItem> entry in newScheduledItems)
            {
                _scheduledItems.Add(entry);
            }

            IDictionary<int, int> newStartValues = new Dictionary<int, int>();
            foreach (KeyValuePair<int, int> entry in _startValues)
            {
                newStartValues.Add(entry.Key + shiftValue, entry.Value);
            }
            StartValues.Clear();
            foreach (KeyValuePair<int, int> entry in newStartValues)
            {
                _startValues.Remove(entry);
                _startValues.Add(entry);
            }

            UpdateMakespan();
        }

        public C5.IntervalHeap<ScheduledItem> GetDependentItems(ItemToSchedule item)
        {
            C5.IntervalHeap<ScheduledItem> dependent = new C5.IntervalHeap<ScheduledItem>();
            if (_dependentItems.ContainsKey(item))
            {
                IEnumerable<ItemToSchedule> items = _dependentItems[item];
                foreach (ScheduledItem scheduled in _scheduledItems.Values)
                {
                    ItemToSchedule itemToSchedule = scheduled.ItemToSchedule;
                    if (items.Contains(itemToSchedule))
                    {
                        dependent.Add(scheduled);
                    }
                }
            }
            return dependent;
        }

        public ScheduledItem GetScheduledItem(ItemToSchedule item)
        {
            return _scheduledItems.ContainsKey(item.Id) ? _scheduledItems[item.Id] : null;
        }

        public void Unschedule(ScheduledItem scheduledItem)
        {
            if (_fixedItems.Contains(scheduledItem))
            {
                throw new ArgumentException("The item " + scheduledItem + " has been fixated and must not be unscheduled!");
            }

            RemoveFromStartValues(scheduledItem);
            _scheduledItems.Remove(scheduledItem.ItemToSchedule.Id);

            UpdateMakespan();
        }

        private void UpdateMakespan()
        {
            Makespan = 0;
            foreach (int newEnd in _startValues.Keys)
            {
                if (newEnd > Makespan)
                {
                    Makespan = newEnd;
                }
            }
        }

        public void Schedule(ScheduledItem scheduledItem)
        {
            AddToStartValues(scheduledItem);
            _scheduledItems.Add(scheduledItem.ItemToSchedule.Id, scheduledItem);

            UpdateMakespan();
        }

        public object Clone()
        {
            return new SchedulePlan(_scheduledItems, _startValues, _dependentItems, _fixedItems);
        }
    }
}
