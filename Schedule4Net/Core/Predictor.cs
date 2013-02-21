using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Schedule4Net.Constraint;
using System.Threading.Tasks;

namespace Schedule4Net.Core
{
    internal class Predictor
    {
        private readonly IDictionary<ItemToSchedule, ISet<ViolationsManager.ConstraintPartner>> _constraintMap;
        private readonly IDictionary<ItemToSchedule, PredictionData> _predictionMap;
        private readonly BlockStore _blockStore;

        private SchedulePlan _plan;

        public Predictor(SchedulePlan plan, IDictionary<ItemToSchedule, ISet<ViolationsManager.ConstraintPartner>> constraintMap)
        {
            _plan = plan;
            _constraintMap = constraintMap;
            _predictionMap = new Dictionary<ItemToSchedule, PredictionData>(constraintMap.Count);
            _blockStore = new BlockStore();

            InitializePredictionMap();
        }

        private void InitializePredictionMap()
        {
            Debug.Assert(_constraintMap != null, "constraintMap != null");
            foreach (var entry in _constraintMap)
            {
                IDictionary<ItemToSchedule, PredictionBlocks> predictionBlocks = new Dictionary<ItemToSchedule, PredictionBlocks>(entry.Value.Count);
                foreach (ViolationsManager.ConstraintPartner partner in entry.Value)
                {
                    PredictionBlocks blocks = null;
                    foreach (ItemPairConstraint constraint in partner.Constraints)
                    {
                        ConstraintPrediction decision = constraint.PredictDecision(entry.Key, partner.PartnerItem);
                        PredictionBlocks newBlocks = CreateBlocksFromDecision(entry.Key, partner.PartnerItem, decision);
                        blocks = blocks == null ? newBlocks : blocks.Aggregate(newBlocks, Method.MergeMax);
                    }
                    predictionBlocks.Add(partner.PartnerItem, blocks);
                }
                _predictionMap.Add(entry.Key, new PredictionData(predictionBlocks, this));
            }
        }

        public void ItemWasMoved(ItemToSchedule movedItem)
        {
            foreach (ViolationsManager.ConstraintPartner partner in _constraintMap[movedItem])
            {
                ItemToSchedule partnerItem = partner.PartnerItem;
                _predictionMap[partnerItem].FlaggedDirty.Add(movedItem);
            }
        }

        private PredictionBlocks CreateBlocksFromDecision(ItemToSchedule movedItem, ItemToSchedule fixItem, ConstraintPrediction decision)
        {
            PredictionBlocks blocks = null;

            // the block conflicts when before
            if (ConstraintPrediction.Prediction.Conflict.Equals(decision.ConflictsWhenBefore))
            {
                blocks = GetConflictBeforeBlock(movedItem, decision);
            }
            // it is possible (but unknown) that the block conflicts when before
            else if (ConstraintPrediction.Prediction.Unknown.Equals(decision.ConflictsWhenBefore))
            {
                blocks = GetUnknownBeforeBlock(decision);
            }

            // the block conflicts when starting together (and maybe when overlapping)
            if (ConstraintPrediction.Prediction.Conflict.Equals(decision.ConflictsWhenTogether))
            {
                PredictionBlocks newBlocks = GetConflictDuringBlock(movedItem, fixItem, decision);
                blocks = blocks == null ? newBlocks : blocks.Aggregate(newBlocks, Method.MergeMax);
            }
            // it is possible (but unknown) that the block conflicts when starting together (and maybe when overlapping)
            else if (ConstraintPrediction.Prediction.Unknown.Equals(decision.ConflictsWhenTogether))
            {
                PredictionBlocks newBlocks = GetUnknownDuringBlock(movedItem, fixItem, decision);
                blocks = blocks == null ? newBlocks : blocks.Aggregate(newBlocks, Method.MergeMax);
            }

            // the block conflicts when after
            if (ConstraintPrediction.Prediction.Conflict.Equals(decision.ConflictsWhenAfter))
            {
                PredictionBlocks newBlocks = GetConflictAfterBlock(movedItem, fixItem, decision);
                blocks = blocks == null ? newBlocks : blocks.Aggregate(newBlocks, Method.MergeMax);
            }
            // it is possible (but unknown) that the block conflicts when after
            else if (ConstraintPrediction.Prediction.Unknown.Equals(decision.ConflictsWhenAfter))
            {
                PredictionBlocks newBlocks = GetUnknownAfterBlock(movedItem, fixItem, decision);
                blocks = blocks == null ? newBlocks : blocks.Aggregate(newBlocks, Method.MergeMax);
            }

            if (blocks == null)
            {
                return new PredictionBlocks(_blockStore.GetBeforeBlock(0, 0, -1), new List<MiddleBlock>(),
                        _blockStore.GetAfterBlock(0, 0, 0), _blockStore);
            }
            return blocks;
        }

        private PredictionBlocks GetUnknownAfterBlock(ItemToSchedule movedItem, ItemToSchedule fixItem, ConstraintPrediction decision)
        {
            AfterBlock afterBlock = _blockStore.GetAfterBlock(0, decision.PredictedConflictValue,
                    fixItem.MaxDuration - (movedItem.MaxDuration - 1));
            return new PredictionBlocks(_blockStore.GetBeforeBlock(0, 0, fixItem.MaxDuration - movedItem.MaxDuration),
                    new List<MiddleBlock>(), afterBlock, _blockStore);
        }

        private PredictionBlocks GetConflictAfterBlock(ItemToSchedule movedItem, ItemToSchedule fixItem, ConstraintPrediction decision)
        {
            IList<MiddleBlock> middleBlocks = new List<MiddleBlock>(1);
            if (movedItem.MaxDuration > 1)
            {
                middleBlocks.Add(_blockStore.GetMiddleBlock(0, decision.PredictedConflictValue,
                        fixItem.MaxDuration - (movedItem.MaxDuration - 1), fixItem.MaxDuration - 1));
            }
            AfterBlock afterBlock = _blockStore.GetAfterBlock(decision.PredictedConflictValue, 0, fixItem.MaxDuration);
            return new PredictionBlocks(_blockStore.GetBeforeBlock(0, 0, fixItem.MaxDuration - movedItem.MaxDuration), middleBlocks,
                    afterBlock, _blockStore);
        }

        private PredictionBlocks GetUnknownDuringBlock(ItemToSchedule movedItem, ItemToSchedule fixItem, ConstraintPrediction decision)
        {
            BeforeBlock beforeBlock = _blockStore.GetBeforeBlock(0, 0, -movedItem.MaxDuration);
            IList<MiddleBlock> middleBlocks = new List<MiddleBlock>(1);
            middleBlocks.Add(_blockStore.GetMiddleBlock(0, decision.PredictedConflictValue, -(movedItem.MaxDuration - 1),
                    fixItem.MaxDuration - 1));
            return new PredictionBlocks(beforeBlock, middleBlocks, _blockStore.GetAfterBlock(0, 0, fixItem.MaxDuration), _blockStore);
        }

        private PredictionBlocks GetConflictDuringBlock(ItemToSchedule movedItem, ItemToSchedule fixItem, ConstraintPrediction decision)
        {
            BeforeBlock beforeBlock = _blockStore.GetBeforeBlock(0, 0, -movedItem.MaxDuration);
            IList<MiddleBlock> middleBlocks = new List<MiddleBlock>(3)
            {
                _blockStore.GetMiddleBlock(0, decision.PredictedConflictValue, -(movedItem.MaxDuration - 1), -1),
                _blockStore.GetMiddleBlock(decision.PredictedConflictValue, 0, 0, 0)
            };
            if (fixItem.MaxDuration > 1)
            {
                middleBlocks.Add(_blockStore.GetMiddleBlock(0, decision.PredictedConflictValue, 1, fixItem.MaxDuration - 1));
            }
            return new PredictionBlocks(beforeBlock, middleBlocks, _blockStore.GetAfterBlock(0, 0, fixItem.MaxDuration), _blockStore);
        }

        private PredictionBlocks GetUnknownBeforeBlock(ConstraintPrediction decision)
        {
            return new PredictionBlocks(_blockStore.GetBeforeBlock(0, decision.PredictedConflictValue, -1),
                    new List<MiddleBlock>(), _blockStore.GetAfterBlock(0, 0, 0), _blockStore);
        }

        private PredictionBlocks GetConflictBeforeBlock(ItemToSchedule movedItem, ConstraintPrediction decision)
        {
            BeforeBlock beforeBlock = _blockStore.GetBeforeBlock(decision.PredictedConflictValue, 0, -movedItem.MaxDuration);
            IList<MiddleBlock> middleBlocks = new List<MiddleBlock>(1);
            middleBlocks.Add(_blockStore.GetMiddleBlock(0, decision.PredictedConflictValue, -(movedItem.MaxDuration - 1), -1));
            return new PredictionBlocks(beforeBlock, middleBlocks, _blockStore.GetAfterBlock(0, 0, 0), _blockStore);
        }

        public ConflictPrediction PredictConflicts(ScheduledItem item)
        {
            PredictionData data = _predictionMap[item.ItemToSchedule];
            Block predictedBlock = data.GetBlockForTime(item.Start);
            return new ConflictPrediction(predictedBlock.GetValues());
        }

        internal class ConflictPrediction
        {
            private readonly Block.PredictionValues _values;

            public ConflictPrediction(Block.PredictionValues values)
            {
                _values = values;
            }

            public int GetDefinedHardConflictValue()
            {
                return _values.ConflictValue;
            }

            public int GetPossibleHardConflictValue()
            {
                return _values.UnknownValue;
            }
        }

        private class PredictionData
        {
            internal readonly ISet<ItemToSchedule> FlaggedDirty;
            private readonly IDictionary<ItemToSchedule, PredictionBlocks> _predictionBlocks;
            private PredictionBlocks _aggregated;
            private readonly Predictor _predictor;

            public PredictionData(IDictionary<ItemToSchedule, PredictionBlocks> predictionBlocks, Predictor predictor)
            {
                _predictionBlocks = predictionBlocks;
                _predictor = predictor;
                FlaggedDirty = new HashSet<ItemToSchedule>();
                _aggregated = null;
            }

            public Block GetBlockForTime(int start)
            {
                if (_aggregated == null)
                {
                    CreateAggregationBlock();
                }
                else if (FlaggedDirty.Count != 0)
                {
                    if (FlaggedDirty.Count < (_predictionBlocks.Count / 2))
                    {
                        UpdateAggregationBlock();
                    }
                    else
                    {
                        CreateAggregationBlock();
                    }
                }
                Debug.Assert(_aggregated != null, "aggregated != null");
                return _aggregated.GetBlockForTime(start);
            }

            private void UpdateAggregationBlock()
            {
                // subtract the old dirty flagged blocks
                var blocksToAggregate = new List<PredictionBlocks>(FlaggedDirty.Count);
                blocksToAggregate.AddRange(FlaggedDirty.Select(item => _predictionBlocks[item]));
                _aggregated = _aggregated.Aggregate(blocksToAggregate, Method.Subtract);

                // update them and then add them to the aggregate again
                blocksToAggregate.Clear();
                foreach (ItemToSchedule item in FlaggedDirty)
                {
                    Debug.Assert(_predictor._plan != null, "plan != null");
                    int itemStart = _predictor._plan.GetScheduledItem(item).Start;
                    PredictionBlocks itemBlocks = _predictionBlocks[item];
                    itemBlocks.SetStartPosition(itemStart);
                    blocksToAggregate.Add(itemBlocks);
                }
                _aggregated = _aggregated.Aggregate(blocksToAggregate, Method.Add);
                FlaggedDirty.Clear();
            }

            private void CreateAggregationBlock()
            {
                FlaggedDirty.Clear();
                Debug.Assert(_predictor._blockStore != null, "blockStore != null");
                _aggregated = new PredictionBlocks(_predictor._blockStore.GetBeforeBlock(0, 0, -1), new List<MiddleBlock>(),
                        _predictor._blockStore.GetAfterBlock(0, 0, 0), 0, _predictor._blockStore);
                IList<PredictionBlocks> blocksToAggregate = new List<PredictionBlocks>();
                foreach (var entry in _predictionBlocks)
                {
                    int itemStart = _predictor._plan.GetScheduledItem(entry.Key).Start;
                    PredictionBlocks itemBlocks = entry.Value;
                    itemBlocks.SetStartPosition(itemStart);
                    blocksToAggregate.Add(itemBlocks);
                }
                _aggregated = _aggregated.Aggregate(blocksToAggregate, Method.Add);
            }
        }

        internal class PredictionBlocks
        {
            private int _startPosition;
            private readonly BeforeBlock _beforeBlock;
            private readonly List<MiddleBlock> _middleBlocks;
            private readonly AfterBlock _afterBlock;
            private readonly BlockStore _blockStore;

            public PredictionBlocks(BeforeBlock beforeBlock, IEnumerable<MiddleBlock> middleBlocks, AfterBlock afterBlock, BlockStore blockStore)
                : this(beforeBlock, middleBlocks, afterBlock, 0, blockStore)
            {
            }

            public PredictionBlocks(BeforeBlock beforeBlock, IEnumerable<MiddleBlock> middleBlocks, AfterBlock afterBlock, int startPosition, BlockStore blockStore)
            {
                _beforeBlock = beforeBlock;
                _middleBlocks = new List<MiddleBlock>(middleBlocks);
                _afterBlock = afterBlock;
                _blockStore = blockStore;

                SetStartPosition(startPosition);
            }

            internal PredictionBlocks Aggregate(PredictionBlocks blockToAggregate, Method method)
            {
                return Aggregate(new List<PredictionBlocks> { blockToAggregate }, method);
            }

            internal PredictionBlocks Aggregate(IList<PredictionBlocks> blocksToAggregate, Method method)
            {

                // create a set of the times used
                var startTimes = new C5.TreeSet<int>();
                var endTimes = new C5.TreeSet<int>();

                AddTimes(startTimes, endTimes);
                foreach (PredictionBlocks blocks in blocksToAggregate)
                {
                    blocks.AddTimes(startTimes, endTimes);
                }

                var itS = startTimes.GetEnumerator();
                itS.MoveNext();
                var itE = endTimes.GetEnumerator();
                itE.MoveNext();
                IList<Block> oldBlocks = new List<Block>(blocksToAggregate.Count + 1);

                // create the new before block
                int time = itE.Current;
                foreach (PredictionBlocks blocks in blocksToAggregate)
                {
                    oldBlocks.Add(blocks._beforeBlock);
                }
                BeforeBlock newBeforeBlock = _blockStore.GetBeforeBlock(GetNewConflictValue(_beforeBlock, oldBlocks, method),
                        GetNewUnknownValue(_beforeBlock, oldBlocks, method), time);

                IList<MiddleBlock> newMiddleBlocks = new List<MiddleBlock>(startTimes.Count);
                AfterBlock newAfterBlock;
                IList<MiddleBlockTask> tasks = new List<MiddleBlockTask>();
                while (true)
                {
                    time = itS.Current;
                    if (!itS.MoveNext())
                    {
                        if (itE.MoveNext())
                        {
                            throw new ApplicationException("There is just the start time foreach the last block, but still more end times left.");
                        }
                        // create the after block
                        oldBlocks.Clear();
                        foreach (PredictionBlocks blocks in blocksToAggregate)
                        {
                            oldBlocks.Add(blocks._afterBlock);
                        }
                        newAfterBlock = _blockStore.GetAfterBlock(GetNewConflictValue(_afterBlock, oldBlocks, method),
                                GetNewUnknownValue(_afterBlock, oldBlocks, method), time);
                        break;
                    }
                    // create another middle block
                    oldBlocks.Clear();
                    itE.MoveNext();
                    tasks.Add(new MiddleBlockTask(time, itE.Current));
                }
                Parallel.ForEach(tasks, task =>
                    {
                        IList<Block> prevBlocks = blocksToAggregate.Select(blocks => blocks.GetBlockForTime(task.Time)).ToList();
                        Block referenceBlock = GetBlockForTime(task.Time);

                        task.Result = _blockStore.GetMiddleBlock(GetNewConflictValue(referenceBlock, prevBlocks, method),
                                GetNewUnknownValue(referenceBlock, prevBlocks, method), task.Time, task.EndTime);
                    });

                // TODO: merge adjacent equal blocks with equal value ?
                foreach (MiddleBlockTask middleBlockTask in tasks)
                {
                    newMiddleBlocks.Add(middleBlockTask.Result);
                }

                return new PredictionBlocks(newBeforeBlock, newMiddleBlocks, newAfterBlock, _startPosition, _blockStore);
            }

            private class MiddleBlockTask
            {
                internal readonly int Time;
                internal readonly int EndTime;
                internal MiddleBlock Result;

                public MiddleBlockTask(int time, int endTime)
                {
                    Time = time;
                    EndTime = endTime;
                }
            }

            private static int GetNewUnknownValue(Block reference, IEnumerable<Block> oldBlocks, Method method)
            {
                int newValue = reference.GetValues().UnknownValue;
                if (Method.Add.Equals(method))
                {
                    newValue += oldBlocks.Sum(block => block.GetValues().UnknownValue);
                }
                else if (Method.MergeMax.Equals(method))
                {
                    foreach (Block block in oldBlocks)
                    {
                        if (newValue < block.GetValues().UnknownValue)
                        {
                            newValue = block.GetValues().UnknownValue;
                        }
                    }
                }
                else if (Method.Subtract.Equals(method))
                {
                    newValue -= oldBlocks.Sum(block => block.GetValues().UnknownValue);
                }
                return newValue;
            }

            private static int GetNewConflictValue(Block referenceBlock, IEnumerable<Block> oldBlocks, Method method)
            {
                int newValue = referenceBlock.GetValues().ConflictValue;
                if (Method.Add.Equals(method))
                {
                    newValue += oldBlocks.Sum(block => block.GetValues().ConflictValue);
                }
                else if (Method.MergeMax.Equals(method))
                {
                    foreach (Block block in oldBlocks)
                    {
                        if (newValue < block.GetValues().ConflictValue)
                        {
                            newValue = block.GetValues().ConflictValue;
                        }
                    }
                }
                else if (Method.Subtract.Equals(method))
                {
                    newValue -= oldBlocks.Sum(block => block.GetValues().ConflictValue);
                }
                return newValue;
            }

            public Block GetBlockForTime(int time)
            {
                time -= _startPosition;
                if (_beforeBlock.ContainsPoint(time))
                {
                    return _beforeBlock;
                }
                if (_afterBlock.ContainsPoint(time))
                {
                    return _afterBlock;
                }
                // binary search for the middle block
                int low = 0;
                int high = _middleBlocks.Count - 1;

                while (low <= high)
                {
                    int mid = (low + high) / 2;
                    MiddleBlock midVal = _middleBlocks[mid];
                    if (time < (midVal.Start))
                    {
                        high = mid - 1;
                    }
                    else if (time > (midVal.End))
                    {
                        low = mid + 1;
                    }
                    else
                    {
                        return midVal;
                    }
                }
                throw new ApplicationException("This code segment should never be reached. For every time t a Block should be found!");
            }

            private void AddTimes(C5.TreeSet<int> startTimes, C5.TreeSet<int> endTimes)
            {
                endTimes.Add(_beforeBlock.End + _startPosition);
                startTimes.Add(_afterBlock.Start + _startPosition);
                foreach (MiddleBlock middleBlock in _middleBlocks)
                {
                    startTimes.Add(middleBlock.Start + _startPosition);
                    endTimes.Add(middleBlock.End + _startPosition);
                }
            }

            public void SetStartPosition(int newStartPosition)
            {
                _startPosition = newStartPosition;
            }
        }

        internal enum Method
        {
            Add, MergeMax, Subtract
        }

        internal abstract class Block
        {
            private readonly PredictionValues _values;

            protected Block(int conflictValue, int unknownValue)
            {
                _values = new PredictionValues(conflictValue, unknownValue);
            }

            public abstract bool ContainsPoint(int point);

            public PredictionValues GetValues()
            {
                return _values;
            }

            public class PredictionValues
            {
                internal int ConflictValue;
                internal int UnknownValue;

                public PredictionValues(int conflictValue, int unknownValue)
                {
                    if (conflictValue < 0 || unknownValue < 0)
                    {
                        throw new ArgumentException();
                    }
                    ConflictValue = conflictValue;
                    UnknownValue = unknownValue;
                }
            }
        }

        internal class BeforeBlock : Block
        {
            internal int End;

            public BeforeBlock(int conflictValue, int unknownValue, int end)
                : base(conflictValue, unknownValue)
            {
                End = end;
            }

            public override bool ContainsPoint(int point)
            {
                return point <= End;
            }
        }

        internal class AfterBlock : Block
        {
            internal int Start;

            public AfterBlock(int conflictValue, int unknownValue, int start)
                : base(conflictValue, unknownValue)
            {
                Start = start;
            }

            public override bool ContainsPoint(int point)
            {
                return point >= Start;
            }
        }

        internal class MiddleBlock : Block
        {
            internal int Start;
            internal int End;

            public MiddleBlock(int conflictValue, int unknownValue, int start, int end)
                : base(conflictValue, unknownValue)
            {
                Start = start;
                End = end;
            }

            public override bool ContainsPoint(int point)
            {
                return point <= End && point >= Start;
            }
        }

        internal class BlockStore
        {

            private readonly IDictionary<int, ConcurrentBag<MiddleBlock>> _middleBlockStore;
            private readonly IDictionary<int, ConcurrentBag<BeforeBlock>> _beforeBlockStore;
            private readonly IDictionary<int, ConcurrentBag<AfterBlock>> _afterBlockStore;

            public BlockStore()
            {
                _middleBlockStore = new ConcurrentDictionary<int, ConcurrentBag<MiddleBlock>>(8, 50000);
                _beforeBlockStore = new ConcurrentDictionary<int, ConcurrentBag<BeforeBlock>>(8, 25000);
                _afterBlockStore = new ConcurrentDictionary<int, ConcurrentBag<AfterBlock>>(8, 25000);
            }

            public MiddleBlock GetMiddleBlock(int conflictValue, int unknownValue, int start, int end)
            {
                int key = ((conflictValue * 2 + unknownValue) * 2 + start) * 2 + end;
                ConcurrentBag<MiddleBlock> blockList = _middleBlockStore.ContainsKey(key) ? _middleBlockStore[key] : null;
                if (blockList == null)
                {
                    blockList = new ConcurrentBag<MiddleBlock>();
                    try
                    {
                        _middleBlockStore.Add(key, blockList);
                    }
                    catch (ArgumentException)
                    {
                        // some other thread was faster
                        blockList = _middleBlockStore[key];
                    }
                }
                foreach (MiddleBlock middleBlock in blockList)
                {
                    Block.PredictionValues values = middleBlock.GetValues();
                    if (middleBlock.Start == start && middleBlock.End == end && values.ConflictValue == conflictValue
                            && values.UnknownValue == unknownValue)
                    {
                        return middleBlock;
                    }
                }
                var newBlock = new MiddleBlock(conflictValue, unknownValue, start, end);
                blockList.Add(newBlock);
                return newBlock;
            }

            public BeforeBlock GetBeforeBlock(int conflictValue, int unknownValue, int end)
            {
                int key = (conflictValue * 2 + unknownValue) * 2 + end;
                ConcurrentBag<BeforeBlock> blockList = _beforeBlockStore.ContainsKey(key) ? _beforeBlockStore[key] : null;
                if (blockList == null)
                {
                    blockList = new ConcurrentBag<BeforeBlock>();
                    try
                    {
                        _beforeBlockStore.Add(key, blockList);
                    }
                    catch (ArgumentException)
                    {
                        // some other thread was faster
                        blockList = _beforeBlockStore[key];
                    }
                }
                foreach (BeforeBlock beforeBlock in blockList)
                {
                    Block.PredictionValues values = beforeBlock.GetValues();
                    if (beforeBlock.End == end && values.ConflictValue == conflictValue && values.UnknownValue == unknownValue)
                    {
                        return beforeBlock;
                    }
                }
                var newBlock = new BeforeBlock(conflictValue, unknownValue, end);
                blockList.Add(newBlock);
                return newBlock;
            }

            public AfterBlock GetAfterBlock(int conflictValue, int unknownValue, int start)
            {
                int key = (conflictValue * 2 + unknownValue) * 2 + start;
                ConcurrentBag<AfterBlock> blockList = _afterBlockStore.ContainsKey(key) ? _afterBlockStore[key] : null;
                if (blockList == null)
                {
                    blockList = new ConcurrentBag<AfterBlock>();
                    try
                    {
                        _afterBlockStore.Add(key, blockList);
                    }
                    catch (ArgumentException)
                    {
                        // some other thread was faster
                        blockList = _afterBlockStore[key];
                    }
                }
                foreach (AfterBlock afterBlock in blockList)
                {
                    Block.PredictionValues values = afterBlock.GetValues();
                    if (afterBlock.Start == start && values.ConflictValue == conflictValue && values.UnknownValue == unknownValue)
                    {
                        return afterBlock;
                    }
                }
                var newBlock = new AfterBlock(conflictValue, unknownValue, start);
                blockList.Add(newBlock);
                return newBlock;
            }
        }

        public void PlanHasBeenUpdated(SchedulePlan oldPlan, SchedulePlan newPlan)
        {
            //TODO: improve the update
            _plan = newPlan;
            foreach (ScheduledItem item in newPlan.ScheduledItems)
            {
                ItemWasMoved(item.ItemToSchedule);
            }
        }
    }
}
