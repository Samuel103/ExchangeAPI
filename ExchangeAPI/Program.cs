using ExchangeAPI.DependencyInjection;
using ExchangeAPI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddExchangeApi(builder.Configuration);

var app = builder.Build();

app.Services.GetRequiredService<IDynamicApiServer>().MapEndpoints(app);

app.Run();
