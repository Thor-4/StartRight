using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using StartRight.Models;

namespace StartRight
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string RegistryStartupPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        public MainWindow()
        {
            InitializeComponent();
            CheckAdminStatus();
            LoadStartupPrograms();
            
            // Add double-click event handler
            StartupListView.MouseDoubleClick += StartupListView_MouseDoubleClick;
        }

        private void CheckAdminStatus()
        {
            bool isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent())
                .IsInRole(WindowsBuiltInRole.Administrator);
            
            AdminStatusText.Text = isAdmin ? " (Administrator)" : " (Standard User)";
            AdminStatusText.Foreground = isAdmin ? Brushes.Green : Brushes.Orange;
        }

        private void LoadStartupPrograms()
        {
            try
            {
                var startupPrograms = new List<StartupProgram>();

                // Load from Current User registry
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryStartupPath))
                {
                    if (key != null)
                    {
                        LoadRegistryPrograms(key, startupPrograms, "HKCU");
                    }
                }

                // Load from Local Machine registry (requires admin privileges)
                try
                {
                    using (RegistryKey? key = Registry.LocalMachine.OpenSubKey(RegistryStartupPath))
                    {
                        if (key != null)
                        {
                            LoadRegistryPrograms(key, startupPrograms, "HKLM");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Note: Could not load HKLM startup items: {ex.Message}");
                }

                // Load from Startup folder
                LoadStartupFolderPrograms(startupPrograms);

                StartupListView.ItemsSource = startupPrograms;
                StatusText.Text = $"Loaded {startupPrograms.Count} startup programs. Double-click to open file location.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading startup programs: {ex.Message}", "Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadRegistryPrograms(RegistryKey key, List<StartupProgram> programs, string location)
        {
            foreach (string valueName in key.GetValueNames())
            {
                object? value = key.GetValue(valueName);
                if (value != null)
                {
                    string path = value.ToString() ?? string.Empty;
                    if (!string.IsNullOrEmpty(path))
                    {
                        bool enabled = !path.StartsWith("REMOVED:");
                        
                        programs.Add(new StartupProgram(
                            valueName,
                            enabled ? path : path.Substring(8), // Remove "REMOVED:" prefix
                            enabled,
                            $"{location}\\{valueName}",
                            StartupType.Registry
                        ));
                    }
                }
            }
        }

        private void LoadStartupFolderPrograms(List<StartupProgram> programs)
        {
            string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            
            if (Directory.Exists(startupFolder))
            {
                foreach (string file in Directory.GetFiles(startupFolder))
                {
                    if (file.ToLower().EndsWith(".lnk") || file.ToLower().EndsWith(".exe"))
                    {
                        programs.Add(new StartupProgram(
                            Path.GetFileNameWithoutExtension(file),
                            file,
                            true,
                            $"StartupFolder\\{Path.GetFileName(file)}",
                            StartupType.StartupFolder
                        ));
                    }
                }
            }
        }

        private void BrowseProgram_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                Title = "Select a program to add to startup",
                InitialDirectory = @"C:\",
                RestoreDirectory = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                ProgramPathTextBox.Text = openFileDialog.FileName;
            }
        }

        private void AddStartup_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ProgramPathTextBox.Text))
            {
                MessageBox.Show("Please select a program first.", "Warning", 
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!File.Exists(ProgramPathTextBox.Text))
            {
                MessageBox.Show("The selected file does not exist.", "Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                string programPath = ProgramPathTextBox.Text;
                string programName = Path.GetFileNameWithoutExtension(programPath);

                // Add to Current User registry (no admin required)
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryStartupPath, true))
                {
                    if (key != null)
                    {
                        key.SetValue(programName, $"\"{programPath}\"");
                    }
                    else
                    {
                        MessageBox.Show("Could not access registry to add startup program.", "Error",
                                      MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                StatusText.Text = $"Added {programName} to startup";
                ProgramPathTextBox.Clear();
                LoadStartupPrograms(); // Refresh the list
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding program to startup: {ex.Message}", "Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemoveStartup_Click(object sender, RoutedEventArgs e)
        {
            if (StartupListView.SelectedItem is StartupProgram selectedProgram)
            {
                try
                {
                    if (selectedProgram.RegistryKey.StartsWith("HKCU"))
                    {
                        using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryStartupPath, true))
                        {
                            if (key != null)
                            {
                                key.DeleteValue(selectedProgram.Name, false);
                            }
                        }
                    }
                    else if (selectedProgram.RegistryKey.StartsWith("HKLM"))
                    {
                        // Try to remove from HKLM (requires admin)
                        try
                        {
                            using (RegistryKey? key = Registry.LocalMachine.OpenSubKey(RegistryStartupPath, true))
                            {
                                if (key != null)
                                {
                                    key.DeleteValue(selectedProgram.Name, false);
                                }
                            }
                        }
                        catch (UnauthorizedAccessException)
                        {
                            // Ask user if they want to run as admin
                            var result = MessageBox.Show(
                                $"Administrator privileges are required to remove '{selectedProgram.Name}'.\n\n" +
                                "Would you like to restart Start Right with administrator privileges?",
                                "Administrator Rights Required",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question);

                            if (result == MessageBoxResult.Yes)
                            {
                                RestartAsAdmin();
                                return; // Close the current instance
                            }
                            else
                            {
                                return; // User declined admin rights
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error removing program: {ex.Message}", "Error", 
                                        MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }
                    else if (selectedProgram.RegistryKey.StartsWith("StartupFolder"))
                    {
                        // Remove from startup folder
                        if (File.Exists(selectedProgram.Path))
                        {
                            File.Delete(selectedProgram.Path);
                        }
                    }

                    StatusText.Text = $"Removed {selectedProgram.Name} from startup";
                    LoadStartupPrograms(); // Refresh the list
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error removing program: {ex.Message}", "Error", 
                                MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Please select a program to remove.", "Warning", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void RestartAsAdmin()
        {
            try
            {
                string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? "StartRight.exe";
                var processInfo = new ProcessStartInfo
                {
                    Verb = "runas",
                    FileName = currentExe,
                    UseShellExecute = true
                };

                Process.Start(processInfo);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to restart as administrator: {ex.Message}", "Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ToggleStartup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.DataContext is StartupProgram program)
            {
                try
                {
                    if (program.RegistryKey.StartsWith("HKCU"))
                    {
                        using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryStartupPath, true))
                        {
                            if (key != null)
                            {
                                if (checkBox.IsChecked == true)
                                {
                                    // Enable: Remove "REMOVED:" prefix if present
                                    string path = program.Path.StartsWith("REMOVED:") ? 
                                        program.Path.Substring(8) : program.Path;
                                    key.SetValue(program.Name, path);
                                }
                                else
                                {
                                    // Disable: Add "REMOVED:" prefix but keep the value
                                    key.SetValue(program.Name, "REMOVED:" + program.Path);
                                }
                            }
                        }
                    }
                    else if (program.RegistryKey.StartsWith("HKLM"))
                    {
                        // For HKLM items, we need admin privileges
                        try
                        {
                            using (RegistryKey? key = Registry.LocalMachine.OpenSubKey(RegistryStartupPath, true))
                            {
                                if (key != null)
                                {
                                    if (checkBox.IsChecked == true)
                                    {
                                        string path = program.Path.StartsWith("REMOVED:") ? 
                                            program.Path.Substring(8) : program.Path;
                                        key.SetValue(program.Name, path);
                                    }
                                    else
                                    {
                                        key.SetValue(program.Name, "REMOVED:" + program.Path);
                                    }
                                }
                            }
                        }
                        catch (UnauthorizedAccessException)
                        {
                            var result = MessageBox.Show(
                                $"Administrator privileges are required to modify '{program.Name}'.\n\n" +
                                "Would you like to restart Start Right with administrator privileges?",
                                "Administrator Rights Required",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question);

                            if (result == MessageBoxResult.Yes)
                            {
                                RestartAsAdmin();
                                return;
                            }
                            else
                            {
                                // Revert the checkbox state since we couldn't make the change
                                checkBox.IsChecked = !checkBox.IsChecked;
                                return;
                            }
                        }
                    }

                    LoadStartupPrograms(); // Refresh to show updated state
                    StatusText.Text = $"{(checkBox.IsChecked == true ? "Enabled" : "Disabled")} {program.Name}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error toggling startup program: {ex.Message}", "Error", 
                                MessageBoxButton.OK, MessageBoxImage.Error);
                    LoadStartupPrograms(); // Refresh to revert UI state
                }
            }
        }

        private void RefreshList_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "Refreshing startup programs...";
                LoadStartupPrograms();
                var programs = StartupListView.ItemsSource as List<StartupProgram>;
                StatusText.Text = $"List refreshed - {programs?.Count ?? 0} programs found";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Error refreshing list";
                MessageBox.Show($"Error refreshing list: {ex.Message}", "Refresh Error", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveChanges_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int changesDetected = 0;
                
                // Check if there are any disabled programs (with REMOVED: prefix)
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryStartupPath))
                {
                    if (key != null)
                    {
                        foreach (string valueName in key.GetValueNames())
                        {
                            object? value = key.GetValue(valueName);
                            if (value?.ToString()?.StartsWith("REMOVED:") == true)
                            {
                                changesDetected++;
                            }
                        }
                    }
                }

                if (changesDetected > 0)
                {
                    StatusText.Text = $"Saved changes for {changesDetected} program(s)";
                    MessageBox.Show($"Successfully saved changes for {changesDetected} startup program(s).\n\n" +
                                "Disabled programs will not start on next system restart.",
                                "Changes Saved",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                }
                else
                {
                    StatusText.Text = "No pending changes to save";
                    MessageBox.Show("No changes detected. All startup programs are in their saved state.",
                                "No Changes",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving changes: {ex.Message}", "Error", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RunAsAdmin_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Restart Start Right with administrator privileges?\n\n" +
                "This will allow you to modify system-wide startup programs.",
                "Run as Administrator",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                RestartAsAdmin();
            }
        }

        private void StartupListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (StartupListView.SelectedItem is StartupProgram program)
            {
                try
                {
                    string? directory = Path.GetDirectoryName(program.Path);
                    if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                    {
                        Process.Start("explorer.exe", $"/select,\"{program.Path}\"");
                    }
                    else
                    {
                        MessageBox.Show("The file location does not exist or cannot be accessed.", 
                                      "Location Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not open file location: {ex.Message}", "Error", 
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}