using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;

public sealed class DataDictionaryCoverageTests
{
    private const string DictionaryPath = "../docs/DATA_DICTIONARY.md";

    [Test]
    public void DictionaryCoversEveryEmittedField()
    {
        string document = File.ReadAllText(DictionaryPath);

        AssertSectionMatches<ExperimentEventRecord>(
            document,
            "EVENT_FIELDS");
        AssertSectionMatches<ExperimentSessionMetadata>(
            document,
            "SESSION_FIELDS");
        AssertSectionMatches<ExperimentSessionSummary>(
            document,
            "SUMMARY_FIELDS");
    }

    [Test]
    public void SessionSummaryDoesNotExposeAmbiguousScore()
    {
        string[] fields = typeof(ExperimentSessionSummary)
            .GetFields(BindingFlags.Instance | BindingFlags.Public)
            .Select(field => field.Name)
            .ToArray();

        Assert.That(fields, Does.Not.Contain("score"));
        Assert.That(fields, Does.Not.Contain("correctTasks"));
        Assert.That(fields, Does.Contain("identificationScore"));
        Assert.That(fields, Does.Contain("preTestScore"));
        Assert.That(fields, Does.Contain("postTestScore"));
    }

    private static void AssertSectionMatches<T>(
        string document,
        string marker)
    {
        string section = Between(
            document,
            $"<!-- {marker}_START -->",
            $"<!-- {marker}_END -->");
        HashSet<string> documented = Regex
            .Matches(section, @"^\| `(?<name>[^`]+)`", RegexOptions.Multiline)
            .Cast<Match>()
            .Select(match => match.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> emitted = typeof(T)
            .GetFields(BindingFlags.Instance | BindingFlags.Public)
            .Select(field => field.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.That(documented, Is.EquivalentTo(emitted));
    }

    private static string Between(
        string source,
        string startMarker,
        string endMarker)
    {
        int start = source.IndexOf(
            startMarker,
            StringComparison.Ordinal);
        int end = source.IndexOf(
            endMarker,
            StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0));
        Assert.That(end, Is.GreaterThan(start));
        return source.Substring(
            start + startMarker.Length,
            end - start - startMarker.Length);
    }
}
