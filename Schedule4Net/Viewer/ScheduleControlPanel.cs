using Schedule4Net.Constraint;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Schedule4Net.Viewer
{
    public class ScheduleControlPanel : Grid
    {
        private ScheduleCanvas _scheduleCanvas;
        private ScrollViewer _viewer;
        private SchedulePlan _displayedPlan;
        private Scheduler _originalScheduler;
        private List<ScheduledItem> _currentSchedule;

        private TextBox _snapshotTextBox;
        private Label _snapshotLabel;
        private int _currentSnapshot;
        private bool _updateTextbox;
        private ListBox _constraintList;

        public ScheduleControlPanel()
        {
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            ColumnDefinitions.Add(new ColumnDefinition());
            AddContent();
        }

        private void AddContent()
        {
            var mainStackPanel = CreateMenu();

            var sep = new Separator { Style = (Style)FindResource(ToolBar.SeparatorStyleKey) };
            SetColumn(sep, 1);

            _scheduleCanvas = new ScheduleCanvas();
            _viewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = _scheduleCanvas
            };
            SetColumn(_viewer, 2);

            Children.Add(mainStackPanel);
            Children.Add(sep);
            Children.Add(_viewer);
        }

        private StackPanel CreateMenu()
        {
            var mainStackPanel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(5, 10, 5, 10) };
            SetColumn(mainStackPanel, 0);

            AddDurationSlider(mainStackPanel);
            AddSnapshotSelection(mainStackPanel);
            AddTimeMarkerCheckbox(mainStackPanel);
            AddConstraintDisplay(mainStackPanel);

            return mainStackPanel;
        }

        private void AddConstraintDisplay(StackPanel mainStackPanel)
        {
            _constraintList = new ListBox { IsEnabled = false };
            mainStackPanel.Children.Add(_constraintList);
        }

        private void AddTimeMarkerCheckbox(Panel mainStackPanel)
        {
            var timeMarkerCheckbox = new CheckBox { Content = "Display time markers", Margin = new Thickness(3), IsChecked = true };
            timeMarkerCheckbox.Checked += timeMarkerCheckbox_Checked;
            timeMarkerCheckbox.Unchecked += TimeMarkerCheckboxOnUnchecked;
            mainStackPanel.Children.Add(timeMarkerCheckbox);
        }

        private void TimeMarkerCheckboxOnUnchecked(object sender, RoutedEventArgs e)
        {
            _scheduleCanvas.DisplayTimeMarkers = false;
            DisplaySnapshot(_currentSnapshot);
        }

        private void timeMarkerCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            _scheduleCanvas.DisplayTimeMarkers = true;
            DisplaySnapshot(_currentSnapshot);
        }

        private void AddSnapshotSelection(Panel mainStackPanel)
        {
            _snapshotTextBox = new TextBox
            {
                Text = "0",
                VerticalAlignment = VerticalAlignment.Center,
                Width = 25,
                HorizontalContentAlignment = HorizontalAlignment.Right
            };
            _snapshotTextBox.TextChanged += _snapshotTextBox_TextChanged;
            _snapshotLabel = new Label { Content = "/ 0" };
            var snapLowerButton = new Button { Content = " < " };
            snapLowerButton.Click += snapLowerButton_Click;
            var snapGreaterButton = new Button { Content = " > " };
            snapGreaterButton.Click += snapGreaterButton_Click;

            var snapshotPanel = new DockPanel { LastChildFill = false };
            snapshotPanel.Children.Add(new Label { Content = "Snapshot: " });
            snapshotPanel.Children.Add(_snapshotTextBox);
            snapshotPanel.Children.Add(_snapshotLabel);
            snapshotPanel.Children.Add(snapLowerButton);
            snapshotPanel.Children.Add(snapGreaterButton);

            mainStackPanel.Children.Add(snapshotPanel);
        }

        void _snapshotTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            int result;
            if (!_updateTextbox && int.TryParse(_snapshotTextBox.Text, out result))
            {
                DisplaySnapshot(result - 1);
            }
        }

        void snapGreaterButton_Click(object sender, RoutedEventArgs e)
        {
            DisplaySnapshot(_currentSnapshot + 1);
        }

        void snapLowerButton_Click(object sender, RoutedEventArgs e)
        {
            DisplaySnapshot(_currentSnapshot - 1);
        }

        private void DisplaySnapshot(int number)
        {
            if (number < 0 || _originalScheduler == null) return;
            var snapshots = _originalScheduler.Snapshots;
            if (number >= snapshots.Count) return;
            _currentSnapshot = number;

            _updateTextbox = true;
            _snapshotTextBox.Text = (number + 1).ToString(CultureInfo.InvariantCulture);
            _updateTextbox = false;

            _scheduleCanvas.Initialize(snapshots[number], _originalScheduler);
        }

        private void AddDurationSlider(Panel mainStackPanel)
        {
            var durationScaleSlider = new Slider
            {
                Orientation = Orientation.Horizontal,
                Minimum = -1,
                Maximum = 1,
                Value = 0,
                TickFrequency = 0.1,
                VerticalAlignment = VerticalAlignment.Center
            };
            durationScaleSlider.ValueChanged += DurationScaleSliderOnValueChanged;
            var scalePanel = new DockPanel();
            scalePanel.Children.Add(new Label { Content = "Duration scale: " });
            scalePanel.Children.Add(durationScaleSlider);
            mainStackPanel.Children.Add(scalePanel);
        }

        private void DurationScaleSliderOnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _scheduleCanvas.DurationScale = Math.Pow(10, e.NewValue);
            DisplaySnapshot(_currentSnapshot);
        }

        public void Initialize(SchedulePlan displayedPlan)
        {
            _currentSchedule = displayedPlan.ScheduledItems;
            _originalScheduler = null;
            _snapshotLabel.Content = "/ 1";
            _snapshotTextBox.Text = "1";
            _scheduleCanvas.Initialize(_currentSchedule);
        }

        public void Initialize(SchedulePlan displayedPlan, Scheduler originalScheduler)
        {
            _displayedPlan = displayedPlan;
            _originalScheduler = originalScheduler;
            _currentSchedule = displayedPlan.ScheduledItems;
            _snapshotLabel.Content = "/ " + _originalScheduler.Snapshots.Count;
            UpdateDisplayedConstraints();
            DisplaySnapshot(_originalScheduler.Snapshots.Count - 1);
        }

        private void UpdateDisplayedConstraints()
        {
            _constraintList.Items.Clear();
            foreach (ItemPairConstraint constraint in _originalScheduler.ViolationsManager.PairConstraints)
            {
                Brush backColor = ScheduleCanvas.GetColorForConstraint(constraint).Clone();
                backColor.Opacity = 0.5;
                var item = new ListBoxItem { Content = constraint.GetType().Name, Background = backColor };
                _constraintList.Items.Add(item);
            }
        }
    }
}
