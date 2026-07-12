namespace Leno.Testing.Fixtures;

[CollectionDefinition(Name)]
public sealed class ContainerCollection : ICollectionFixture<ContainerFixture>
{
    public const string Name = "ContainerCollection";
}