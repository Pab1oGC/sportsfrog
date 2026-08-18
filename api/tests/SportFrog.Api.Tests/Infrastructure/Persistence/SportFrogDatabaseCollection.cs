namespace SportFrog.Api.Tests.Infrastructure.Persistence;

/// <summary>
/// Groups every test class that needs the real database. xUnit runs classes
/// in the same collection sequentially against one shared
/// <see cref="SportFrogDatabaseFixture"/>, instead of one container per class.
/// </summary>
[CollectionDefinition(nameof(SportFrogDatabaseCollection))]
public sealed class SportFrogDatabaseCollection : ICollectionFixture<SportFrogDatabaseFixture>;
