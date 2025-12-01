var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.PersonalizedFeed_Api>("personalizedfeed-api");

builder.AddProject<Projects.PersonalizedFeed_Worker>("personalizedfeed-worker");

builder.Build().Run();
