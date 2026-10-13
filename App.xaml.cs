using System;
using System.Diagnostics;
using System.Windows;

namespace LucidTune
{
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += (s, e) =>
            {
                Debug.WriteLine($"[DispatcherUnhandledException] {e.Exception}");
            };
        }
    }
}
