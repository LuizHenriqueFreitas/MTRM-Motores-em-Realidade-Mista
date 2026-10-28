using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class PistonPhaseDiagnosticPlayModeTests
{
    private const string PrefabPath = "MTRM/Prefabs/Engine/StateEngineManager";
    private const float DiagnosticRpm = 60f;
    private const float CyclesToObserve = 3f;

    private sealed class Sample
    {
        public float CrankAngle;
        public float[] Heights;
    }

    [UnityTest]
    public IEnumerator PistonsFollowExpectedFlatPlaneCrankPairing()
    {
        GameObject prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/MTRM/Prefabs/Engine/StateEngineManager.prefab");
        }

        Assert.That(prefab, Is.Not.Null, "Engine stimulus prefab was not found.");

        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        instance.name = "PistonPhaseDiagnostic";

        yield return null;

        try
        {
            ComponentTag[] representationTags = instance.GetComponentsInChildren<ComponentTag>(true)
                .Where(tag => tag.Representation == EngineRepresentation.Expande)
                .ToArray();
            Pistao[] unorderedPistons = representationTags
                .Where(tag => tag.Id == EngineComponentId.Pistao)
                .Select(tag => tag.GetComponentInParent<Pistao>(true))
                .Where(piston => piston != null)
                .Distinct()
                .ToArray();
            Assert.That(unorderedPistons, Has.Length.EqualTo(4));

            Pistao[] pistons = OrderPistonsFromTimingEnd(representationTags, unorderedPistons);
            var samples = new List<Sample>();
            const float sampleStepDegrees = 6f;
            float targetAngle = 720f * CyclesToObserve;
            for (float crankAngle = 0f; crankAngle < targetAngle; crankAngle += sampleStepDegrees)
            {
                foreach (Pistao piston in pistons)
                    piston.AtualizarEstado(crankAngle, DiagnosticRpm);

                samples.Add(new Sample
                {
                    CrankAngle = Mathf.Repeat(crankAngle, 720f),
                    Heights = pistons.Select(piston => piston.GetAlturaAtual()).ToArray()
                });
                yield return null;
            }

            Assert.That(samples.Count, Is.GreaterThan(100),
                "The low-RPM run did not produce enough empirical samples.");

            DiagnosticResult[] results = BuildResults(pistons, samples);
            WriteDiagnosticCsv(results);

            Assert.That(results[0].CorrelationWithPiston1, Is.GreaterThan(0.99f));
            Assert.That(results[3].CorrelationWithPiston1, Is.GreaterThan(0.99f),
                "Pistons 1 and 4 must move in phase.");
            Assert.That(results[1].CorrelationWithPiston1, Is.LessThan(-0.70f));
            Assert.That(results[2].CorrelationWithPiston1, Is.LessThan(-0.70f),
                "Pistons 2 and 3 must move in opposition to pistons 1 and 4.");
            Assert.That(results[1].CorrelationWithPiston2, Is.GreaterThan(0.99f));
            Assert.That(results[2].CorrelationWithPiston2, Is.GreaterThan(0.99f),
                "Pistons 2 and 3 must move in phase.");
        }
        finally
        {
            UnityEngine.Object.Destroy(instance);
        }
    }

    private static Pistao[] OrderPistonsFromTimingEnd(
        ComponentTag[] representationTags,
        Pistao[] pistons)
    {
        ComponentTag flywheel = representationTags
            .First(tag => tag.Id == EngineComponentId.Volante);
        ComponentTag crankGear = representationTags
            .First(tag => tag.Id == EngineComponentId.EngrenagemVirabrequim);

        Vector3 timingToFlywheel = flywheel.transform.position - crankGear.transform.position;
        Assert.That(timingToFlywheel.sqrMagnitude, Is.GreaterThan(0.000001f));
        Vector3 axis = timingToFlywheel.normalized;

        return pistons
            .OrderBy(piston => Vector3.Dot(piston.transform.position - crankGear.transform.position, axis))
            .ToArray();
    }

    private sealed class DiagnosticResult
    {
        public int Cylinder;
        public string HierarchyPath;
        public string SerializedBank;
        public float SerializedOffset;
        public float ObservedPeakCrankDegrees;
        public float RelativePhaseDegrees;
        public float CorrelationWithPiston1;
        public float CorrelationWithPiston2;
    }

    private static DiagnosticResult[] BuildResults(Pistao[] pistons, List<Sample> samples)
    {
        float[][] centered = new float[4][];
        for (int pistonIndex = 0; pistonIndex < 4; pistonIndex++)
        {
            float mean = samples.Average(sample => sample.Heights[pistonIndex]);
            centered[pistonIndex] = samples
                .Select(sample => sample.Heights[pistonIndex] - mean)
                .ToArray();
        }

        var results = new DiagnosticResult[4];
        float piston1Peak = 0f;
        for (int pistonIndex = 0; pistonIndex < 4; pistonIndex++)
        {
            Sample peak = samples
                .Where(sample => sample.CrankAngle < 720f)
                .OrderByDescending(sample => sample.Heights[pistonIndex])
                .First();
            float peakAngle = peak.CrankAngle;
            if (pistonIndex == 0)
                piston1Peak = peakAngle;

            results[pistonIndex] = new DiagnosticResult
            {
                Cylinder = pistonIndex + 1,
                HierarchyPath = GetPath(pistons[pistonIndex].transform),
                SerializedBank = pistons[pistonIndex].banco.ToString(),
                SerializedOffset = pistons[pistonIndex].defasagemAngulo,
                ObservedPeakCrankDegrees = peakAngle,
                RelativePhaseDegrees = Mathf.Repeat(peakAngle - piston1Peak + 360f, 360f),
                CorrelationWithPiston1 = Correlation(centered[pistonIndex], centered[0]),
                CorrelationWithPiston2 = Correlation(centered[pistonIndex], centered[1])
            };
        }

        return results;
    }

    private static float Correlation(float[] left, float[] right)
    {
        double numerator = 0d;
        double leftSquare = 0d;
        double rightSquare = 0d;
        for (int i = 0; i < left.Length; i++)
        {
            numerator += left[i] * right[i];
            leftSquare += left[i] * left[i];
            rightSquare += right[i] * right[i];
        }

        return (float)(numerator / Math.Sqrt(leftSquare * rightSquare));
    }

    private static void WriteDiagnosticCsv(IEnumerable<DiagnosticResult> results)
    {
        string repositoryRoot = Directory.GetParent(Application.dataPath).Parent.FullName;
        string outputPath = Path.Combine(repositoryRoot, "docs", "piston_phase_diagnostic.csv");
        var csv = new StringBuilder();
        csv.AppendLine(
            "cylinder,hierarchy_path,serialized_bank,serialized_angle_offset_deg," +
            "observed_peak_crank_deg,relative_phase_to_piston_1_deg," +
            "correlation_with_piston_1,correlation_with_piston_2");

        foreach (DiagnosticResult result in results)
        {
            csv.Append(result.Cylinder).Append(',')
                .Append('"').Append(result.HierarchyPath.Replace("\"", "\"\"")).Append("\",")
                .Append(result.SerializedBank).Append(',')
                .Append(Format(result.SerializedOffset)).Append(',')
                .Append(Format(result.ObservedPeakCrankDegrees)).Append(',')
                .Append(Format(result.RelativePhaseDegrees)).Append(',')
                .Append(Format(result.CorrelationWithPiston1)).Append(',')
                .Append(Format(result.CorrelationWithPiston2)).AppendLine();
        }

        File.WriteAllText(outputPath, csv.ToString(), new UTF8Encoding(false));
    }

    private static string Format(float value) =>
        value.ToString("0.######", CultureInfo.InvariantCulture);

    private static string GetPath(Transform transform)
    {
        var names = new Stack<string>();
        while (transform != null)
        {
            names.Push(transform.name);
            transform = transform.parent;
        }

        return string.Join("/", names);
    }
}
