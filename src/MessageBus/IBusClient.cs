namespace MessageBus
{
    public interface IBusClient
    {
        Task Publish<T>(T model, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task Send<T>(T model, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<TReply> SendAndGetReply<T, TReply>(T model, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
    }

    public interface IBusConfigurator
    {
        void AddRequestClient<T>();

        void AddConsumer<T>();
    }

}