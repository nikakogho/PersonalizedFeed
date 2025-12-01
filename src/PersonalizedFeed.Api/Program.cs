using Azure.Messaging.ServiceBus;
using PersonalizedFeed.Api.Messaging;
using PersonalizedFeed.Domain.Ranking;
using PersonalizedFeed.Domain.Services;
using PersonalizedFeed.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Domain services
builder.Services.AddScoped<IFeedService, FeedService>();
builder.Services.AddScoped<IUserEventIngestionService, UserEventIngestionService>();

// Ranking components
builder.Services.AddSingleton<IFeatureExtractor, SimpleFeatureExtractor>();
builder.Services.AddSingleton<IRankingModel, LinearRankingModel>();
builder.Services.AddSingleton<IFeedDiversifier, SimpleFeedDiversifier>();
builder.Services.AddSingleton<IRanker, Ranker>();

// Infrastructure
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

builder.Services.AddScoped<IUserEventSink, InlineUserEventSink>(); // local
// builder.Services.AddScoped<IUserEventSink, ServiceBusUserEventSink>(); // production

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
