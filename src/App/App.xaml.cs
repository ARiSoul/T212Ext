namespace Arisoul.T212.App;

public partial class App : Application
{
    public App(BackgroundSyncService backgroundSyncService)
    {
        InitializeComponent();

        Task.Run(async () =>
        {
            try
            {
                await backgroundSyncService.SyncDividendsAsync().ConfigureAwait(false);
                // await backgroundSyncService.SyncOrdersAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        });

        MainPage = new AppShell();
    }
}
