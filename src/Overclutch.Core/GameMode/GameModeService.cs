using System.ComponentModel;
using System.Diagnostics;
using Overclutch.Core.Maintenance;
using Overclutch.Core.Native;
using Overclutch.Core.Settings;

namespace Overclutch.Core.GameMode;

public sealed record GameSession(int ProcessId, string Name, string Path, DateTime StartedAt, IReadOnlyList<string> Actions);

/// <summary>
/// Watches for a game to start, boosts it while it runs and puts everything back when it exits.
/// </summary>
public sealed class GameModeService(Func<AppSettings> settings) : IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan LibrariesRefresh = TimeSpan.FromMinutes(1);

    /// <summary>Search indexing, Windows Update and the transfers it runs through: disk and network bursts.</summary>
    private static readonly string[] PausedServices = ["WSearch", "wuauserv", "BITS", "DoSvc"];

    private readonly Lock _lock = new();
    private readonly Dictionary<int, GameMatch?> _games = [];
    private readonly List<(Process Process, ProcessPriorityClass Original)> _lowered = [];
    private readonly List<string> _paused = [];
    private Timer? _timer;
    private Process? _game;
    private Guid? _previousPowerPlan;
    private IReadOnlyList<GameInstall> _installs = [];
    private DateTime _installsReadAt = DateTime.MinValue;

    private sealed record GameMatch(string Path, string Name);

    public event Action<GameSession>? GameStarted;
    public event Action<GameSession>? GameEnded;

    public GameSession? Current { get; private set; }

    public void Start() => _timer ??= new Timer(_ => Tick(), null, TimeSpan.Zero, Interval);

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        lock (_lock)
            EndSession();
    }

    private void Tick()
    {
        lock (_lock)
        {
            try
            {
                if (Current is not null)
                {
                    if (_game is null || GameExited(_game))
                        EndSession();
                    return;
                }

                if (settings().GameModeEnabled && FindGame() is { } game)
                    StartSession(game.Process, game.Match);
            }
            catch (Exception e)
            {
                Trace.WriteLine($"Mode jeu : {e}");
            }
        }
    }

    private (Process Process, GameMatch Match)? FindGame()
    {
        // A game installed or moved meanwhile shows up at the next read; processes are looked at again too.
        if (DateTime.UtcNow - _installsReadAt > LibrariesRefresh)
        {
            _installs = GameLibraries.Discover();
            _installsReadAt = DateTime.UtcNow;
            _games.Clear();
        }

        var extraGames = settings().ExtraGames;
        var processes = Process.GetProcesses();
        var alive = new HashSet<int>();
        (Process, GameMatch)? found = null;

        foreach (var process in processes)
        {
            alive.Add(process.Id);
            if (found is not null || process.Id <= 4 || process.Id == Environment.ProcessId)
                continue;

            if (!_games.TryGetValue(process.Id, out var match))
            {
                var path = ProcessImage.PathOf(process.Id);
                match = path is not null && GameDetector.IsGame(path, extraGames, _installs)
                    ? new GameMatch(path, GameDetector.InstallOf(path, _installs)?.Name ?? Path.GetFileNameWithoutExtension(path))
                    : null;
                _games[process.Id] = match;
            }

            // A game is only "running" once it has a window; until then keep checking.
            if (match is not null && SafeHasWindow(process))
                found = (process, match);
        }

        foreach (var pid in _games.Keys.Where(pid => !alive.Contains(pid)).ToList())
            _games.Remove(pid);

        foreach (var process in processes.Where(p => found is null || p.Id != found.Value.Item1.Id))
            process.Dispose();
        return found;
    }

    private void StartSession(Process game, GameMatch match)
    {
        var current = settings();
        var options = current.GameModeOptions;
        var actions = new List<string>();

        if (options.RaisePriority && TrySetPriority(game, ProcessPriorityClass.High) is not null)
            actions.Add("Priorité CPU du jeu : haute");

        if (options.BoostPowerPlan)
        {
            var active = PowerPlans.GetActive();
            var plan = PowerPlans.EnsureOverclutchPlan();
            if (active != plan)
            {
                PowerPlans.SetActive(plan);
                _previousPowerPlan = active;
                actions.Add("Plan d'alimentation Overclutch activé");
            }
        }

        if (options.LowerBackgroundApps)
        {
            var names = current.BackgroundApps.Select(GameDetector.TrimExe).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var process in Process.GetProcesses())
            {
                if (names.Contains(process.ProcessName) && TrySetPriority(process, ProcessPriorityClass.BelowNormal) is { } original)
                    _lowered.Add((process, original));
                else
                    process.Dispose();
            }
            if (_lowered.Count > 0)
                actions.Add($"{_lowered.Count} processus d'arrière-plan en priorité basse");
        }

        if (options.PurgeStandbyMemory)
        {
            try
            {
                actions.Add($"Mémoire en attente vidée ({SizeText.Format(Cleanup.PurgeStandbyMemory())})");
            }
            catch (Exception e) when (e is Win32Exception or InvalidOperationException)
            {
                Trace.WriteLine($"Mode jeu : {e.Message}");
            }
        }

        if (options.PauseServices)
        {
            foreach (var name in PausedServices)
                if (WindowsServices.Pause(name, TimeSpan.FromSeconds(10)))
                    _paused.Add(name);
            if (_paused.Count(WindowsServices.IsStopped) is var count and > 0)
                actions.Add(count == 1 ? "1 service d'arrière-plan en pause" : $"{count} services d'arrière-plan en pause");
        }

        _game = game;
        Current = new GameSession(game.Id, match.Name, match.Path, DateTime.Now, actions);
        GameStarted?.Invoke(Current);
    }

    private void EndSession()
    {
        if (Current is not { } session)
            return;

        foreach (var (process, original) in _lowered)
        {
            if (!SafeHasExited(process))
                TrySetPriority(process, original);
            process.Dispose();
        }
        _lowered.Clear();

        foreach (var name in _paused)
            WindowsServices.Resume(name);
        _paused.Clear();

        if (_previousPowerPlan is { } previous && PowerPlans.Exists(previous))
            PowerPlans.SetActive(previous);
        _previousPowerPlan = null;

        _game?.Dispose();
        _game = null;
        Current = null;
        GameEnded?.Invoke(session);
    }

    /// <summary>Returns the previous priority, or null when the process refused (protected by an anti-cheat...).</summary>
    private static ProcessPriorityClass? TrySetPriority(Process process, ProcessPriorityClass priority)
    {
        try
        {
            var original = process.PriorityClass;
            process.PriorityClass = priority;
            return original;
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    private static bool SafeHasWindow(Process process)
    {
        try { return process.MainWindowHandle != 0; }
        catch (InvalidOperationException) { return false; }
    }

    private static bool SafeHasExited(Process process)
    {
        try { return process.HasExited; }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException) { return true; }
    }

    /// <summary>
    /// An anti-cheat can refuse even the handle needed to watch its game; it has then exited once its ID is gone
    /// or taken by another program.
    /// </summary>
    private static bool GameExited(Process game)
    {
        try { return game.HasExited; }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException)
        {
            try
            {
                using var current = Process.GetProcessById(game.Id);
                return current.ProcessName != game.ProcessName;
            }
            catch (ArgumentException) { return true; }
        }
    }

    public void Dispose() => Stop();
}
