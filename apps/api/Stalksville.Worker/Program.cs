using Stalksville.Application;
using Stalksville.Infrastructure;
using Stalksville.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddStalksvilleInfrastructure(builder.Configuration);
builder.Services.AddStalksvilleApplication();
builder.Services.AddHostedService<AdaptiveRefreshWorker>();
builder.Services.AddHostedService<ScheduledScanWorker>();

var host = builder.Build();
host.Run();
