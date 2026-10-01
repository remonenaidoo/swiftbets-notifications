extern alias migrator;
using System.Security.Claims;
using System.Security.Cryptography;
using Dapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using SwiftBets.BuildingBlocks.Testing;
using SwiftBets.Contracts.Messaging;

namespace SwiftBets.Notifications.IntegrationTests;

/// <summary>The real host against a fresh database, a unique Kafka environment and the fake SMTP server.</summary>
internal sealed class NotificationsHost : WebApplicationFactory<Program>
{
    private const string Issuer = "https://identity.notifications.test";
    private static readonly RsaSecurityKey Key = new(RSA.Create(2048)) { KeyId = "notifications-test" };

    private NotificationsHost(string connectionString, string bootstrap, string environment, int smtpPort)
    {
        ConnectionString = connectionString;
        Bootstrap = bootstrap;
        Environment = environment;
        SmtpPort = smtpPort;
    }

    public string ConnectionString { get; }

    public string Bootstrap { get; }

    public string Environment { get; }

    public int SmtpPort { get; }

    public static async Task<NotificationsHost> StartAsync(PostgresFixture postgres, RedpandaFixture redpanda, int smtpPort)
    {
        var name = "notif_" + Guid.NewGuid().ToString("N")[..10];
        await using (var server = new NpgsqlConnection(postgres.ConnectionString))
        {
            await server.ExecuteAsync($"CREATE DATABASE {name}");
        }

        var connectionString = new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Database = name }.ConnectionString;
        (await MigrateAsync(connectionString)).ShouldBe(0);

        var environment = "t" + Guid.NewGuid().ToString("N")[..10];
        foreach (var topic in new[] { Topics.NotificationRequested, Topics.AccountStatusChanged })
        {
            var full = TopicName.For(topic, environment);
            await redpanda.CreateTopicsAsync(1, full.Value, full.DeadLetter().Value);
        }

        var host = new NotificationsHost(connectionString, redpanda.BootstrapServers, environment, smtpPort);
        _ = host.Server;
        return host;
    }

    public static async Task<int> MigrateAsync(string connectionString, params string[] extra)
    {
        var entryPoint = typeof(migrator::Program).Assembly.EntryPoint!;
        var result = entryPoint.Invoke(null, [new[] { $"--ConnectionStrings:SbNotifications={connectionString}" }.Concat(extra).ToArray()]);
        return result is Task<int> task ? await task : (int)result!;
    }

    public static string StaffToken(params string[] permissions)
    {
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()) };
        claims.AddRange(permissions.Select(p => new Claim("perm", p)));
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = "swiftbets",
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = new SigningCredentials(Key, SecurityAlgorithms.RsaSha256),
        });
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:SbNotifications", ConnectionString);
        builder.UseSetting("Kafka:BootstrapServers", Bootstrap);
        builder.UseSetting("Kafka:Environment", Environment);
        builder.UseSetting("Kafka:ClientId", "notifications-tests");
        builder.UseSetting("Email:SmtpHost", "127.0.0.1");
        builder.UseSetting("Email:SmtpPort", SmtpPort.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("Jwt:Authority", Issuer);
        builder.UseSetting("Jwt:RequireHttpsMetadata", "false");
        builder.ConfigureServices(services => services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Authority = null;
            var configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
            configuration.SigningKeys.Add(Key);
            options.Configuration = configuration;
            options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
            options.TokenValidationParameters.ValidIssuer = Issuer;
            options.TokenValidationParameters.ValidAudience = "swiftbets";
        }));
    }
}
