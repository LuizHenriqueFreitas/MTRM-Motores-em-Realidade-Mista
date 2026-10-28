using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ValveTimingTests
{
    private readonly List<GameObject> objects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject item in objects)
            UnityEngine.Object.DestroyImmediate(item);
        objects.Clear();
    }

    [Test]
    public void IntakeHasFourPhasesSpacedBy180CrankDegreesInFiringOrder()
    {
        ArvoreComando intake = CreateCamshaft(
            side: 0,
            openingOffset: 90f);

        int[] firingOrder = { 1, 3, 4, 2 };
        float[] peaks = firingOrder
            .Select(cylinder => FindPeakCrankAngle(intake, cylinder, 0))
            .ToArray();

        Assert.That(peaks, Is.EqualTo(new[] { 160f, 340f, 520f, 700f }));
        for (int i = 1; i < peaks.Length; i++)
            Assert.That(peaks[i] - peaks[i - 1], Is.EqualTo(180f));
    }

    [Test]
    public void IntakeAndExhaustPeaksRemain360CrankDegreesApart()
    {
        ArvoreComando intake = CreateCamshaft(
            side: 0,
            openingOffset: 90f);
        ArvoreComando exhaust = CreateCamshaft(
            side: 1,
            openingOffset: 120f);

        foreach (int cylinder in Enumerable.Range(1, 4))
        {
            float intakePeak = FindPeakCrankAngle(intake, cylinder, 0);
            float exhaustPeak = FindPeakCrankAngle(exhaust, cylinder, 1);
            Assert.That(
                Mathf.Repeat(exhaustPeak - intakePeak, 720f),
                Is.EqualTo(360f).Within(0.001f),
                $"Cylinder {cylinder}");
        }
    }

    [Test]
    public void EveryIntakePeakOccursWhileItsPistonIsDescending()
    {
        ArvoreComando intake = CreateCamshaft(
            side: 0,
            openingOffset: 90f);

        foreach (int cylinder in Enumerable.Range(1, 4))
        {
            float peak = FindPeakCrankAngle(intake, cylinder, 0);
            float effectivePistonAngle =
                cylinder == 2 || cylinder == 3
                    ? Mathf.Repeat(peak + 180f, 360f)
                    : Mathf.Repeat(peak, 360f);

            Assert.That(
                effectivePistonAngle,
                Is.InRange(10f, 170f),
                $"Cylinder {cylinder} intake peak must occur on descent.");
        }
    }

    [Test]
    public void CircularOpeningWindowHasSinglePeakAtConfiguredCenter()
    {
        ArvoreComando intake = CreateCamshaft(
            side: 0,
            openingOffset: 90f);

        foreach (int cylinder in Enumerable.Range(1, 4))
        {
            float peak = FindPeakCrankAngle(intake, cylinder, 0);
            float maximum = OpeningAt(intake, cylinder, 0, peak);
            Assert.That(maximum, Is.EqualTo(1f).Within(0.0001f));

            float before = OpeningAt(
                intake,
                cylinder,
                0,
                Mathf.Repeat(peak - 1f, 720f));
            float after = OpeningAt(
                intake,
                cylinder,
                0,
                Mathf.Repeat(peak + 1f, 720f));
            Assert.That(before, Is.LessThan(maximum));
            Assert.That(after, Is.LessThan(maximum));
        }
    }

    private ArvoreComando CreateCamshaft(int side, float openingOffset)
    {
        var gameObject = new GameObject($"Camshaft-{side}");
        objects.Add(gameObject);
        ArvoreComando camshaft = gameObject.AddComponent<ArvoreComando>();
        SetField(camshaft, "fatorRotacao", 0.5f);
        SetField(camshaft, "ladoComando", side);
        SetField(camshaft, "cilindros", 4);
        SetField(camshaft, "duracaoAberturaCame", 200f);
        SetField(camshaft, "anguloAberturaCame", openingOffset);
        camshaft.inverterRotacao = true;
        return camshaft;
    }

    private static float FindPeakCrankAngle(
        ArvoreComando camshaft,
        int cylinder,
        int valveType)
    {
        return Enumerable.Range(0, 720)
            .Select(angle => new
            {
                Angle = (float)angle,
                Opening = OpeningAt(
                    camshaft,
                    cylinder,
                    valveType,
                    angle)
            })
            .OrderByDescending(item => item.Opening)
            .ThenBy(item => item.Angle)
            .First()
            .Angle;
    }

    private static float OpeningAt(
        ArvoreComando camshaft,
        int cylinder,
        int valveType,
        float crankAngle)
    {
        camshaft.AtualizarEstado(crankAngle, 60f);
        return camshaft.GetAberturaValvula(cylinder, valveType);
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new InvalidOperationException($"Field not found: {name}");
        field.SetValue(target, value);
    }
}
