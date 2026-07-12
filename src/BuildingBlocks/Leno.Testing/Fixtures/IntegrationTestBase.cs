namespace Leno.Testing.Fixtures;

[Collection(ContainerCollection.Name)]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected ContainerFixture Containers { get; }

    protected IntegrationTestBase(ContainerFixture fixture)
    {
        Containers = fixture;
    }

    public virtual Task InitializeAsync() => Task.CompletedTask;

    public virtual Task DisposeAsync() => Task.CompletedTask;
}