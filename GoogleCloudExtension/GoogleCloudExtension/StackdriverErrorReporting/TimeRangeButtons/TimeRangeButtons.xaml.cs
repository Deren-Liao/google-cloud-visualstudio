﻿// Copyright 2017 Google Inc. All Rights Reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using TimeRangeEnum = Google.Apis.Clouderrorreporting.v1beta1.ProjectsResource.GroupStatsResource.ListRequest.TimeRangePeriodEnum;
using EventTimeRangeEnum = Google.Apis.Clouderrorreporting.v1beta1.ProjectsResource.EventsResource.ListRequest.TimeRangePeriodEnum;
using static GoogleCloudExtension.Resources;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace GoogleCloudExtension.StackdriverErrorReporting
{
    /// <summary>
    /// Interaction logic for TimeRangeButtons.xaml
    /// </summary>
    public partial class TimeRangeButtons : ItemsControl
    {
        private readonly ObservableCollection<TimeRangeItem> _timeRangeItems;

        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register(
                nameof(SelectedItem),
                typeof(TimeRangeItem),
                typeof(TimeRangeButtons),
                new FrameworkPropertyMetadata(null, OnTimePartPropertyChanged, null));

        //public static readonly DependencyProperty AllItemsProperty =
        //    DependencyProperty.Register(
        //        nameof(AllItems),
        //        typeof(ObservableCollection<TimeRangeItem>),
        //        typeof(TimeRangeButtons),
        //        new FrameworkPropertyMetadata());

        /// <summary>
        /// Gets or sets <seealso cref="SelectedItemProperty"/>.
        /// The selected <seealso cref="TimeRangeItem"/> . 
        /// </summary>
        public TimeRangeItem SelectedItem
        {
            get { return (TimeRangeItem)GetValue(SelectedItemProperty); }
            set { SetValue(SelectedItemProperty, value); }
        }

        ///// <summary>
        ///// Gets or sets <seealso cref="AllItemsProperty"/>.
        ///// All time range items. 
        ///// </summary>
        //public ObservableCollection<TimeRangeItem> AllItems
        //{
        //    get { return (ObservableCollection<TimeRangeItem>)GetValue(AllItemsProperty); }
        //    set { SetValue(SelectedItemProperty, value); }
        //}

        /// <summary>
        /// Initializes a new instance of <seealso cref="TimeRangeButtons"/> class;
        /// </summary>
        public TimeRangeButtons()
        {
            // Note: caption is visible to user, the second parameter timedCountDuration is not.
            _timeRangeItems = new ObservableCollection<TimeRangeItem>();
            _timeRangeItems.Add(new TimeRangeItem(
                ErrorReporting1HourButtonCaption, 
                $"{60 * 60 / 30}s", 
                TimeRangeEnum.PERIOD1HOUR, 
                EventTimeRangeEnum.PERIOD1HOUR));
            _timeRangeItems.Add(new TimeRangeItem(
                ErrorReporting6HoursButtonCaption, 
                $"{6 * 60 * 60 / 30}s", 
                TimeRangeEnum.PERIOD6HOURS, 
                EventTimeRangeEnum.PERIOD6HOURS));
            _timeRangeItems.Add(new TimeRangeItem(
                ErrorReporting1DayButtonCaption,
                $"{24 * 60 * 60 / 30}s", 
                TimeRangeEnum.PERIOD1DAY, 
                EventTimeRangeEnum.PERIOD1DAY));
            _timeRangeItems.Add(new TimeRangeItem(
                ErrorReporting7DaysButtonCaption, 
                $"{7 * 24 * 60 * 60 / 30}s", 
                TimeRangeEnum.PERIOD1WEEK, 
                EventTimeRangeEnum.PERIOD1WEEK));
            _timeRangeItems.Add(new TimeRangeItem(
                ErrorReporting30DaysButtonCaption,
                $"{24 * 60 * 60}s",
                TimeRangeEnum.PERIOD30DAYS,
                EventTimeRangeEnum.PERIOD30DAYS));

            InitializeComponent();
        }

        /// <summary>
        /// Override the <seealso cref="OnApplyTemplate"/> method to initialize controls.
        /// </summary>
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            ItemsSource = _timeRangeItems;
            SelectedItem = _timeRangeItems.Last();
        }

        private static void OnTimePartPropertyChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            var newValue = e.NewValue as TimeRangeItem;
            var oldValue = e.OldValue as TimeRangeItem;
            if (oldValue != null)
            {
                oldValue.IsCurrentSelection = false;
            }
            if (newValue != null)
            {
                newValue.IsCurrentSelection = true;
            }
        }

        private void timeRangeButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null)
            {
                Debug.WriteLine("timeRangeButton_Click, sender is not button. This is not expected. Code bug.");
                return;
            }

            SelectedItem = button.DataContext as TimeRangeItem;
        }
    }
}