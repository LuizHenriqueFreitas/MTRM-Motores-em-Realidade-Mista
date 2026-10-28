using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public sealed class TelemetryAssemblyBoundaryTests
{
    [Serializable]
    private sealed class AssemblyDefinition
    {
        public string[] references;
    }

    [Test]
    public void TelemetryAsmdef_DoesNotReferenceAnyMtrmAssembly()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string path = Path.Combine(
            projectRoot,
            "Assets",
            "MTRM",
            "Scripts",
            "Telemetry",
            "MTRM.Telemetry.asmdef");
        AssemblyDefinition definition = JsonUtility.FromJson<AssemblyDefinition>(File.ReadAllText(path));

        Assert.That(definition.references, Is.Not.Null);
        Assert.That(
            Array.Exists(definition.references, reference => reference.StartsWith("MTRM.", StringComparison.Ordinal)),
            Is.False,
            "MTRM.Telemetry deve permanecer uma assembly-folha sem referências a assemblies MTRM.");
    }

    [Test]
    public void TelemetryAssembly_HasNoSceneBoundUnityTypes()
    {
        foreach (Type type in typeof(ExperimentEventRecorder).Assembly.GetTypes())
        {
            Assert.That(
                typeof(MonoBehaviour).IsAssignableFrom(type),
                Is.False,
                $"{type.FullName} não pode ser MonoBehaviour.");
            Assert.That(
                typeof(ScriptableObject).IsAssignableFrom(type),
                Is.False,
                $"{type.FullName} não pode ser ScriptableObject.");
        }
    }
}
