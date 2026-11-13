namespace NIOP_Monitoring.Agent
{
    public class CatalogLoader(CatalogService catalog) : IHostedService
    {
        public Task StartAsync(CancellationToken ct) => catalog.LoadAsync(ct);
        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
    }
}