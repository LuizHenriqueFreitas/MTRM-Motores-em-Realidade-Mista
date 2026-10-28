using UnityEngine;

public sealed class ComponentTag : MonoBehaviour
{
    [SerializeField] private EngineComponentId id = EngineComponentId.None;
    [SerializeField] private int instanceIndex;
    [SerializeField] private int subIndex;
    [SerializeField] private EngineRepresentation representation;
    [SerializeField] private string displayName;
    [TextArea(2, 5)]
    [SerializeField] private string educationalDescription;
    [SerializeField] private bool intentionallyUnidentified;
    [TextArea(2, 4)]
    [SerializeField] private string justification;

    public EngineComponentId Id => id;
    public int InstanceIndex => instanceIndex;
    public int SubIndex => subIndex;
    public EngineRepresentation Representation => representation;
    public string DisplayName => displayName;
    public string EducationalDescription => educationalDescription;
    public bool IntentionallyUnidentified => intentionallyUnidentified;
    public string Justification => justification;
}
