using Arisoul.Core.Maui;
using Arisoul.T212.App.Storage;
using Arisoul.T212.Client;

namespace Arisoul.T212.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.UseArisoulMaui()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("FontAwesome6FreeBrands.otf", "FontAwesomeBrands");
				fonts.AddFont("FontAwesome6FreeRegular.otf", "FontAwesomeRegular");
				fonts.AddFont("FontAwesome6FreeSolid.otf", "FontAwesomeSolid");
			});

		builder.Services.AddT212Client<T212ClientOptions>();

        builder.Services.AddSingleton<MainViewModel>();
		builder.Services.AddSingleton<MainPage>();

		builder.Services.AddTransient<OpenPositionsDetailViewModel>();
		builder.Services.AddTransient<OpenPositionsDetailPage>();

		builder.Services.AddSingleton<OpenPositionsViewModel>();
		builder.Services.AddSingleton<OpenPositionsPage>();

        builder.Services.AddSingleton<SettingsViewModel>();
        builder.Services.AddSingleton<SettingsPage>();

        builder.Services.AddTransient<SampleDataService>();

		builder.Services.AddSingleton<ApplicationDbContext>();

		return builder.Build();
	}
}
