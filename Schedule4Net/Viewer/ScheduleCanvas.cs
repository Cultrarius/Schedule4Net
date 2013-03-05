﻿using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Schedule4Net.Core;
using System;

namespace Schedule4Net.Viewer
{
    public class ScheduleCanvas : Canvas
    {
        public double DurationScale = 1.0;
        public bool DisplayTimeMarkers = true;
        private const double MinTimeMarkerDistance = 20;
        private const double TopMargin = 15;
        private const double LeftMargin = 5;

        private SortedSet<Lane> _lanes;
        private SortedSet<int> _times;
        private IList<ScheduledItem> _items;
        private IDictionary<ItemToSchedule, ISet<ViolationsManager.ConstraintPartner>> _constraintMap;
        private IDictionary<Rectangle, ScheduledItem> _itemTable;
        private IDictionary<ItemToSchedule, IList<Rectangle>> _rectangleTable;

        public ScheduleCanvas()
        {
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Top;
            Width = 50;
        }

        public void Initialize(List<ScheduledItem> items)
        {
            Initialize(items, null);
        }

        public void Initialize(IList<ScheduledItem> items, Scheduler originalScheduler)
        {
            _itemTable = new Dictionary<Rectangle, ScheduledItem>();
            _rectangleTable = new Dictionary<ItemToSchedule, IList<Rectangle>>();
            _constraintMap = originalScheduler == null ? null : originalScheduler.ViolationsManager.ConstraintMap;
            _items = items;
            Children.Clear();
            Width = 50;

            FindLanes();
            PaintLanes();
            if (DisplayTimeMarkers)
            {
                FindTimeMarkers();
                PaintTimeMarkers();
                PaintTimeLabels();
            }
            PaintItems();

            Height = _lanes.Count * 50 + 50 + TopMargin;
        }

        private void PaintTimeLabels()
        {
            foreach (int time in _times)
            {
                // text on top
                TextBlock textTop = new TextBlock { Text = time.ToString(CultureInfo.InvariantCulture), FontSize = 11, Foreground = Brushes.Silver};
                TextOptions.SetTextFormattingMode(textTop, TextFormattingMode.Display);
                Children.Add(textTop);
                SetLeft(textTop, 2 + 25 + time * DurationScale + LeftMargin);
                SetTop(textTop, 0);

                //text on bottom
                TextBlock textBottom = new TextBlock { Text = time.ToString(CultureInfo.InvariantCulture), FontSize = 11, Foreground = Brushes.Silver };
                TextOptions.SetTextFormattingMode(textBottom, TextFormattingMode.Display);
                Children.Add(textBottom);
                SetLeft(textBottom, 2 + 25 + time * DurationScale + LeftMargin);
                SetTop(textBottom, _lanes.Count * 50 + 10 + TopMargin);
            }
        }

        private void PaintTimeMarkers()
        {
            foreach (int time in _times)
            {
                Line verticalLine = new Line
                {
                    X1 = 2 + 25 + time * DurationScale + LeftMargin,
                    X2 = 2 + 25 + time * DurationScale + LeftMargin,
                    Y1 = TopMargin,
                    Y2 = _lanes.Count * 50 + 10 + TopMargin,
                    Stroke = Brushes.Silver,
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 3, 2 },
                    StrokeDashCap = PenLineCap.Round
                };
                Children.Add(verticalLine);
            }
        }

        private void FindTimeMarkers()
        {
            SortedSet<int> tempTimes = new SortedSet<int>();
            foreach (ScheduledItem scheduledItem in _items)
            {
                tempTimes.UnionWith(scheduledItem.Ends.Values);
                tempTimes.Add(scheduledItem.Start);
            }

            _times = new SortedSet<int> { 0 };
            int lastValue = 0;
            foreach (int time in tempTimes)
            {
                if (!(time - lastValue > MinTimeMarkerDistance / DurationScale)) continue;
                _times.Add(time);
                lastValue = time;
            }
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
            TextBlock text = new TextBlock { Text = "Id: " + scheduledItem.ItemToSchedule.Id, FontSize = 11 };
            TextOptions.SetTextFormattingMode(text, TextFormattingMode.Display);
            Children.Add(text);
            SetLeft(text, 7 + 25 + scheduledItem.Start * DurationScale + LeftMargin);
            SetTop(text, 2 + 15 + offset + TopMargin);
        }

        private void PaintRectangle(ScheduledItem scheduledItem, Lane lane, int offset)
        {
            int end = scheduledItem.Ends[lane];
            Rectangle r = new Rectangle
                {
                    Height = 46,
                    Width = (end - scheduledItem.Start) * DurationScale,
                    Opacity = 0.4,
                    Stroke = Brushes.Black,
                    Fill = Brushes.LightSteelBlue,
                    RadiusX = 10,
                    RadiusY = 10,
                    SnapsToDevicePixels = true
                };
            _itemTable.Add(r, scheduledItem);
            if (_rectangleTable.ContainsKey(scheduledItem.ItemToSchedule))
            {
                _rectangleTable[scheduledItem.ItemToSchedule].Add(r);
            }
            else
            {
                _rectangleTable.Add(scheduledItem.ItemToSchedule, new List<Rectangle> {r});
            }
            
            r.MouseEnter += r_MouseEnter;
            r.MouseLeave += r_MouseLeave;
            Children.Add(r);
            SetLeft(r, 2 + 25 + scheduledItem.Start * DurationScale + LeftMargin);
            SetTop(r, offset + 2 + TopMargin);

            Width = Math.Max(Width,
                             50 + (2 + 25 + scheduledItem.Start * DurationScale + LeftMargin) +
                             ((end - scheduledItem.Start) * DurationScale));
        }

        private void PaintLanes()
        {
            int offset = 0;
            foreach (Lane lane in _lanes)
            {
                AddLabelText(lane, offset);
                offset += 50;

                Line horizontalLine = new Line
                {
                    X1 = 0,
                    X2 = 10000,
                    Y1 = offset + TopMargin,
                    Y2 = offset + TopMargin,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                    SnapsToDevicePixels = true
                };
                Children.Add(horizontalLine);
            }

            Line verticalLine = new Line
            {
                X1 = 20 + LeftMargin,
                X2 = 20 + LeftMargin,
                Y1 = 0,
                Y2 = offset + TopMargin,
                Stroke = Brushes.Black,
                StrokeThickness = 1,
                SnapsToDevicePixels = true
            };
            Children.Add(verticalLine);
        }

        private void AddLabelText(Lane lane, int offset)
        {
            TextBlock text = new TextBlock { Text = lane.ToString(), LayoutTransform = new RotateTransform(270), FontSize = 11 };
            TextOptions.SetTextFormattingMode(text, TextFormattingMode.Display);
            Children.Add(text);
            SetTop(text, offset + 3 + TopMargin);
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
            if (r == null) return;
            ItemToSchedule item = _itemTable[r].ItemToSchedule;
            foreach (Rectangle rect in _rectangleTable[item])
            {
                rect.Fill = Brushes.Chartreuse;
            }
            
            foreach (ViolationsManager.ConstraintPartner partner in _constraintMap[item])
            {
                foreach (Rectangle rect in _rectangleTable[partner.PartnerItem])
                {
                    rect.Fill = Brushes.OrangeRed;
                }
                
            }
        }
    }
}
