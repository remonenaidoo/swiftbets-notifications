using SwiftBets.BuildingBlocks.Observability;
using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Notifications.Api;
using SwiftBets.Notifications.Application;
using SwiftBets.Notifications.Infrastructure;

if (HealthProbe.TryRun(args) is { } probeExitCode)
{
    return probeExitCode;
}

var builder = WebApplication.CreateBuilder(args);
builder.AddSwiftBetsObservability("swiftbets-notifications");
builder.Services.AddSwiftBetsWeb();
builder.Services.AddSwiftBetsJwtBearer(builder.Configuration);
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(NotificationEndpoints.ReadPermission, p => p.RequireClaim("perm", NotificationEndpoints.ReadPermission));
builder.Services.AddNotificationsApplication();
builder.Services.AddNotificationsInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseSwiftBetsObservability();
app.UseSwiftBetsWeb();
app.UseAuthentication();
app.UseAuthorization();
app.MapSwiftBetsOperationalEndpoints();
app.MapNotificationEndpoints();
app.MapCustomerEndpoints();

await app.RunAsync();
return 0;

public partial class Program;
