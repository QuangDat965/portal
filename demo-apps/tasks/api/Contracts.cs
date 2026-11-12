namespace Tasks.Api;

public sealed record CreateTaskRequest(string Title, string? Notes);

public sealed record UpdateTaskRequest(string? Title, string? Notes, bool? Done);
