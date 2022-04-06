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
                    DateTimeOffset.ParseExact(reader.GetString() ?? throw new InvalidOperationException(),
                        "MM/dd/yyyy", CultureInfo.InvariantCulture);

            public override void Write(
                Utf8JsonWriter writer,
                DateTimeOffset dateTimeValue,
                JsonSerializerOptions options) =>
                    writer.WriteStringValue(dateTimeValue.ToString(
                        "MM/dd/yyyy", CultureInfo.InvariantCulture));
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

            var serializer = host.Services.GetRequiredService<IMessageSerializerFactory>().CreateMessageSerializer();

            var json = Encoding.UTF8.GetString(serializer.Serialize(new WeatherForecast()));
            var expected = @"{
  ""Date"": ""12/31/2021"",
  ""TemperatureCelsius"": 12,
  ""Summary"": ""Hot""
}".ReplaceLineEndings();

            json.Should().Be(expected);
        }
    }
}
