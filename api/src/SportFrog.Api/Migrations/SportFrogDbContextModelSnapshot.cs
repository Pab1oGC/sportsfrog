using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Deliberately empty snapshot: the context declares no <c>DbSet</c> and the
/// schema is defined by the migrations' SQL. Exists so the EF Core tools have
/// a starting point once entities are added.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
public partial class SportFrogDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.11");
    }
}
