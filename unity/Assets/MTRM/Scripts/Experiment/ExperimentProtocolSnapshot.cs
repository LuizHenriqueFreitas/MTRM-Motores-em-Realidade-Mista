using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public static class ExperimentProtocolSnapshot
{
    public static string Serialize(ExperimentProtocol protocol)
    {
        if (protocol == null)
            throw new ArgumentNullException(nameof(protocol));
        string protocolJson = JsonUtility.ToJson(protocol, true);
        string bankJson = protocol.knowledgeItemBank != null
            ? JsonUtility.ToJson(protocol.knowledgeItemBank, true)
            : "null";
        return "{\n  \"protocol\": " +
               protocolJson.Replace("\n", "\n  ") +
               ",\n  \"knowledgeItemBank\": " +
               bankJson.Replace("\n", "\n  ") +
               "\n}";
    }

    public static string ComputeHash(string json)
    {
        if (json == null)
            throw new ArgumentNullException(nameof(json));
        using SHA256 sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
        return BitConverter.ToString(hash)
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }
}
