﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RetroDRY
{
    /// <summary>
    /// A timed worker for calling background tasks; runs from creation through disposing, checking schedule every 5 seconds
    /// </summary>
    public class BackgroundWorker : IDisposable
    {
        private class ScheduleItem
        {
            public Func<Task> Action;
            public DateTime Next; //UTC
            public int Interval; //seconds

            public ScheduleItem(Func<Task> action, DateTime next, int interval)
            {
                Action = action;
                Next = next;
                Interval = interval;
            }
        }

        private bool IsDisposed;
        private readonly List<ScheduleItem> Schedule = new List<ScheduleItem>(); //lock on access

        /// <summary>
        /// Create
        /// </summary>
        public BackgroundWorker()
        {
            Task.Run(Run);
        }

        /// <summary>
        /// </summary>
        public void Dispose()
        {
            IsDisposed = true;
        }

        /// <summary>
        /// Register an action to call repeatedly
        /// </summary>
        /// <param name="interval">in seconds; the first call will be this many seconds in the future</param>
        /// <param name="action">the action to run</param>
        public void Register(Func<Task> action, int interval)
        {
            lock (Schedule)
            {
                Schedule.Add(new ScheduleItem(action, DateTime.UtcNow.AddSeconds(interval), interval));
            }
        }

        private async void Run()
        {
            while (!IsDisposed)
            {
                await Task.Delay(5000);
                try
                {
                    DateTime now = DateTime.UtcNow;
                    var tocall = new List<Func<Task>>();
                    lock (Schedule)
                    {
                        foreach (var item in Schedule)
                        {
                            if (now > item.Next)
                            {
                                tocall.Add(item.Action);
                                item.Next = now.AddSeconds(item.Interval);
                            }
                        }
                    }
                    foreach (var thing in tocall) await thing();
                }
                catch { } //this is async void method so it must catch everything
            }
        }
    }
}
