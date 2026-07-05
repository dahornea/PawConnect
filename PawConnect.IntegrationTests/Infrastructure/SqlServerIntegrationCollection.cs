namespace PawConnect.IntegrationTests.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SqlServerIntegrationCollection : ICollectionFixture<SqlServerTestcontainerFixture>
{
    public const string Name = "SQL Server integration tests";
}
