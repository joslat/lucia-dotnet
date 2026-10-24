using Lucia.Companion.Core;

namespace Lucia.Companion.App.Configuration;

public sealed record HomeImportConflict(string SourceId, ObservationRole Role, string ExistingPlaceId, string ProposedPlaceId);
