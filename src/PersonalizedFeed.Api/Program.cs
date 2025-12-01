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

// Ranking components
builder.Services.AddSingleton<IFeatureExtractor, SimpleFeatureExtractor>();
builder.Services.AddSingleton<IRankingModel, LinearRankingModel>();
builder.Services.AddSingleton<IFeedDiversifier, SimpleFeedDiversifier>();
builder.Services.AddSingleton<IRanker, Ranker>();

// Infrastructure
builder.Services.AddInMemoryInfrastructure();

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
