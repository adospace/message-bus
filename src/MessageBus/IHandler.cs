namespace MessageBus
{
    public interface IHandler
    { 
    }

    public interface IHandler<in T> : IHandler where T : class
    {
        Task Handle(T message, CancellationToken cancellationToken = default);
    }

    public interface IHandler<in T, TRType> : IHandler where T : class
    {
        Task<TRType> Handle(T message, CancellationToken cancellationToken = default);
    }

}