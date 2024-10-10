using Arisoul.Core.Root.Extensions;

namespace Arisoul.T212.App.Controls;

public partial class TabView : VerticalStackLayout
{
    public static readonly BindableProperty TabsProperty = BindableProperty.Create(
        nameof(Tabs),
        typeof(ObservableCollection<CustomTab>),
        typeof(TabView),
        default(ObservableCollection<CustomTab>),
        propertyChanged: (bindable, oldValue, newValue) =>
        {
            if (bindable is TabView tabView)
                tabView.OnTabsChanged();
        });

    public ObservableCollection<CustomTab> Tabs
    {
        get => (ObservableCollection<CustomTab>)GetValue(TabsProperty);
        set => SetValue(TabsProperty, value);
    }

    public static readonly BindableProperty ActiveTabIndexProperty = BindableProperty.Create(
        nameof(ActiveTabIndex),
        typeof(int),
        typeof(TabView),
        default(int),
        propertyChanged: (bindable, oldValue, newValue) =>
        {
            if (bindable is TabView tabView)
                tabView.OnActiveTabIndexChanged();
        });

    public int ActiveTabIndex
    {
        get => (int)GetValue(ActiveTabIndexProperty);
        set => SetValue(ActiveTabIndexProperty, value);
    }

    private HorizontalStackLayout _headerRow;

    void OnTabsChanged()
    {
        Children.Clear();
        Children.Add(BuildTabs());
        OnActiveTabIndexChanged();
        ActiveTabIndex = Tabs.Count > 0 ? 0 : -1;
    }

    public TabView()
    {
        Tabs = [];
        Loaded += TabView_Loaded;
    }

    private void TabView_Loaded(object? sender, EventArgs e)
    {
        OnTabsChanged();
    }

    void OnActiveTabIndexChanged()
    {
        var activeTab = GetActiveTab();
        if (activeTab is null)
            return;

        if (Children.Count == 1)
            Children.Add(activeTab);
        else
            Children[1] = activeTab;

        foreach (var tab in Tabs)
        {
            var header = _headerRow.FindLayoutChildByAutomationId<Frame>(tab.AutomationId);

            if (header is null)
                continue;

            if (tab.AutomationId.Equals(activeTab.AutomationId))
                header.BackgroundColor = Colors.DarkBlue;
            else
                header.BackgroundColor = Colors.SlateGrey;
        }
    }

    private HorizontalStackLayout BuildTabs()
    {
        _headerRow = new HorizontalStackLayout()
        {
            HorizontalOptions = LayoutOptions.Start,
            Spacing = 10,
            Padding = new Thickness(10)
        };

        for (var index = 0; index < Tabs.Count; index++)
        {
            var tab = Tabs[index];
            var index1 = index;
            var tabHeader = new VerticalStackLayout()
            {
                GestureRecognizers =
                {
                    new TapGestureRecognizer()
                    {
                        Command = new Command((() => ActiveTabIndex = index1))
                    }
                }
            };

            var tabHeaderFrame = new Frame()
            {
                BackgroundColor = Colors.SlateGrey,
                CornerRadius = 15f,
                Content = new Label() { Text = tab.Caption, FontAttributes = FontAttributes.Bold, HorizontalOptions = LayoutOptions.Center }
            };

            if (tab.Icon is not null)
                tabHeaderFrame.Content = new Image() { Source = tab.Icon, HorizontalOptions = LayoutOptions.Center, WidthRequest = 30, HeightRequest = 30 };

            if (string.IsNullOrEmpty(tabHeaderFrame.AutomationId))
                tabHeaderFrame.AutomationId = tab.Caption;

            tabHeader.Children.Add(tabHeaderFrame);
            
            _headerRow.Children.Add(tabHeader);
        }

        return _headerRow;
    }

    View? GetActiveTab()
    {
        if (Tabs.Count < ActiveTabIndex || ActiveTabIndex < 0 || !Tabs.Any())
            return null;
        
        var activeTab = Tabs[ActiveTabIndex];

        if (string.IsNullOrEmpty(activeTab.Content.AutomationId))
            activeTab.Content.AutomationId = Tabs[ActiveTabIndex].AutomationId;
        
        return activeTab.Content;
    }

   
}

public static class LayoutExtensions
{
    public static T? FindLayoutChildByAutomationId<T>(this Layout rootLayout, string automationId)
       where T : IView
    {
        return rootLayout.Descendants().OfType<T>()
                         .FirstOrDefault(f => f.AutomationId.EqualsIgnoreCase(automationId));
    }

    public static IEnumerable<IView> Descendants(this Layout layout)
    {
        foreach (var child in layout.Children)
        {
            yield return child;

            if (child is Layout childLayout)
            {
                foreach (var descendant in childLayout.Descendants())
                {
                    yield return descendant;
                }
            }
        }
    }
}
