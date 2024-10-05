namespace Arisoul.T212.App;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute(nameof(OpenPositionsDetailPage), typeof(OpenPositionsDetailPage));
    }
}
