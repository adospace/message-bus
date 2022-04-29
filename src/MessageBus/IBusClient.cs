namespace MessageBus
{
    public interface IBusClient
    {
        void Publish<T>(T model);

        Task Send<T>(T model, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<TReply> SendAndGetReply<T, TReply>(T model, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
    }

    public interface IBusConfigurator
    {
        void AddRequestClient<T>();

        void AddConsumer<T>();
    }

}