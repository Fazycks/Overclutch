using Velopack;
using Velopack.Sources;

namespace Overclutch.App.Services;

public sealed class UpdateService
{
    public const string RepositoryUrl = "https://github.com/Fazycks/Overclutch";

    private readonly UpdateManager _manager = new(new GithubSource(RepositoryUrl, null, false));
    private UpdateInfo? _pending;

    /// <summary>False when running from a build folder rather than an installed copy.</summary>
    public bool IsInstalled => _manager.IsInstalled;

    public string CurrentVersion =>
        _manager.CurrentVersion?.ToString() ?? typeof(UpdateService).Assembly.GetName().Version?.ToString(3) ?? "?";

    /// <summary>Returns the new version number, or null when up to date.</summary>
    public async Task<string?> CheckAsync()
    {
        _pending = await _manager.CheckForUpdatesAsync();
        return _pending?.TargetFullRelease.Version.ToString();
    }

    public async Task DownloadAndRestartAsync(Action<int> progress)
    {
        if (_pending is null)
            return;
        await _manager.DownloadUpdatesAsync(_pending, progress);
        _manager.ApplyUpdatesAndRestart(_pending.TargetFullRelease);
    }
}
