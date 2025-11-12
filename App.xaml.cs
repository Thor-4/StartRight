using System;
using System.Threading.Tasks;
using System.Windows;

namespace StartRight
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Handle any unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                MessageBox.Show($"Unhandled exception: {args.ExceptionObject}",
                              "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            };

            this.DispatcherUnhandledException += (sender, args) =>
            {
                MessageBox.Show($"Application error: {args.Exception.Message}",
                              "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            // Handle task scheduler exceptions
            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                MessageBox.Show($"Background task error: {args.Exception.Message}",
                              "Task Error", MessageBoxButton.OK, MessageBoxImage.Error);
                args.SetObserved();
            };
        }
    }
}