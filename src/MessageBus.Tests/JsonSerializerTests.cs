using FluentAssertions;
using MessageBus;
using MessageBus.Serializer.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MessageBus.Tests
{
    [TestClass]
    public class JsonSerializerTests
    {
        private class DateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
        {
            public override DateTimeOffset Read(
                ref Utf8JsonReader reader,
                Type typeToConvert,
                JsonSerializerOptions options) =>
                    DateTimeOffset.Parse(reader.GetString() ?? throw new InvalidOperationException(),
                        CultureInfo.InvariantCulture);

            public override void Write(
                Utf8JsonWriter writer,
                DateTimeOffset dateTimeValue,
                JsonSerializerOptions options) =>
                    writer.WriteStringValue(dateTimeValue.ToString(CultureInfo.InvariantCulture));
        }
        private class WeatherForecast
        {
            public DateTimeOffset Date { get; set; } = new DateTimeOffset(2021, 12, 31, 20, 0, 0, TimeSpan.Zero);
            public int TemperatureCelsius { get; set; } = 12;
            public string Summary { get; set; } = "Hot";
        }

        [TestMethod]
        public void TestJsonSettings()
        {
            var host = Host.CreateDefaultBuilder()
                .AddMessageBus(config =>  config.UseJsonSerializer()
                    .ConfigureJsonSerializer(cfg => cfg.Converters.Add(new DateTimeOffsetJsonConverter()))
                    .ConfigureJsonSerializer(cfg => cfg.WriteIndented = true))
                .Build();

            var serializer = host.Services.GetRequiredService<IMessageSerializerFactory>()
                .CreateMessageSerializer();
            var messageContextProvider = host.Services.GetRequiredService<IMessageContextProvider>();

            var model = new WeatherForecast();
            var serialized = serializer.Serialize(new Message(model, messageContextProvider.Context));
            var deserializedMessage = serializer.Deserialize(serialized, typeof(WeatherForecast));
            Assert.IsNotNull(deserializedMessage);

            var deserializedModel = deserializedMessage.Model;
            Assert.IsNotNull(deserializedModel);

            deserializedModel.Should().BeEquivalentTo(model);
        }
    }
}
