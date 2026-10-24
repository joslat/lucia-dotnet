using Lucia.Companion.Core;

namespace Lucia.Companion.Connection;

public sealed class HomeSource
{
    public HomeSource(string sourceId, string name, ObservationRole field)
    {
        SourceId = WireValue.SourceIdentifier(sourceId);
        Name = WireValue.Label(name);
        if (!Enum.IsDefined(field)) { throw new ArgumentException("The source field is invalid."); }
        Field = field;
    }
    public string SourceId { get; }
    public string Name { get; }
    public ObservationRole Field { get; }
}
