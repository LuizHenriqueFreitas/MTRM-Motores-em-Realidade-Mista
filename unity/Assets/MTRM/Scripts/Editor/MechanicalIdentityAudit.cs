using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class MechanicalIdentityAudit
{
    private const string PrefabPath =
        "Assets/MTRM/Prefabs/Engine/StateEngineManager.prefab";
    private const string OutputPath =
        "../../docs/mechanical_identity_audit.csv";

    [MenuItem("MTRM/Identidade/Auditar distinções mecânicas")]
    public static void Export()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var csv = new StringBuilder();
            csv.AppendLine(
                "representation,category,mesh_path,primary_evidence," +
                "primary_value,secondary_value,derived_identity," +
                "instance_index,sub_index,note");
            foreach (Transform representation in new[]
                     {
                         root.transform.Find("4L.blend-8vExpande"),
                         root.transform.Find("4L.blend-8vLigaDesliga")
                     })
            {
                WriteCrankshaftRows(csv, representation);
                WriteCamshaftRows(csv, representation);
                WriteIgnitionRows(csv, representation);
                WriteOptionalBearingRows(csv, representation);
            }
            string path = Path.GetFullPath(
                Path.Combine(Application.dataPath, OutputPath));
            File.WriteAllText(path, csv.ToString(), new UTF8Encoding(false));
            Debug.Log($"MTRM mechanical identity audit written to {path}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    public static void ExportFromCommandLine()
    {
        try
        {
            Export();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void WriteCrankshaftRows(
        StringBuilder csv,
        Transform representation)
    {
        foreach (MeshRenderer mesh in TitledMeshes(
                     representation,
                     "Virabrequim"))
        {
            bool hasComponent = mesh.GetComponent<Virabrequim>() != null;
            WriteRow(
                csv,
                representation,
                "crankshaft",
                mesh,
                "Virabrequim component on same GameObject",
                hasComponent ? "true" : "false",
                string.Empty,
                hasComponent ? "Virabrequim" : "EngrenagemVirabrequim",
                0,
                0,
                "A separação usa o vínculo MonoBehaviour→m_GameObject, " +
                "não o nome da malha.");
        }
    }

    private static void WriteCamshaftRows(
        StringBuilder csv,
        Transform representation)
    {
        List<ArvoreComando> camshafts = representation
            .GetComponentsInChildren<ArvoreComando>(true)
            .OrderBy(item => item.GetLadoComando())
            .ToList();
        if (camshafts.Count != 2)
            throw new InvalidOperationException(
                $"Expected two camshafts, found {camshafts.Count}.");

        foreach (ArvoreComando camshaft in camshafts)
        {
            MeshRenderer renderer = camshaft.GetComponent<MeshRenderer>();
            WriteRow(
                csv,
                representation,
                "camshaft",
                renderer,
                "ArvoreComando.ladoComando",
                camshaft.GetLadoComando().ToString(),
                string.Empty,
                "ArvoreDeCames",
                0,
                camshaft.GetLadoComando() + 1,
                camshaft.GetNomeLado());
        }

        List<MeshRenderer> gears = TitledMeshes(
            representation,
            "Engrenagem de Comando");
        foreach (MeshRenderer gear in gears)
        {
            var distances = camshafts
                .Select(item => new
                {
                    Camshaft = item,
                    Distance = PerpendicularDistanceToCamshaftAxis(
                        gear.bounds.center,
                        item.GetComponent<MeshRenderer>())
                })
                .OrderBy(item => item.Distance)
                .ToList();
            bool clear = distances[0].Distance <
                         distances[1].Distance * 0.8f;
            WriteRow(
                csv,
                representation,
                "camshaft_gear",
                gear,
                "perpendicular_distance_to_field_classified_camshaft_axis",
                Format(distances[0].Distance),
                Format(distances[1].Distance),
                clear ? "EngrenagemDeComando" : "unresolved",
                0,
                clear
                    ? distances[0].Camshaft.GetLadoComando() + 1
                    : 0,
                clear
                    ? distances[0].Camshaft.GetNomeLado()
                    : "distâncias ambíguas");
        }
    }

    private static float PerpendicularDistanceToCamshaftAxis(
        Vector3 point,
        MeshRenderer camshaft)
    {
        MeshFilter filter = camshaft.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
            throw new InvalidOperationException(
                "Camshaft has no referenced mesh.");

        Bounds localBounds = filter.sharedMesh.bounds;
        Vector3 size = localBounds.size;
        Vector3 localAxis = size.x >= size.y && size.x >= size.z
            ? Vector3.right
            : size.y >= size.z
                ? Vector3.up
                : Vector3.forward;
        Vector3 worldAxis = camshaft.transform
            .TransformDirection(localAxis)
            .normalized;
        Vector3 axisOrigin = camshaft.transform.TransformPoint(
            localBounds.center);
        return Vector3.Cross(point - axisOrigin, worldAxis).magnitude;
    }

    private static void WriteIgnitionRows(
        StringBuilder csv,
        Transform representation)
    {
        List<MeshRenderer> titled = TitledMeshes(
            representation,
            "Cabos de Vela");
        if (titled.Count != 5)
            throw new InvalidOperationException(
                $"Expected five ignition meshes, found {titled.Count}.");

        MeshRenderer harness = titled
            .OrderByDescending(item => BoundsVolume(item.bounds))
            .First();
        List<MeshRenderer> connectors = titled
            .Where(item => item != harness)
            .ToList();
        int containedCenters = connectors.Count(item =>
            harness.bounds.Contains(item.bounds.center));
        WriteRow(
            csv,
            representation,
            "ignition_harness",
            harness,
            "largest bounds volume; connector centers inside harness bounds",
            Format(BoundsVolume(harness.bounds)),
            $"{containedCenters}/4",
            "CaboVela",
            0,
            0,
            containedCenters == 4
                ? "malha de conjunto engloba os quatro conectores; " +
                  SharedMeshReference(harness)
                : "contenção incompleta; revisar");

        Dictionary<int, Vector3> cylinderCenters = ValveCylinderCenters(
            representation);
        foreach (MeshRenderer connector in connectors)
        {
            var distances = cylinderCenters
                .Select(item => new
                {
                    Cylinder = item.Key,
                    Distance = Vector3.Distance(
                        connector.bounds.center,
                        item.Value)
                })
                .OrderBy(item => item.Distance)
                .ToList();
            bool clear = distances[0].Distance <
                         distances[1].Distance * 0.8f;
            WriteRow(
                csv,
                representation,
                "spark_plug_connector",
                connector,
                "distance_to_tipoValvula cylinder centroid",
                Format(distances[0].Distance),
                Format(distances[1].Distance),
                clear ? "ConectorVela" : "unresolved",
                clear ? distances[0].Cylinder : 0,
                0,
                clear
                    ? $"cilindro {distances[0].Cylinder}; " +
                      SharedMeshReference(connector)
                    : "distâncias ambíguas");
        }
    }

    private static string SharedMeshReference(MeshRenderer renderer)
    {
        Mesh mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
        if (mesh == null ||
            !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                mesh,
                out string guid,
                out long localId))
        {
            return "shared_mesh=unresolved";
        }
        return $"shared_mesh={guid}:{localId}";
    }

    private static void WriteOptionalBearingRows(
        StringBuilder csv,
        Transform representation)
    {
        foreach (string title in new[] { "Bronzina", "Bronzinav", "Mancal" })
        {
            foreach (MeshRenderer mesh in TitledMeshes(representation, title))
            {
                WriteRow(
                    csv,
                    representation,
                    title.StartsWith("Bronzina", StringComparison.Ordinal)
                        ? "optional_bearing_shell"
                        : "optional_main_bearing",
                    mesh,
                    "world_position_only",
                    Vector(mesh.bounds.center),
                    string.Empty,
                    "manual_review",
                    0,
                    0,
                    "Posição absoluta isolada não prova função. Caminho " +
                    "disponível e não executado: proximidade a componentes " +
                    "já identificados (Biela -> bronzina de biela; " +
                    "Virabrequim -> bronzina/mancal principal; " +
                    "ArvoreDeCames -> mancal de comando).");
            }
        }
    }

    private static Dictionary<int, Vector3> ValveCylinderCenters(
        Transform representation)
    {
        return representation
            .GetComponentsInChildren<Valvula>(true)
            .Select(component =>
            {
                var serialized = new SerializedObject(component);
                int cylinder = serialized.FindProperty("cicloValvula").intValue;
                GameObject head = serialized.FindProperty("hasteValvula")
                    .objectReferenceValue as GameObject;
                return new { Cylinder = cylinder, Position = head.transform.position };
            })
            .GroupBy(item => item.Cylinder)
            .ToDictionary(
                group => group.Key,
                group => group.Aggregate(
                    Vector3.zero,
                    (sum, item) => sum + item.Position) / group.Count());
    }

    private static List<MeshRenderer> TitledMeshes(
        Transform representation,
        string title)
    {
        return representation
            .GetComponentsInChildren<MeshRenderer>(true)
            .Where(item =>
            {
                ToolTipTrigger tooltip = item.GetComponent<ToolTipTrigger>();
                return tooltip != null && tooltip.TitleText == title;
            })
            .OrderBy(item => HierarchyPath(item.transform, representation))
            .ToList();
    }

    private static void WriteRow(
        StringBuilder csv,
        Transform representation,
        string category,
        MeshRenderer mesh,
        string evidence,
        string primary,
        string secondary,
        string identity,
        int instanceIndex,
        int subIndex,
        string note)
    {
        csv.Append(Csv(representation.name.Contains("Expande")
            ? "Expande"
            : "LigaDesliga"));
        csv.Append(',');
        csv.Append(Csv(category));
        csv.Append(',');
        csv.Append(Csv(HierarchyPath(mesh.transform, representation)));
        csv.Append(',');
        csv.Append(Csv(evidence));
        csv.Append(',');
        csv.Append(Csv(primary));
        csv.Append(',');
        csv.Append(Csv(secondary));
        csv.Append(',');
        csv.Append(Csv(identity));
        csv.Append(',');
        csv.Append(instanceIndex);
        csv.Append(',');
        csv.Append(subIndex);
        csv.Append(',');
        csv.AppendLine(Csv(note));
    }

    private static float BoundsVolume(Bounds bounds)
    {
        return bounds.size.x * bounds.size.y * bounds.size.z;
    }

    private static string HierarchyPath(Transform current, Transform root)
    {
        var names = new Stack<string>();
        for (Transform cursor = current;
             cursor != null && cursor != root;
             cursor = cursor.parent)
        {
            names.Push(cursor.name);
        }
        return string.Join("/", names);
    }

    private static string Vector(Vector3 value)
    {
        return $"{Format(value.x)}|{Format(value.y)}|{Format(value.z)}";
    }

    private static string Format(float value)
    {
        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    private static string Csv(string value)
    {
        return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
    }
}
