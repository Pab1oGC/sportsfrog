using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "Missing connection string 'ConnectionStrings:Default'.");

// The application connects as sportfrog_app: it can read and write data but
// cannot modify the schema or bypass the isolation policies. Migrations are
// applied out of band, by the schema owner.
builder.Services.AddSingleton(SportFrogDataSource.Create(connectionString));
builder.Services.AddDbContext<SportFrogDbContext>((services, options) =>
    options.UseNpgsql(
        services.GetRequiredService<Npgsql.NpgsqlDataSource>(),
        SportFrogDataSource.MapEnums));

var app = builder.Build();

app.MapGet("/health", () => Results.Ok());

app.Run();

// Needed so WebApplicationFactory<Program> can reach the generated class.
public partial class Program;
