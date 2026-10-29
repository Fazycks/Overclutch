using CommunityToolkit.Mvvm.ComponentModel;
using Overclutch.Core.Tweaks;
using Wpf.Ui.Controls;

namespace Overclutch.App.ViewModels;

public sealed record TweakGroup(string Title, SymbolRegular Icon, IReadOnlyList<TweakItemViewModel> Items);

/// <summary>Tweaks of one risk level, grouped by category.</summary>
public sealed partial class TweakListViewModel : ObservableObject
{
    public TweakListViewModel(TweakRisk risk)
    {
        Groups =
        [
            .. TweakCatalog.All
                .Where(t => t.Risk == risk)
                .GroupBy(t => t.Category)
                .OrderBy(g => g.Key)
                .Select(g => new TweakGroup(CategoryTitle(g.Key), CategoryIcon(g.Key), [.. g.Select(t => new TweakItemViewModel(t))])),
        ];
    }

    public IReadOnlyList<TweakGroup> Groups { get; }

    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    public async Task RefreshAsync()
    {
        if (IsLoading)
            return;
        IsLoading = true;
        try
        {
            var items = Groups.SelectMany(g => g.Items).ToList();
            // One background pass: the NVIDIA driver session is not meant to be used from several threads.
            var states = await Task.Run(() => items.Select(i => i.Read()).ToList());
            for (var i = 0; i < items.Count; i++)
                items[i].Show(states[i]);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public static string CategoryTitle(TweakCategory category) => category switch
    {
        TweakCategory.System => "Système et processeur",
        TweakCategory.Background => "Services et arrière-plan",
        TweakCategory.Network => "Réseau",
        TweakCategory.GpuGamingInput => "GPU, jeux et souris",
        _ => category.ToString(),
    };

    private static SymbolRegular CategoryIcon(TweakCategory category) => category switch
    {
        TweakCategory.System => SymbolRegular.DeveloperBoard24,
        TweakCategory.Background => SymbolRegular.Broom24,
        TweakCategory.Network => SymbolRegular.Wifi124,
        TweakCategory.GpuGamingInput => SymbolRegular.Games24,
        _ => SymbolRegular.Options24,
    };
}
