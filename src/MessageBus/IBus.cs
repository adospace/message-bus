namespace MessageBus
{
    public interface IBus
    {
        Task Run(CancellationToken cancellationToken = default);

        Task Stop(CancellationToken cancellationToken = default);
    }

}