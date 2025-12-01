var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.PersonalizedFeed_Api>("personalizedfeed-api");

builder.Build().Run();
