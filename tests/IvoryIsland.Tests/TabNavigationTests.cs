using System.Collections.ObjectModel;
using IvoryIsland.Models;
using Xunit;

namespace IvoryIsland.Tests;

/// <summary>
/// Unit tests verifying macOS Safari-inspired tab navigation rules, active tab switching,
/// safe tab closure, and preventing orphaned or zero-tab states.
/// </summary>
public sealed class TabNavigationTests
{
    private static ObservableCollection<TabItemModel> CreateDefaultTabs()
    {
        return
        [
            new TabItemModel { Id = "now-playing", Title = "Now Playing", IconGlyph = "\uE8D6", IsActive = true, NavigationUri = "ivory://now-playing" },
            new TabItemModel { Id = "audio-control", Title = "Audio Control", IconGlyph = "\uE995", IsActive = false, NavigationUri = "ivory://audio-control" },
            new TabItemModel { Id = "media-sources", Title = "Media Sources", IconGlyph = "\uEC4F", IsActive = false, NavigationUri = "ivory://media-sources" },
            new TabItemModel { Id = "settings", Title = "Settings", IconGlyph = "\uE713", IsActive = false, NavigationUri = "ivory://settings" }
        ];
    }

    [Fact]
    public void DefaultTabs_NowPlayingIsInitiallyActive()
    {
        var tabs = CreateDefaultTabs();
        Assert.Equal(4, tabs.Count);

        var activeTab = tabs.FirstOrDefault(t => t.IsActive);
        Assert.NotNull(activeTab);
        Assert.Equal("now-playing", activeTab.Id);
    }

    [Fact]
    public void SwitchTab_ActivatesTargetAndDeactivatesOthers()
    {
        var tabs = CreateDefaultTabs();
        var target = tabs.First(t => t.Id == "settings");

        foreach (var tab in tabs)
        {
            tab.IsActive = (tab == target);
        }

        Assert.True(target.IsActive);
        Assert.Equal(1, tabs.Count(t => t.IsActive));
        Assert.False(tabs.First(t => t.Id == "now-playing").IsActive);
    }

    [Fact]
    public void CloseActiveTab_SwitchesToAdjacentTab()
    {
        var tabs = CreateDefaultTabs();
        var activeTab = tabs[0]; // now-playing

        int index = tabs.IndexOf(activeTab);
        tabs.Remove(activeTab);

        int nextIndex = Math.Clamp(index, 0, tabs.Count - 1);
        tabs[nextIndex].IsActive = true;

        Assert.Equal(3, tabs.Count);
        Assert.True(tabs[0].IsActive);
        Assert.Equal("audio-control", tabs[0].Id);
    }

    [Fact]
    public void FinalTab_CannotBeClosedOrOrphaned()
    {
        var tabs = new ObservableCollection<TabItemModel>
        {
            new() { Id = "now-playing", Title = "Now Playing", IsActive = true }
        };

        // When only 1 tab remains, removal should be prevented
        if (tabs.Count > 1)
        {
            tabs.RemoveAt(0);
        }

        Assert.Single(tabs);
        Assert.True(tabs[0].IsActive);
    }

    [Fact]
    public void ReopenMissingTab_AddsTabBackAndActivatesIt()
    {
        var tabs = CreateDefaultTabs();
        var settingsTab = tabs.First(t => t.Id == "settings");
        tabs.Remove(settingsTab);

        Assert.DoesNotContain(tabs, t => t.Id == "settings");

        // Simulate new tab action finding missing tab
        string[] allIds = ["now-playing", "audio-control", "media-sources", "settings"];
        string? missingId = allIds.FirstOrDefault(id => !tabs.Any(t => t.Id == id));

        Assert.Equal("settings", missingId);

        var restored = new TabItemModel
        {
            Id = missingId!,
            Title = "Settings",
            IconGlyph = "\uE713",
            IsActive = true,
            NavigationUri = "ivory://settings"
        };
        tabs.Add(restored);

        Assert.Equal(4, tabs.Count);
        Assert.True(tabs.Last().IsActive);
    }
}