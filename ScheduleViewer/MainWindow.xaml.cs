using System.Collections.Generic;
using Schedule4Net;
using System.Windows;

namespace ScheduleViewer
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            AddSchedule(); //add example schedule
        }

        private void AddSchedule()
        {
            /*
             * This is just an example to demonstrate the ScheduleCanvas
             */
            List<ScheduledItem> fixedItems = new List<ScheduledItem>();
            List<ItemToSchedule> items = new List<ItemToSchedule>();

            IDictionary<Lane, int> durations = new Dictionary<Lane, int>();
            Lane lane0 = new Lane(10);
            Lane lane1 = new Lane(420);
            Lane lane2 = new Lane(22);

            // Test 1
            durations.Clear();
            durations.Add(lane0, 400);
            ItemToSchedule unit1 = new ItemToSchedule(1, durations, new List<ItemToSchedule>());
            items.Add(unit1);

            // Test 11
            durations.Clear();
            durations.Add(lane1, 200);
            ItemToSchedule unit2 = new ItemToSchedule(11, durations, new List<ItemToSchedule>());

            // Test 2 (req Test 11)
            durations.Clear();
            durations.Add(lane1, 200);
            IList<ItemToSchedule> required = new List<ItemToSchedule>();
            required.Add(unit2);
            ItemToSchedule unit3 = new ItemToSchedule(2, durations, required);
            items.Add(unit3);

            items.Add(unit2);

            // Test 22 (req Test 11, Test 2)
            durations.Clear();
            durations.Add(lane2, 200);
            required = new List<ItemToSchedule> { unit2, unit3 };
            items.Add(new ItemToSchedule(22, durations, required));

            Scheduler scheduling = new Scheduler();
            SchedulePlan plan = scheduling.Schedule(items, fixedItems);

            MainCanvas.Initialize(plan.ScheduledItems, scheduling);
        }
    }
}
