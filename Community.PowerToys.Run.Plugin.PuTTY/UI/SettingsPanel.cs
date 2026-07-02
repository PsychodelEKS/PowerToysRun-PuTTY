using System.Windows;
using Community.PowerToys.Run.Plugin.PuTTY.Models;
using Forms = System.Windows.Forms;
using Wpf = System.Windows.Controls;

namespace Community.PowerToys.Run.Plugin.PuTTY.UI;

public sealed class SettingsPanel : Wpf.UserControl
{
    private readonly Wpf.CheckBox _enableGlobalResults;
    private readonly Wpf.CheckBox _enablePuTTYSessions;
    private readonly Wpf.CheckBox _enableKiTTYSessions;
    private readonly Wpf.TextBox _puttyExecutablePath;
    private readonly Wpf.TextBox _kittyExecutablePath;
    private readonly Action<PuTTYSettings> _saveSettings;
    private readonly Func<Task<int?>> _rescanAsync;

    public SettingsPanel(
        PuTTYSettings settings,
        Action<PuTTYSettings> saveSettings,
        Func<Task<int?>> rescanAsync)
    {
        _saveSettings = saveSettings;
        _rescanAsync = rescanAsync;

        _enableGlobalResults = new Wpf.CheckBox
        {
            Content = "Show sessions in global results",
            IsChecked = settings.EnableGlobalResults,
            Margin = new Thickness(0, 0, 0, 8),
        };
        _enablePuTTYSessions = new Wpf.CheckBox
        {
            Content = "Enable PuTTY sessions",
            IsChecked = settings.EnablePuTTYSessions,
            Margin = new Thickness(0, 0, 0, 8),
        };
        _enableKiTTYSessions = new Wpf.CheckBox
        {
            Content = "Enable KiTTY sessions",
            IsChecked = settings.EnableKiTTYSessions,
            Margin = new Thickness(0, 0, 0, 8),
        };
        _puttyExecutablePath = new Wpf.TextBox
        {
            Text = settings.PuTTYExecutablePath,
            MinWidth = 420,
        };
        _kittyExecutablePath = new Wpf.TextBox
        {
            Text = settings.KiTTYExecutablePath,
            MinWidth = 420,
        };

        Content = CreateLayout();
    }

    private UIElement CreateLayout()
    {
        var root = new Wpf.DockPanel
        {
            LastChildFill = true,
            Margin = new Thickness(12),
        };

        var buttons = new Wpf.StackPanel
        {
            Orientation = Wpf.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            Margin = new Thickness(0, 12, 0, 0),
        };

        var saveButton = new Wpf.Button { Content = "Save", MinWidth = 80, Margin = new Thickness(0, 0, 8, 0) };
        saveButton.Click += (_, _) => Save();

        var rescanButton = new Wpf.Button { Content = "Save and rescan", MinWidth = 120 };
        rescanButton.Click += async (_, _) =>
        {
            Save();
            rescanButton.IsEnabled = false;
            try
            {
                await _rescanAsync().ConfigureAwait(true);
            }
            finally
            {
                rescanButton.IsEnabled = true;
            }
        };

        buttons.Children.Add(saveButton);
        buttons.Children.Add(rescanButton);
        Wpf.DockPanel.SetDock(buttons, Wpf.Dock.Bottom);
        root.Children.Add(buttons);

        var panel = new Wpf.StackPanel();
        panel.Children.Add(_enableGlobalResults);

        panel.Children.Add(new Wpf.TextBlock
        {
            Text = @"This editor updates the same options shown in PowerToys Settings. Sessions are read from HKCU\Software\SimonTatham\PuTTY\Sessions and HKCU\Software\9bis.com\KiTTY\Sessions.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12),
        });

        panel.Children.Add(CreatePathRow("PuTTY executable", _puttyExecutablePath, "Select putty.exe"));
        panel.Children.Add(CreatePathRow("KiTTY executable", _kittyExecutablePath, "Select kitty.exe"));
        panel.Children.Add(_enablePuTTYSessions);
        panel.Children.Add(_enableKiTTYSessions);

        root.Children.Add(panel);
        return root;
    }

    private UIElement CreatePathRow(string label, Wpf.TextBox textBox, string dialogTitle)
    {
        var root = new Wpf.Grid
        {
            Margin = new Thickness(0, 0, 0, 10),
        };
        root.ColumnDefinitions.Add(new Wpf.ColumnDefinition { Width = new GridLength(130) });
        root.ColumnDefinitions.Add(new Wpf.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new Wpf.ColumnDefinition { Width = GridLength.Auto });

        var labelBlock = new Wpf.TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };
        Wpf.Grid.SetColumn(labelBlock, 0);
        root.Children.Add(labelBlock);

        Wpf.Grid.SetColumn(textBox, 1);
        root.Children.Add(textBox);

        var browseButton = new Wpf.Button
        {
            Content = "Browse",
            MinWidth = 80,
            Margin = new Thickness(8, 0, 0, 0),
        };
        browseButton.Click += (_, _) => BrowseExecutable(textBox, dialogTitle);
        Wpf.Grid.SetColumn(browseButton, 2);
        root.Children.Add(browseButton);

        return root;
    }

    private static void BrowseExecutable(Wpf.TextBox target, string title)
    {
        using var dialog = new Forms.OpenFileDialog
        {
            Title = title,
            Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.FileName))
        {
            target.Text = dialog.FileName;
        }
    }

    private void Save()
    {
        _saveSettings(new PuTTYSettings
        {
            EnableGlobalResults = _enableGlobalResults.IsChecked == true,
            EnablePuTTYSessions = _enablePuTTYSessions.IsChecked == true,
            EnableKiTTYSessions = _enableKiTTYSessions.IsChecked == true,
            PuTTYExecutablePath = _puttyExecutablePath.Text.Trim(),
            KiTTYExecutablePath = _kittyExecutablePath.Text.Trim(),
        });
    }
}
