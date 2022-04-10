namespace MessageBus
{
    public interface IBus
    {
        Task Start(CancellationToken cancellationToken = default);

        Task Run(CancellationToken cancellationToken = default);

        Task Stop(CancellationToken cancellationToken = default);
    }

}