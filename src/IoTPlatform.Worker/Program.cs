using IoTPlatform.Worker;
using IoTPlatform.Infrastructure.Configuration;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddHiveMqOptions(builder.Configuration);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
