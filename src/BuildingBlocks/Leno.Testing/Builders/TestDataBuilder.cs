using Moq;

namespace Leno.Testing.Builders;

public static class TestDataBuilder
{
    public static Mock<T> CreateMock<T>() where T : class
    {
        return new Mock<T>(MockBehavior.Strict);
    }

    public static T CreateMockObject<T>(Action<Mock<T>>? setup = null) where T : class
    {
        var mock = new Mock<T>(MockBehavior.Loose);
        setup?.Invoke(mock);
        return mock.Object;
    }
}