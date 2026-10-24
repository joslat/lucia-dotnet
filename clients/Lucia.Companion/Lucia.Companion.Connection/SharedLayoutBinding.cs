using Lucia.Companion.Core;

namespace Lucia.Companion.Connection;

public sealed record SharedLayoutBinding(string PlaceId, string SourcePlaceId, string SourceId, ObservationRole Field);
