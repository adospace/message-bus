using System.Linq;
using MessageBus.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using System;
using MessageBus.RabbitMQ;
using MessageBus.Serializer.Json;

namespace MessageBus.Tests
{
    [TestClass]
    public class TestingFrameworkTests
    {
        [TestMethod]
        public async Task SendAndReceiveMessage()
        {
            string appId = Guid.NewGuid().ToString();
            using var clientHost = Host.CreateDefaultBuilder()
                .AddMessageBus(cfg =>
                {
                    cfg.UseRabbitMQ(conf => conf.ApplicationId = appId);
                    cfg.UseJsonSerializer();
                })
                .Build();

            using var consumerHost = Host.CreateDefaultBuilder()
                .AddMessageBus(cfg =>
                {
                    cfg.UseRabbitMQ(conf => conf.ApplicationId = appId);
                    cfg.UseJsonSerializer();
                    cfg.AddHandler<SampleConsumer, SampleModel, SampleModelReply>();
                })
                .Build();

            await clientHost.StartAsync();
            await consumerHost.StartAsync();

            var messageContextProvider = clientHost.Services.GetRequiredService<IMessageContextProvider>();

            var busClient = clientHost.Services.GetRequiredService<IBusClient>();
            var reply = await busClient.SendAndGetReply<SampleModel, SampleModelReply>(new SampleModel("John", "Smith"));

            Assert.AreEqual("Hello John Smith!", reply.NameAndSurname);

            messageContextProvider.Context.TryGetValue<int>("HandleCallCount", out var handleCallCount).Should().BeTrue();
            handleCallCount.Should().Be(1);
        }

        //[TestMethod]
        //public async Task SendAndReceiveMessageWithMultipleConsumers()
        //{
        //    using var serverHost = Host.CreateDefaultBuilder()
        //        .AddMessageBoxInMemoryServer()
        //        .Build();

        //    using var clientHost = Host.CreateDefaultBuilder()
        //        .AddMessageBoxInMemoryClient()
        //        .AddJsonSerializer()
        //        .Build();

        //    var consumer1 = new SampleConsumer();
        //    using var consumerHost1 = Host.CreateDefaultBuilder()
        //        .AddMessageBoxInMemoryClient()
        //        .AddJsonSerializer()
        //        .AddConsumer(consumer1)
        //        .Build();

        //    var consumer2 = new SampleConsumer();
        //    using var consumerHost2 = Host.CreateDefaultBuilder()
        //        .AddMessageBoxInMemoryClient()
        //        .AddJsonSerializer()
        //        .AddConsumer(consumer2)
        //        .Build();

        //    await serverHost.StartAsync();
        //    await clientHost.StartAsync();
        //    await consumerHost1.StartAsync();
        //    await consumerHost2.StartAsync();

        //    var busClient = clientHost.Services.GetRequiredService<IBusClient>();
        //    var reply = await busClient.SendAndGetReply<SampleModelReply>(new SampleModel("John", "Smith"));

        //    Assert.AreEqual("Hello John Smith!", reply.NameAndSurname);

        //    WaitHandle.WaitAny(new WaitHandle[] { consumer1.HandleCalled, consumer2.HandleCalled });
        //}

        [TestMethod]
        public async Task PublishEventMessageWithMultipleConsumers()
        {
            using var clientHost = Host.CreateDefaultBuilder()
                .AddMessageBus(cfg =>
                {
                    cfg.UseRabbitMQ();
                    cfg.UseJsonSerializer();
                })
                .Build();

            using var consumerHost1 = Host.CreateDefaultBuilder()
                .AddMessageBus(cfg =>
                {
                    cfg.UseRabbitMQ();
                    cfg.UseJsonSerializer();
                    cfg.AddEventHandler<SampleConsumer, SampleModelPublished>(serviceLifetime: ServiceLifetime.Singleton);
                })
                .ConfigureServices(services =>
                    services.AddSingleton<IHandler<SampleModelPublished>>(sp => sp.GetRequiredService<SampleConsumer>()))
                .ConfigureServices(services =>
                    services.AddSingleton<SampleConsumer>())
                .Build();

            using var consumerHost2 = Host.CreateDefaultBuilder()
                .AddMessageBus(cfg =>
                {
                    cfg.UseRabbitMQ();
                    cfg.UseJsonSerializer();
                    cfg.AddEventHandler<SampleConsumer, SampleModelPublished>(serviceLifetime: ServiceLifetime.Singleton);
                })
                .ConfigureServices(services => 
                    services.AddSingleton<IHandler<SampleModelPublished>>())
                .ConfigureServices(services =>
                    services.AddSingleton<SampleConsumer>())
                .Build();

            await clientHost.StartAsync();
            await consumerHost1.StartAsync();
            await consumerHost2.StartAsync();

            var busClient = clientHost.Services.GetRequiredService<IBusClient>();

            busClient.Publish(new SampleModelPublished());

            var consumer1 = consumerHost1.Services.GetRequiredService<SampleConsumer>();
            var consumer2 = consumerHost2.Services.GetRequiredService<SampleConsumer>();

            foreach (var ev in new WaitHandle[] { consumer1.HandleCalled, consumer2.HandleCalled })
                ev.WaitOne(TimeSpan.FromSeconds(10)).Should().BeTrue();

            consumer1.HandleCallCount.Should().Be(1);
            consumer2.HandleCallCount.Should().Be(1);
        }

        //[TestMethod]
        //public async Task SendMessageWithMultipleConsumers()
        //{
        //    using var serverHost = Host.CreateDefaultBuilder()
        //        .AddMessageBoxInMemoryServer()
        //        .Build();

        //    using var clientHost = Host.CreateDefaultBuilder()
        //        .AddMessageBoxInMemoryClient()
        //        .AddJsonSerializer()
        //        .Build();

        //    var consumer1 = new SampleConsumer();
        //    using var consumerHost1 = Host.CreateDefaultBuilder()
        //        .AddMessageBoxInMemoryClient()
        //        .AddJsonSerializer()
        //        .AddConsumer(consumer1)
        //        .Build();

        //    var consumer2 = new SampleConsumer();
        //    using var consumerHost2 = Host.CreateDefaultBuilder()
        //        .AddMessageBoxInMemoryClient()
        //        .AddJsonSerializer()
        //        .AddConsumer(consumer2)
        //        .Build();

        //    await serverHost.StartAsync();
        //    await clientHost.StartAsync();
        //    await consumerHost1.StartAsync();
        //    await consumerHost2.StartAsync();

        //    var busClient = clientHost.Services.GetRequiredService<IBusClient>();
        //    await busClient.Send(new SampleModel("John", "Smith"));

        //    WaitHandle.WaitAny(new WaitHandle[] { consumer1.HandleCalled, consumer2.HandleCalled });
        //}

        //[TestMethod]
        //public async Task SendAndReceiveMessageWhenConsumerIsAvailable()
        //{
        //    using var serverHost = Host.CreateDefaultBuilder()
        //        .AddMessageBoxInMemoryServer()
        //        .Build();

        //    using var clientHost = Host.CreateDefaultBuilder()
        //        .AddMessageBoxInMemoryClient()
        //        .AddJsonSerializer()
        //        .Build();

        //    await serverHost.StartAsync();
        //    await clientHost.StartAsync();

        //    var busClient = clientHost.Services.GetRequiredService<IBusClient>();
        //    var replyTask = busClient.SendAndGetReply<SampleModelReply>(new SampleModel("John", "Smith"));
        //    var startConsumerHostTask = Task.Run(async () =>
        //    {
        //        using var consumerHost = Host.CreateDefaultBuilder()
        //            .AddMessageBoxInMemoryClient()
        //            .AddJsonSerializer()
        //            .AddConsumer<SampleConsumer>()
        //            .Build();

        //        await Task.Delay(2000);

        //        await consumerHost.StartAsync();

        //        await Task.Delay(2000);
        //    });

        //    Task.WaitAll(replyTask, startConsumerHostTask);

        //    Assert.AreEqual("Hello John Smith!", replyTask.Result.NameAndSurname);
        //}

        [TestMethod]
        public async Task SendAndConsumerThrowsException()
        {
            using var clientHost = Host.CreateDefaultBuilder()
                .AddMessageBus(cfg =>
                {
                    cfg.UseRabbitMQ();
                    cfg.UseJsonSerializer();
                })
                .Build();

            using var consumerHost = Host.CreateDefaultBuilder()
                .AddMessageBus(cfg =>
                {
                    cfg.UseRabbitMQ();
                    cfg.UseJsonSerializer();
                    cfg.AddHandler<SampleConsumer, SampleModelThatRaisesException>();
                })
                .Build();

            await clientHost.StartAsync();
            await consumerHost.StartAsync();

            var busClient = clientHost.Services.GetRequiredService<IBusClient>();
            await Assert.ThrowsExceptionAsync<MessageBoxCallException>(() => busClient.Send(new SampleModelThatRaisesException()));
        }

        [TestMethod]
        public async Task SendAndConsumerIsUnableToDeserializeInputModel()
        {
            using var clientHost = Host.CreateDefaultBuilder()
                .AddMessageBus(cfg =>
                {
                    cfg.UseRabbitMQ();
                    cfg.UseJsonSerializer();
                })
                .Build();

            using var consumerHost = Host.CreateDefaultBuilder()
                .AddMessageBus(cfg =>
                {
                    cfg.UseRabbitMQ();
                    cfg.UseJsonSerializer();
                    cfg.AddHandler<SampleConsumer, SampleModelDoNotDeserialize>();
                })                
                .Build();

            await clientHost.StartAsync();
            await consumerHost.StartAsync();

            var busClient = clientHost.Services.GetRequiredService<IBusClient>();

            await Assert.ThrowsExceptionAsync<MessageBoxCallException>(() => busClient.Send(new SampleModelDoNotDeserialize(0)));
        }
    }
}