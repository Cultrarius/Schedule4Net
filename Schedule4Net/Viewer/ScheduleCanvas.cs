using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Schedule4Net.Core;

namespace Schedule4Net.Viewer
{
    public class ScheduleCanvas : Canvas
    {
        public double DurationScale = 0.5;

        private SortedSet<Lane> _lanes;
        private List<ScheduledItem> _items;
        private IDictionary<ItemToSchedule, ISet<ViolationsManager.ConstraintPartner>> _constraintMap;
        private IDictionary<Rectangle, ScheduledItem> _itemTable;
        private IDictionary<ItemToSchedule, Rectangle> _rectangleTable; 

        public void Initialize(List<ScheduledItem> items)
        {
            Initialize(items, null);
        }

        public void Initialize(List<ScheduledItem> items, Scheduler originalScheduler)
        {
            _itemTable = new Dictionary<Rectangle, ScheduledItem>();
            _rectangleTable = new Dictionary<ItemToSchedule, Rectangle>();
            _constraintMap = originalScheduler == null ? null : originalScheduler.ViolationsManager.ConstraintMap;
            Width = 1000;
            Height = 1000;
            _items = items;
            FindLanes();
            PaintLanes();
            PaintItems();
        }

        private void PaintItems()
        {
            int offset = 0;
            foreach (Lane lane in _lanes)
            {
                foreach (ScheduledItem scheduledItem in _items)
                {
                    if (!scheduledItem.Ends.ContainsKey(lane)) continue;
                    PaintText(scheduledItem, offset);
                    PaintRectangle(scheduledItem, lane, offset);
                }
                offset += 50;
            }
        }

        private void PaintText(ScheduledItem scheduledItem, int offset)
        {
            TextBlock text = new TextBlock {Text = "Id: " + scheduledItem.ItemToSchedule.Id, FontSize = 11};
            TextOptions.SetTextFormattingMode(text, TextFormattingMode.Display);
            Children.Add(text);
            SetLeft(text, 7 + 25 + scheduledItem.Start*DurationScale);
            SetTop(text, 2 + 15 + offset);
        }

        private void PaintRectangle(ScheduledItem scheduledItem, Lane lane, int offset)
        {
            int end = scheduledItem.Ends[lane];
            Rectangle r = new Rectangle
                {
                    Height = 46,
                    Width = (end - scheduledItem.Start)*DurationScale,
                    Opacity = 0.4,
                    Stroke = Brushes.Black,
                    Fill = Brushes.LightSteelBlue,
                    RadiusX = 10,
                    RadiusY = 10,
                    SnapsToDevicePixels = true
                };
            _itemTable.Add(r, scheduledItem);
            _rectangleTable.Add(scheduledItem.ItemToSchedule, r);
            r.MouseEnter += r_MouseEnter;
            r.MouseLeave += r_MouseLeave;
            Children.Add(r);
            SetLeft(r, 2 + 25 + scheduledItem.Start*DurationScale);
            SetTop(r, offset + 2);
        }

        private void PaintLanes()
        {
            int offset = 0;
            foreach (Lane lane in _lanes)
            {
                AddLabelText(lane, offset);

                Line verticalLine = new Line { X1 = 20, X2 = 20, Y1 = offset, Y2 = offset + 50, Stroke = Brushes.Black, StrokeThickness = 1, SnapsToDevicePixels = true };
                Children.Add(verticalLine);

                offset += 50;

                Line horizontalLine = new Line { X1 = 0, X2 = 10000, Y1 = offset, Y2 = offset, Stroke = Brushes.Black, StrokeThickness = 1, SnapsToDevicePixels = true };
                Children.Add(horizontalLine);
            }
        }

        private void AddLabelText(Lane lane, int offset)
        {
            TextBlock text = new TextBlock { Text = lane.ToString(), LayoutTransform = new RotateTransform(270), FontSize = 11 };
            TextOptions.SetTextFormattingMode(text, TextFormattingMode.Display);
            Children.Add(text);
            SetTop(text, offset + 3);
        }

        private void FindLanes()
        {
            _lanes = new SortedSet<Lane>(new ByLaneNumber());
            foreach (ScheduledItem item in _items)
            {
                _lanes.UnionWith(item.ItemToSchedule.Lanes);
            }
        }

        private class ByLaneNumber : IComparer<Lane>
        {
            public int Compare(Lane x, Lane y)
            {
                return x.Number < y.Number ? -1 : (x.Number == y.Number ? 0 : 1);
            }
        }

        private void r_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_constraintMap == null) return;
            Rectangle r = sender as Rectangle;
            if (r == null) return;
            foreach (Rectangle rectangle in _itemTable.Keys)
            {
                rectangle.Fill = Brushes.LightSteelBlue;
            }
        }

        private void r_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_constraintMap == null) return;
            Rectangle r = sender as Rectangle;
            if (r != null) r.Fill = Brushes.Chartreuse;

            ScheduledItem scheduled = _itemTable[r];
            foreach (ViolationsManager.ConstraintPartner partner in _constraintMap[scheduled.ItemToSchedule])
            {
                Rectangle rect = _rectangleTable[partner.PartnerItem];
                rect.Fill = Brushes.OrangeRed;
            }
        }
    }
}
