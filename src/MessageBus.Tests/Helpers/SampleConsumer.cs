using System.Threading;
using System.Threading.Tasks;

namespace MessageBus.Tests.Helpers
{
    public class SampleConsumer : 
        IHandler<SampleModel, SampleModelReply>,
        IHandler<SampleModelThatRaisesException>,
        IHandler<SampleModelDoNotDeserialize>,
        IHandler<SampleModelPublished>
    {
        private readonly IMessageContextProvider _messageContextProvider;

        public AutoResetEvent HandleCalled { get; } = new AutoResetEvent(false);
        public int HandleCallCount { get; private set; }

        public SampleConsumer(IMessageContextProvider messageContextProvider)
        {
            _messageContextProvider = messageContextProvider;
        }

        public Task<SampleModelReply> Handle(SampleModel message, CancellationToken cancellationToken = default)
        {
            HandleCallCount++;

            _messageContextProvider.Context.SetValue("HandleCallCount", HandleCallCount);

            HandleCalled.Set();
            return Task.FromResult(new SampleModelReply($"Hello {message.Name} {message.Surname}!"));
        }

        public Task Handle(SampleModelThatRaisesException message, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public Task Handle(SampleModelDoNotDeserialize message, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task Handle(SampleModelPublished message, CancellationToken cancellationToken = default)
        {
            HandleCallCount++;

            _messageContextProvider.Context.SetValue("HandleCallCount", HandleCallCount);

            HandleCalled.Set();
            return Task.CompletedTask;
        }
    }
}