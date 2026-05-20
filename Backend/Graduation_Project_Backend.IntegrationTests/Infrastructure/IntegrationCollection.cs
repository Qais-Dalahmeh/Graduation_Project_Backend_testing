using Xunit;

namespace Graduation_Project_Backend.IntegrationTests.Infrastructure;

/// <summary>
/// Shared collection fixture — one PostgreSQL Docker container is started once
/// and shared across ALL integration test classes.
/// This avoids spinning up a new container per test class.
/// </summary>
[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<IntegrationTestFactory> { }
