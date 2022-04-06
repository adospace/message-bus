using Microsoft.Extensions.Hosting;

namespace MessageBus.Implementation
{
    internal class BusService : BackgroundService
    {
        private readonly IBus _bus;

        public BusService(IBus bus)
        {
            _bus = bus;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return _bus.Run(stoppingToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            return _bus.Stop(cancellationToken);
        }
    }
}
