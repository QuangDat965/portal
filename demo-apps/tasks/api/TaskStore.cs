namespace Tasks.Api;

public sealed record TaskItem(
    Guid Id,
    string Title,
    string? Notes,
    bool Done,
    string CreatedBy,
    string CreatedByName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Store trong bộ nhớ cho demo. Trong app thật, thay bằng DB riêng của app
/// (không dùng chung database với Portal).
/// </summary>
public sealed class TaskStore
{
    private readonly object _gate = new();
    private readonly List<TaskItem> _tasks =
    [
        new(
            Guid.NewGuid(),
            "Chuẩn bị demo SSO cho team",
            "Mở app từ Portal, kiểm tra session và quyền truy cập.",
            true,
            "seed",
            "Portal Seed",
            DateTimeOffset.UtcNow.AddHours(-5),
            DateTimeOffset.UtcNow.AddHours(-4)),
        new(
            Guid.NewGuid(),
            "Viết tài liệu tích hợp app mới",
            "Bổ sung ví dụ .NET + React vào docs/app-integration.md.",
            false,
            "seed",
            "Portal Seed",
            DateTimeOffset.UtcNow.AddHours(-3),
            DateTimeOffset.UtcNow.AddHours(-3)),
        new(
            Guid.NewGuid(),
            "Kiểm tra JWKS cache khi xoay khoá",
            "Đổi khoá ký và mở lại app để chắc rằng JWKS được refresh.",
            false,
            "seed",
            "Portal Seed",
            DateTimeOffset.UtcNow.AddHours(-2),
            DateTimeOffset.UtcNow.AddHours(-2)),
        new(
            Guid.NewGuid(),
            "Review quyền theo role",
            "Role member được mở Taskboard; admin thấy mọi app.",
            false,
            "seed",
            "Portal Seed",
            DateTimeOffset.UtcNow.AddHours(-1),
            DateTimeOffset.UtcNow.AddHours(-1))
    ];

    public IReadOnlyList<TaskItem> List()
    {
        lock (_gate)
        {
            return _tasks
                .OrderBy(t => t.Done)
                .ThenByDescending(t => t.CreatedAt)
                .ToArray();
        }
    }

    public TaskItem Create(string title, string? notes, string subject, string displayName)
    {
        var now = DateTimeOffset.UtcNow;
        var task = new TaskItem(
            Guid.NewGuid(),
            title,
            notes,
            false,
            subject,
            string.IsNullOrWhiteSpace(displayName) ? subject : displayName,
            now,
            now);

        lock (_gate)
        {
            _tasks.Add(task);
        }

        return task;
    }

    public TaskItem? Update(Guid id, string? title, string? notes, bool? done)
    {
        lock (_gate)
        {
            var index = _tasks.FindIndex(t => t.Id == id);
            if (index < 0)
            {
                return null;
            }

            var current = _tasks[index];
            var updated = current with
            {
                Title = string.IsNullOrWhiteSpace(title) ? current.Title : title,
                Notes = notes ?? current.Notes,
                Done = done ?? current.Done,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _tasks[index] = updated;
            return updated;
        }
    }

    public bool Delete(Guid id)
    {
        lock (_gate)
        {
            return _tasks.RemoveAll(t => t.Id == id) > 0;
        }
    }
}
