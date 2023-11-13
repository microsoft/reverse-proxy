var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
#if NET8_0_OR_GREATER
builder.Services.AddRequestTimeouts(options =>
{
    options.AddPolicy("customPolicy", TimeSpan.FromSeconds(20));
});
#endif
var app = builder.Build();
#if NET8_0_OR_GREATER
app.UseRequestTimeouts();
#endif
app.MapReverseProxy();

app.Run();
