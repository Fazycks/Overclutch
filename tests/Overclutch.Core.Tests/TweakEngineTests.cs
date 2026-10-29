using Microsoft.Win32;
using Overclutch.Core.Native;
using Overclutch.Core.Tweaks;

namespace Overclutch.Core.Tests;

/// <summary>Exercises apply/revert end to end on a throwaway HKCU key.</summary>
public sealed class TweakEngineTests : IDisposable
{
    private readonly string _keyPath = $@"Software\Overclutch.Tests\{Guid.NewGuid():N}";
    private readonly string _backupFile = Path.Combine(Path.GetTempPath(), $"overclutch-{Guid.NewGuid():N}.json");

    private Tweak MakeTweak(params ITweakOperation[] ops) => new()
    {
        Id = "test.tweak",
        Title = "Test",
        Description = "Test",
        Category = TweakCategory.System,
        Operations = () => ops,
    };

    private object? Read(string name) => RegistryAccess.Read(RegistryHive.CurrentUser, _keyPath, name);

    private void Write(string name, object value, RegistryValueKind kind)
    {
        using var key = RegistryAccess.Create(RegistryHive.CurrentUser, _keyPath);
        key.SetValue(name, value, kind);
    }

    [Fact]
    public void Apply_then_revert_restores_existing_and_missing_values()
    {
        Write("Existing", 7, RegistryValueKind.DWord);
        var engine = new TweakEngine(new BackupStore(_backupFile));
        var tweak = MakeTweak(
            RegistryValueOp.Hkcu(_keyPath, "Existing", 1),
            RegistryValueOp.Hkcu(_keyPath, "Missing", "on", RegistryValueKind.String));

        Assert.Equal(TweakState.NotApplied, engine.GetState(tweak));

        engine.Apply(tweak);
        Assert.Equal(TweakState.Applied, engine.GetState(tweak));
        Assert.Equal(1, Read("Existing"));
        Assert.Equal("on", Read("Missing"));
        Assert.True(engine.CanRevert(tweak));

        engine.Revert(tweak);
        Assert.Equal(7, Read("Existing"));
        Assert.Null(Read("Missing"));
        Assert.False(engine.CanRevert(tweak));
    }

    [Fact]
    public void Reapplying_keeps_the_original_backup()
    {
        Write("Value", 3, RegistryValueKind.DWord);
        var store = new BackupStore(_backupFile);
        var engine = new TweakEngine(store);
        var tweak = MakeTweak(RegistryValueOp.Hkcu(_keyPath, "Value", 1));

        engine.Apply(tweak);
        engine.Apply(tweak);
        engine.Revert(tweak);

        Assert.Equal(3, Read("Value"));
    }

    [Fact]
    public void Backups_survive_a_reload_from_disk()
    {
        Write("Multi", new[] { "a", "b" }, RegistryValueKind.MultiString);
        Write("Big", 0xFFFFFFFFL, RegistryValueKind.QWord);
        var tweak = MakeTweak(
            RegistryValueOp.Hkcu(_keyPath, "Multi", "x", RegistryValueKind.String),
            RegistryValueOp.Hkcu(_keyPath, "Big", 1L, RegistryValueKind.QWord));

        new TweakEngine(new BackupStore(_backupFile)).Apply(tweak);
        new TweakEngine(new BackupStore(_backupFile)).Revert(tweak);

        Assert.Equal(new[] { "a", "b" }, Read("Multi"));
        Assert.Equal(0xFFFFFFFFL, Read("Big"));
        Assert.Equal(RegistryValueKind.MultiString, RegistryAccess.Open(RegistryHive.CurrentUser, _keyPath, false)!.GetValueKind("Multi"));
    }

    [Fact]
    public void Partial_state_is_reported()
    {
        Write("A", 1, RegistryValueKind.DWord);
        var engine = new TweakEngine(new BackupStore(_backupFile));
        var tweak = MakeTweak(RegistryValueOp.Hkcu(_keyPath, "A", 1), RegistryValueOp.Hkcu(_keyPath, "B", 1));

        Assert.Equal(TweakState.Partial, engine.GetState(tweak));
    }

    public void Dispose()
    {
        using var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        root.DeleteSubKeyTree(@"Software\Overclutch.Tests", throwOnMissingSubKey: false);
        File.Delete(_backupFile);
    }
}
