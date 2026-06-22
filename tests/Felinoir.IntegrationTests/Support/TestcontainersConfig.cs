using System.Runtime.CompilerServices;

namespace Felinoir.IntegrationTests.Support;

internal static class TestcontainersConfig
{
    /// <summary>
    /// Runs before any test code. Disables the Testcontainers "Ryuk" resource reaper
    /// unless the environment already set the flag. Ryuk is a helper container that is
    /// (a) pulled from Docker Hub — unreachable here (no/expired login) — and (b) runs
    /// on Docker's bridge network, which Calico CNI blocks on this host. The Postgres
    /// image is local and <c>PostgresFixture</c> disposes its container explicitly, so
    /// the reaper isn't needed. CI can re-enable it via TESTCONTAINERS_RYUK_DISABLED=false.
    /// </summary>
    [ModuleInitializer]
    public static void Init()
    {
        const string key = "TESTCONTAINERS_RYUK_DISABLED";
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            Environment.SetEnvironmentVariable(key, "true");
    }
}
