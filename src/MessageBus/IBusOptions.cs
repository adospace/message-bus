namespace MessageBus
{
    public interface IBusOptions
    {
        string? ApplicationId { get; }

        TimeSpan DefaultCallTimeout { get; }

        int MaxDegreeOfParallelism { get; }
    }

}