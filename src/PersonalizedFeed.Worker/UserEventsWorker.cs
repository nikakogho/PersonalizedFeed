using Azure.Messaging.ServiceBus;
using PersonalizedFeed.Domain.Events;
using PersonalizedFeed.Domain.Services;
using System.Text.Json;

namespace PersonalizedFeed.Worker;

public sealed class UserEventsWorker : BackgroundService
{
    private readonly ServiceBusProcessor _processor;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UserEventsWorker> _logger;

    public UserEventsWorker(
        ServiceBusClient client,
        IServiceScopeFactory scopeFactory,
        ILogger<UserEventsWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        _processor = client.CreateProcessor(
            queueName: "user-events",
            new ServiceBusProcessorOptions
            {
                MaxConcurrentCalls = 4,
                AutoCompleteMessages = false
            });

        _processor.ProcessMessageAsync += HandleMessageAsync;
        _processor.ProcessErrorAsync += HandleErrorAsync;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("UserEventsWorker starting Service Bus processing.");

        await _processor.StartProcessingAsync(stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            // normal shutdown
        }

        _logger.LogInformation("UserEventsWorker stopping Service Bus processing.");
        await _processor.StopProcessingAsync(CancellationToken.None);
    }

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        try
        {
            var json = args.Message.Body.ToString();

            var batch = JsonSerializer.Deserialize<UserEventBatch>(json);

            if (batch is null)
            {
                _logger.LogWarning(
                    "Received null or invalid UserEventBatch. MessageId={MessageId}",
                    args.Message.MessageId);

                await args.DeadLetterMessageAsync(
                    args.Message,
                    "DeserializationFailed",
                    "UserEventBatch was null or invalid.");
                return;
            }

            using (var scope = _scopeFactory.CreateScope())
            {
                var ingestionService = scope.ServiceProvider
                    .GetRequiredService<IUserEventIngestionService>();

                await ingestionService.IngestAsync(batch, args.CancellationToken);
            }

            await args.CompleteMessageAsync(args.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing message. MessageId={MessageId}",
                args.Message.MessageId);

            await args.AbandonMessageAsync(args.Message);
        }
    }

    private Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(
            args.Exception,
            "ServiceBus processing error. EntityPath={EntityPath}, Namespace={Namespace}",
            args.EntityPath,
            args.FullyQualifiedNamespace);

        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _processor.CloseAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
