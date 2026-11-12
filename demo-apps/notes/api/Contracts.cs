namespace Notes.Api;

public sealed record CreateNoteRequest(string Title, string? Body, string? Color);

public sealed record UpdateNoteRequest(string? Title, string? Body, string? Color, bool? Pinned);
