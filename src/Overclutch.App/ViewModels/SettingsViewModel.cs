using System.IO;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Overclutch.App.Services;
using Overclutch.Core;
using Overclutch.Core.Settings;

namespace Overclutch.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private static AppServices Services => AppServices.Instance;
    private static UpdateService Updates => Services.Updates;

    private bool _loaded;

    /// <summary>Set while the switch is moved from code, so it does not re-create the scheduled task.</summary>
    private bool _suppressAutostart;

    public string Version => Updates.CurrentVersion;

    public string RepositoryUrl => UpdateService.RepositoryUrl;

    [ObservableProperty]
    public partial bool StartWithWindows { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditAutostart))]
    public partial bool IsAutostartBusy { get; private set; } = true;

    public bool CanEditAutostart => !IsAutostartBusy;

    [ObservableProperty]
    public partial string? AutostartError { get; private set; }

    public bool MinimizeToTray
    {
        get => Services.Settings.MinimizeToTray;
        set
        {
            Services.Settings.MinimizeToTray = value;
            Services.Settings.Save();
            OnPropertyChanged();
        }
    }

    [ObservableProperty]
    public partial string UpdateStatus { get; private set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckUpdatesCommand), nameof(InstallUpdateCommand))]
    public partial bool IsUpdating { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallUpdateCommand))]
    public partial string? AvailableVersion { get; private set; }

    public async Task LoadAsync()
    {
        if (_loaded)
            return;
        _loaded = true;

        UpdateStatus = Updates.IsInstalled
            ? "Overclutch vérifie les mises à jour sur GitHub."
            : "Version de développement : les mises à jour ne concernent que la version installée.";

        var enabled = await Task.Run(() => Autostart.IsEnabled);
        _suppressAutostart = true;
        StartWithWindows = enabled;
        _suppressAutostart = false;
        IsAutostartBusy = false;

        if (Updates.IsInstalled)
            await CheckUpdatesAsync();
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (!_suppressAutostart)
            _ = ApplyAutostartAsync(value);
    }

    private async Task ApplyAutostartAsync(bool enable)
    {
        IsAutostartBusy = true;
        AutostartError = null;
        try
        {
            await Task.Run(() =>
            {
                if (enable)
                    Autostart.Enable(Environment.ProcessPath!);
                else
                    Autostart.Disable();
            });
        }
        catch (Exception e)
        {
            AutostartError = e.Message;
            _suppressAutostart = true;
            StartWithWindows = !enable;
            _suppressAutostart = false;
        }
        finally
        {
            IsAutostartBusy = false;
        }
    }

    private bool CanCheck() => !IsUpdating && Updates.IsInstalled;

    [RelayCommand(CanExecute = nameof(CanCheck))]
    private async Task CheckUpdatesAsync()
    {
        IsUpdating = true;
        try
        {
            UpdateStatus = "Recherche de mises à jour…";
            AvailableVersion = await Updates.CheckAsync();
            UpdateStatus = AvailableVersion is null
                ? "Overclutch est à jour."
                : $"La version {AvailableVersion} est disponible.";
        }
        catch (Exception e)
        {
            UpdateStatus = $"Impossible de vérifier les mises à jour : {e.Message}";
        }
        finally
        {
            IsUpdating = false;
        }
    }

    private bool CanInstall() => !IsUpdating && AvailableVersion is not null;

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task InstallUpdateAsync()
    {
        IsUpdating = true;
        try
        {
            // Game mode must hand back the power plan and priorities before the process is replaced.
            Services.GameMode.Stop();
            await Updates.DownloadAndRestartAsync(p => UpdateStatus = $"Téléchargement… {p} %");
        }
        catch (Exception e)
        {
            UpdateStatus = $"Échec de la mise à jour : {e.Message}";
            if (Services.Settings.GameModeEnabled)
                Services.GameMode.Start();
        }
        finally
        {
            IsUpdating = false;
        }
    }

    [RelayCommand]
    private static void OpenDataFolder()
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppPaths.DataDirectory}\""));
    }

    [RelayCommand]
    private static void OpenRepository() =>
        Process.Start(new ProcessStartInfo(UpdateService.RepositoryUrl) { UseShellExecute = true });
}
