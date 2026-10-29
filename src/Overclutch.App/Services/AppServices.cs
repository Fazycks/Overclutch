using CommunityToolkit.Mvvm.ComponentModel;
using Overclutch.Core.GameMode;
using Overclutch.Core.Monitoring;
using Overclutch.Core.Settings;
using Overclutch.Core.Tweaks;

namespace Overclutch.App.Services;

/// <summary>Application-wide singletons. The app is small enough that a container would add nothing.</summary>
public sealed partial class AppServices : ObservableObject
{
    public static AppServices Instance { get; } = new();

    private AppServices()
    {
        Settings = AppSettings.Load();
        Engine = new TweakEngine(BackupStore.Default());
        GameMode = new GameModeService(() => Settings);
        Hardware = new Lazy<HardwareInfo>(HardwareInfo.Detect);
    }

    public AppSettings Settings { get; }
    public TweakEngine Engine { get; }
    public GameModeService GameMode { get; }
    public Lazy<HardwareInfo> Hardware { get; }
    public UpdateService Updates { get; } = new();

    /// <summary>Set once a tweak that only takes effect after a reboot changed during this session.</summary>
    [ObservableProperty]
    public partial bool RebootPending { get; set; }

    /// <summary>Raised when game mode is switched on or off, from the page or the tray menu.</summary>
    public event Action? GameModeEnabledChanged;

    public void SetGameModeEnabled(bool enabled)
    {
        Settings.GameModeEnabled = enabled;
        Settings.Save();
        if (enabled)
            GameMode.Start();
        else
            GameMode.Stop();
        GameModeEnabledChanged?.Invoke();
    }

    public void NotifyTweaksChanged(IEnumerable<Tweak> changed)
    {
        if (changed.Any(t => t.RequiresReboot))
            RebootPending = true;
    }
}
