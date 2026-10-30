using System.IO.Compression;
using System.Text;
using Overclutch.Core.GameMode;
using Overclutch.Core.Profiles;
using Overclutch.Core.Settings;
using Overclutch.Core.Tweaks;

namespace Overclutch.Core.Tests;

public sealed class ProfileTests : IDisposable
{
    private readonly string _backupFile = Path.Combine(Path.GetTempPath(), $"overclutch-{Guid.NewGuid():N}.json");

    private sealed class FakeOp(bool available, bool applied) : ITweakOperation
    {
        public bool IsAvailable => available;
        public bool IsApplied { get; private set; } = applied;
        public BackupEntry Capture() => new RegistryBackup(Microsoft.Win32.RegistryHive.CurrentUser, @"Software\Overclutch.Tests", Guid.NewGuid().ToString("N"), null, null);
        public void Apply() => IsApplied = true;
    }

    private static Tweak MakeTweak(string id, TweakRisk risk = TweakRisk.Safe, bool available = true, bool applied = false) => new()
    {
        Id = id,
        Title = id,
        Description = id,
        Category = TweakCategory.System,
        Risk = risk,
        Operations = () => [new FakeOp(available, applied)],
    };

    private TweakEngine Engine() => new(new BackupStore(_backupFile));

    /// <summary>A code carrying this exact text, to feed <see cref="ShareCode.Decode"/> what it never writes itself.</summary>
    private static string CodeOf(string json)
    {
        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.SmallestSize))
            deflate.Write(Encoding.UTF8.GetBytes(json));
        return "OC1-" + Convert.ToBase64String(output.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    [Fact]
    public void A_profile_survives_a_share_code()
    {
        var profile = new Profile
        {
            Name = "Compétitif é€",
            Tweaks = ["a", "b"],
            GameModeLevel = GameModeLevel.Custom,
            GameModeCustom = new GameModeOptions { RaisePriority = false, PauseServices = true },
            ExtraGames = ["osu!"],
            BackgroundApps = ["Spotify"],
        };

        var decoded = ShareCode.Decode(ShareCode.Encode(profile));

        Assert.Equal(profile.Name, decoded.Name);
        Assert.Equal(profile.Tweaks, decoded.Tweaks);
        Assert.Equal(profile.GameModeLevel, decoded.GameModeLevel);
        Assert.Equal(profile.GameModeCustom, decoded.GameModeCustom);
        Assert.Equal(profile.ExtraGames, decoded.ExtraGames);
        Assert.Equal(profile.BackgroundApps, decoded.BackgroundApps);
    }

    [Fact]
    public void A_code_wrapped_by_a_chat_still_reads()
    {
        var code = ShareCode.Encode(new Profile { Name = "Test" });

        Assert.Equal("Test", ShareCode.Decode($"  {code[..10]}\r\n{code[10..]} \n").Name);
    }

    [Fact]
    public void The_largest_profile_fits_in_a_chat_message()
    {
        var code = ShareCode.Encode(new Profile
        {
            Name = new string('x', 40),
            Tweaks = [.. TweakCatalog.All.Select(t => t.Id)],
            BackgroundApps = new AppSettings().BackgroundApps,
        });

        Assert.True(code.Length < 2000, $"{code.Length} caractères");
    }

    [Theory]
    [InlineData("")]
    [InlineData("bonjour")]
    [InlineData("OC1-")]
    [InlineData("OC1-pas du tout du base64 !")]
    [InlineData("OC1-AAAA")]
    public void Garbage_is_refused(string code) => Assert.Throws<FormatException>(() => ShareCode.Decode(code));

    [Theory]
    [InlineData("null")]
    [InlineData("[1, 2]")]
    [InlineData("""{ "Tweaks": "pas une liste" }""")]
    public void Valid_compression_with_the_wrong_content_is_refused(string json) =>
        Assert.Throws<FormatException>(() => ShareCode.Decode(CodeOf(json)));

    [Fact]
    public void A_code_from_a_newer_version_asks_to_update()
    {
        var error = Assert.Throws<FormatException>(() => ShareCode.Decode("OC2-AAAA"));

        Assert.Contains("mets l'application à jour", error.Message);
    }

    [Fact]
    public void A_code_that_inflates_to_a_huge_text_is_refused()
    {
        var code = CodeOf(new string(' ', 5_000_000) + "{}");

        Assert.True(code.Length < 16 * 1024);
        Assert.Throws<FormatException>(() => ShareCode.Decode(code));
    }

    [Fact]
    public void A_code_cannot_smuggle_paths_or_bad_values()
    {
        var profile = ShareCode.Decode(CodeOf("""
            {
              "Name": "  a\u0007b  ",
              "GameModeLevel": 99,
              "GameModeCustom": null,
              "Tweaks": null,
              "ExtraGames": ["C:\\Users\\moi\\jeu.exe", "..\\autre", "osu!.exe", "OSU!", ""],
              "BackgroundApps": null
            }
            """));

        Assert.Equal("ab", profile.Name);
        Assert.Equal(GameModeLevel.Balanced, profile.GameModeLevel);
        Assert.Equal(GameModeOptions.Balanced, profile.GameModeCustom);
        Assert.Empty(profile.Tweaks);
        Assert.Equal(["osu!"], profile.ExtraGames);
        Assert.Empty(profile.BackgroundApps);
    }

    [Theory]
    [InlineData("", "Profil importé")]
    [InlineData("   ", "Profil importé")]
    public void A_nameless_profile_gets_a_name(string name, string expected) =>
        Assert.Equal(expected, ShareCode.Decode(ShareCode.Encode(new Profile { Name = name })).Name);

    [Fact]
    public void Capture_leaves_out_advanced_tweaks_overclutch_did_not_change()
    {
        var engine = Engine();
        var native = MakeTweak("native", applied: true);
        var nativeAdvanced = MakeTweak("native-advanced", TweakRisk.Advanced, applied: true);
        var changedAdvanced = MakeTweak("changed-advanced", TweakRisk.Advanced);
        var untouched = MakeTweak("untouched");
        engine.Apply(changedAdvanced);

        var profile = Profile.Capture("Moi", engine, new AppSettings { GameModeLevel = GameModeLevel.Performance },
            [native, nativeAdvanced, changedAdvanced, untouched]);

        Assert.Equal(["native", "changed-advanced"], profile.Tweaks);
        Assert.Equal(GameModeLevel.Performance, profile.GameModeLevel);
    }

    [Fact]
    public void The_plan_sorts_tweaks_by_what_this_pc_can_do()
    {
        var todo = MakeTweak("todo");
        var advanced = MakeTweak("advanced", TweakRisk.Advanced);
        var done = MakeTweak("done", applied: true);
        var missing = MakeTweak("missing", available: false);
        var profile = new Profile { Tweaks = ["todo", "advanced", "done", "missing", "from-the-future", "todo"] };

        var plan = profile.Plan(Engine(), new AppSettings(), [todo, advanced, done, missing, MakeTweak("not-in-profile")]);

        Assert.Equal([todo, advanced], plan.ToApply);
        Assert.Equal([advanced], plan.Advanced);
        Assert.Equal([missing], plan.Unavailable);
        Assert.Equal(1, plan.AlreadyActive);
        Assert.Equal(1, plan.UnknownTweaks);
    }

    [Fact]
    public void Applying_settings_adds_to_the_lists_and_takes_the_level()
    {
        var settings = new AppSettings { ExtraGames = ["osu!"], BackgroundApps = ["Spotify"] };
        var profile = new Profile
        {
            GameModeLevel = GameModeLevel.Performance,
            ExtraGames = ["OSU!", "javaw"],
            BackgroundApps = ["Discord"],
        };

        var plan = profile.Plan(Engine(), settings, []);
        profile.ApplySettingsTo(settings);

        Assert.Equal(["javaw"], plan.NewGames);
        Assert.Equal(["Discord"], plan.NewBackgroundApps);
        Assert.Equal(GameModeLevel.Performance, settings.GameModeLevel);
        Assert.Equal(["osu!", "javaw"], settings.ExtraGames);
        Assert.Equal(["Spotify", "Discord"], settings.BackgroundApps);
    }

    [Fact]
    public void Saved_profiles_survive_the_settings_file()
    {
        var settings = new AppSettings { Profiles = [new Profile { Name = "Ranked", Tweaks = ["a"] }] };

        var saved = Assert.Single(AppSettings.Parse(settings.ToJson()).Profiles);

        Assert.Equal("Ranked", saved.Name);
        Assert.Equal(["a"], saved.Tweaks);
        Assert.Empty(AppSettings.Parse("{}").Profiles);
    }

    public void Dispose() => File.Delete(_backupFile);
}
