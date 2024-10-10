namespace Arisoul.T212.App.Controls;

public partial class CustomTab : View
{
    public static readonly BindableProperty CaptionProperty = BindableProperty.Create(
        nameof(Caption),
        typeof(string),
        typeof(CustomTab),
        default(string),
        propertyChanged: OnTitleChanged());

    private static BindableProperty.BindingPropertyChangedDelegate OnTitleChanged()
    {
        return (bindable, oldValue, newValue) =>
        {
            if (bindable is CustomTab tab)
                tab.AutomationId = newValue.ToString();
        };
    }

    public string Caption
    {
        get => (string)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    public static readonly BindableProperty IconProperty = BindableProperty.Create(
      nameof(Icon),
      typeof(ImageSource),
      typeof(CustomTab),
      default(ImageSource));

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public static readonly BindableProperty ContentProperty = BindableProperty.Create(
        nameof(Content),
        typeof(View),
        typeof(CustomTab),
        new ContentView());

    public View Content
    {
        get => (View)GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }
}
