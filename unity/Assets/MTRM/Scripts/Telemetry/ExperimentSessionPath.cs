using System.IO;

public static class ExperimentSessionPath
{
    public static string Resolve(string persistentDataPath, string sessionId)
    {
        return Path.Combine(persistentDataPath, "Experiments", sessionId);
    }
}
