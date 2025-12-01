using Azure.Messaging.ServiceBus;
using PersonalizedFeed.Domain.Ranking;
using PersonalizedFeed.Domain.Services;
using PersonalizedFeed.Infrastructure;
using PersonalizedFeed.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Domain services
builder.Services.AddScoped<IFeatureExtractor, SimpleFeatureExtractor>();
builder.Services.AddScoped<IRankingModel, LinearRankingModel>();
builder.Services.AddScoped<IFeedDiversifier, SimpleFeedDiversifier>();
builder.Services.AddScoped<IRanker, Ranker>();
builder.Services.AddScoped<IFeedService, FeedService>();
builder.Services.AddScoped<IUserEventIngestionService, UserEventIngestionService>();

// Infrastructure (still in-memory; later swap to real DB/Redis)
builder.Services.AddInMemoryInfrastructure();

// Messaging
const string PlaceholderServiceBusConnectionString = "Endpoint=sb://placeholder.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=PLACEHOLDER";
builder.Services.AddSingleton(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("ServiceBus")
        ?? PlaceholderServiceBusConnectionString;
    return new ServiceBusClient(connectionString);
});

builder.Services.AddHostedService<UserEventsWorker>();

var host = builder.Build();
host.Run();
