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

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            await _bus.Start(cancellationToken);
            await base.StartAsync(cancellationToken);
        }

        protected override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            return _bus.Run(cancellationToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            return _bus.Stop(cancellationToken);
        }
    }
}
