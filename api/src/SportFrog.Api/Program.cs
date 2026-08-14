var builder = WebApplication.CreateBuilder(args);

// The connection string is only read here to fail at startup if it's
// missing. The DbContext isn't registered in the container yet: it's
// resolved via IDesignTimeDbContextFactory for the EF tools.
_ = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "Missing connection string 'ConnectionStrings:Default'.");

var app = builder.Build();

app.MapGet("/health", () => Results.Ok());

app.Run();

// Needed so WebApplicationFactory<Program> can reach the generated class.
public partial class Program;
