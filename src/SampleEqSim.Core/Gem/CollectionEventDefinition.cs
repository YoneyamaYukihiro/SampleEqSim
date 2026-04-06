namespace SampleEqSim.Core.Gem;

/// <summary>
/// GEM コレクションイベント定義 (SEMI E30 §10.4)
/// </summary>
public class CollectionEventDefinition
{
    public uint CollectionEventId { get; }
    public string EventName { get; }

    public CollectionEventDefinition(uint collectionEventId, string eventName)
    {
        CollectionEventId = collectionEventId;
        EventName = eventName;
    }
}
