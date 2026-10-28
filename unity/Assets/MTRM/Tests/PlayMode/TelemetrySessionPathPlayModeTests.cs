using NUnit.Framework;
using UnityEngine;

public sealed class TelemetrySessionPathPlayModeTests
{
    [Test]
    public void ResolveSessionPath_DoesNotDuplicateProductName()
    {
        string path = ExperimentSessionPath.Resolve(Application.persistentDataPath, "P_TEST");

        Assert.That(path, Does.EndWith(System.IO.Path.Combine("Experiments", "P_TEST")));
        Assert.That(path, Does.Not.Contain(System.IO.Path.Combine("MTRM", "MTRM")));
    }
}
