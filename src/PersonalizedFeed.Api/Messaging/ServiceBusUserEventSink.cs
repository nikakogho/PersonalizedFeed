using Azure.Messaging.ServiceBus;
using PersonalizedFeed.Domain.Events;
using System.Text.Json;

namespace PersonalizedFeed.Api.Messaging;

public sealed class ServiceBusUserEventSink : IUserEventSink
{
    private readonly ServiceBusSender _sender;

    public ServiceBusUserEventSink(ServiceBusClient client)
    {
        _sender = client.CreateSender("user-events");
    }

    public async Task HandleAsync(
        UserEventBatch batch,
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(batch);
        var message = new ServiceBusMessage(json);

        await _sender.SendMessageAsync(message, cancellationToken);
    }
}
