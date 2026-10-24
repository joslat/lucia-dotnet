namespace Lucia.Companion.App.Persistence;

public sealed class HomeSaveConflictException() : IOException("The saved setup changed since it was read.");
