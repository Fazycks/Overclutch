using System.ComponentModel;
using System.Diagnostics;
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

    private readonly Lock _lock = new();
    private readonly Dictionary<int, string?> _gamePaths = [];
    private readonly List<(Process Process, ProcessPriorityClass Original)> _lowered = [];
    private Timer? _timer;
    private Process? _game;
    private Guid? _previousPowerPlan;

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
                    if (_game is null || _game.HasExited)
                        EndSession();
                    return;
                }

                if (settings().GameModeEnabled && FindGame() is { } game)
                    StartSession(game.Process, game.Path);
            }
            catch (Exception e)
            {
                Trace.WriteLine($"Mode jeu : {e}");
            }
        }
    }

    private (Process Process, string Path)? FindGame()
    {
        var extraGames = settings().ExtraGames;
        var processes = Process.GetProcesses();
        var alive = new HashSet<int>();
        (Process, string)? found = null;

        foreach (var process in processes)
        {
            alive.Add(process.Id);
            if (found is not null || process.Id <= 4 || process.Id == Environment.ProcessId)
                continue;

            if (!_gamePaths.TryGetValue(process.Id, out var gamePath))
            {
                var path = ImagePath(process.Id);
                gamePath = path is not null && GameDetector.IsGame(path, extraGames) ? path : null;
                _gamePaths[process.Id] = gamePath;
            }

            // A game is only "running" once it has a window; until then keep checking.
            if (gamePath is not null && SafeHasWindow(process))
                found = (process, gamePath);
        }

        foreach (var pid in _gamePaths.Keys.Where(pid => !alive.Contains(pid)).ToList())
            _gamePaths.Remove(pid);

        foreach (var process in processes.Where(p => found is null || p.Id != found.Value.Item1.Id))
            process.Dispose();
        return found;
    }

    private void StartSession(Process game, string path)
    {
        var current = settings();
        var actions = new List<string>();

        if (TrySetPriority(game, ProcessPriorityClass.High) is not null)
            actions.Add("Priorité CPU du jeu : haute");

        if (current.GameModeBoostPowerPlan)
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

        if (current.GameModeLowerBackgroundApps)
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

        _game = game;
        Current = new GameSession(game.Id, Path.GetFileNameWithoutExtension(path), path, DateTime.Now, actions);
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

    private static string? ImagePath(int pid)
    {
        var handle = NativeMethods.OpenProcess(NativeMethods.ProcessQueryLimitedInformation, false, pid);
        if (handle == 0)
            return null;
        try
        {
            var buffer = new char[1024];
            var size = (uint)buffer.Length;
            return NativeMethods.QueryFullProcessImageName(handle, 0, buffer, ref size) ? new string(buffer, 0, (int)size) : null;
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }
    }

    public void Dispose() => Stop();
}
