using System.Buffers;
using System.Text;
using System.Text.Json;
using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Contracts.Tests.Support;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Apogeu.Contracts.Tests.Unit;

/// <summary>
/// SignalR payloads follow the same JSON conventions as the HTTP contracts (SPEC-0006 §6, ADR-0008): the Host's
/// <see cref="JsonHubProtocolOptions"/> carry <c>ContractJson</c>, proven by round-tripping a message through
/// <see cref="JsonHubProtocol"/> built from them.
/// </summary>
public sealed class ContractHubProtocolTests
{
    private const string Target = "FixtureRoundStarted";
    private const byte RecordSeparator = 0x1e;

    private static readonly HubFixturePayload Payload = new(
        new Money(10.5m),
        new Multiplier(2.47m),
        new DateTimeOffset(2026, 10, 8, 1, 0, 0, TimeSpan.FromHours(3)),
        HubFixturePhase.InFlight);

    [Fact(DisplayName = "Dado o protocolo JSON de hubs do Host, quando uma mensagem com Money, Multiplier, data e enum é serializada, então segue as convenções do contrato HTTP")]
    [Trait("Spec", "SPEC-0006:UT-01")]
    public void GivenHostJsonHubProtocolWhenMessageIsWrittenThenPayloadFollowsContractConventions()
    {
        var protocol = HostJsonHubProtocol();

        var json = Write(protocol, new InvocationMessage(Target, [Payload, new Money(3m)]));

        using var document = JsonDocument.Parse(json);
        var arguments = document.RootElement.GetProperty("arguments");
        var payload = arguments[0];
        AssertString(payload, "stake", "10.50", json);
        AssertString(payload, "cashout", "2.47", json);
        AssertString(payload, "startedAt", "2026-10-07T22:00:00.000Z", json);
        AssertString(payload, "phase", "inFlight", json);
        Assert.True(arguments[1].ValueKind == JsonValueKind.String, $"A Money argument must travel as a JSON string: {json}");
        Assert.Equal("3.00", arguments[1].GetString());
    }

    [Fact(DisplayName = "Dado uma mensagem de hub no formato do contrato, quando lida pelo protocolo JSON do Host, então devolve os mesmos valores exatos")]
    [Trait("Spec", "SPEC-0006:UT-01")]
    public void GivenContractShapedHubMessageWhenReadByHostProtocolThenValuesRoundTripExactly()
    {
        var protocol = HostJsonHubProtocol();
        var json = """{"type":1,"target":"FixtureRoundStarted","arguments":[{"stake":"10.50","cashout":"2.47","startedAt":"2026-10-07T22:00:00.000Z","phase":"inFlight"},"3.00"]}""";
        var buffer = new ReadOnlySequence<byte>([.. Encoding.UTF8.GetBytes(json), RecordSeparator]);

        Assert.True(protocol.TryParseMessage(ref buffer, new FixtureBinder(), out var message), "Expected a complete hub message.");

        if (message is InvocationBindingFailureMessage failure)
        {
            Assert.Fail($"The Host's hub protocol could not bind the contract-shaped arguments: {failure.BindingFailure.SourceException.Message}");
        }

        var invocation = Assert.IsType<InvocationMessage>(message);
        Assert.Equal(Payload, Assert.IsType<HubFixturePayload>(invocation.Arguments[0]));
        Assert.Equal(new Money(3m), Assert.IsType<Money>(invocation.Arguments[1]));
    }

    private static JsonHubProtocol HostJsonHubProtocol()
    {
        using var factory = ContractHost.Create("Development");
        var options = factory.Services.GetRequiredService<IOptions<JsonHubProtocolOptions>>();
        return new JsonHubProtocol(options);
    }

    private static string Write(JsonHubProtocol protocol, HubMessage message)
    {
        var buffer = new ArrayBufferWriter<byte>();
        protocol.WriteMessage(message, buffer);
        var written = buffer.WrittenSpan;
        Assert.Equal(RecordSeparator, written[^1]);
        return Encoding.UTF8.GetString(written[..^1]);
    }

    private static void AssertString(JsonElement payload, string property, string expected, string json)
    {
        Assert.True(
            payload.TryGetProperty(property, out var value),
            $"Expected the camelCase property '{property}' in the hub payload: {json}");
        Assert.True(value.ValueKind == JsonValueKind.String, $"'{property}' must travel as a JSON string, got {value.ValueKind}: {json}");
        Assert.Equal(expected, value.GetString());
    }

    /// <summary>Binds the arguments of <c>FixtureRoundStarted</c> as the payload and a Money.</summary>
    private sealed class FixtureBinder : IInvocationBinder
    {
        public IReadOnlyList<Type> GetParameterTypes(string methodName) => [typeof(HubFixturePayload), typeof(Money)];

        public Type GetReturnType(string invocationId) => typeof(object);

        public Type GetStreamItemType(string streamId) => typeof(object);
    }
}
