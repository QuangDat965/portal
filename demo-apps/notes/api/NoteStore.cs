namespace Notes.Api;

public sealed record NoteItem(
    Guid Id,
    string Title,
    string Body,
    string Color,
    bool Pinned,
    string CreatedBy,
    string CreatedByName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Store trong bộ nhớ cho demo. App thật nên dùng database riêng, key theo claim sub.
/// </summary>
public sealed class NoteStore
{
    public static readonly string[] AllowedColors =
        ["amber", "sky", "rose", "emerald", "violet"];

    private readonly object _gate = new();
    private readonly List<NoteItem> _notes =
    [
        new(
            Guid.NewGuid(),
            "Chào mừng tới Team Notes",
            "Ghi chú này được tạo sẵn khi app khởi động. Bạn có thể sửa, ghim hoặc xoá.",
            "amber",
            true,
            "seed",
            "Portal Seed",
            DateTimeOffset.UtcNow.AddHours(-6),
            DateTimeOffset.UtcNow.AddHours(-6)),
        new(
            Guid.NewGuid(),
            "Quy ước đặt tên",
            "Dùng tiêu đề ngắn, nội dung đủ ngữ cảnh. Màu chỉ mang tính phân loại.",
            "sky",
            false,
            "seed",
            "Portal Seed",
            DateTimeOffset.UtcNow.AddHours(-4),
            DateTimeOffset.UtcNow.AddHours(-3)),
        new(
            Guid.NewGuid(),
            "Việc cần làm tuần này",
            "- Kiểm tra SSO với Portal\n- Thêm ghi chú mới\n- Thử ghim ghi chú quan trọng",
            "emerald",
            false,
            "seed",
            "Portal Seed",
            DateTimeOffset.UtcNow.AddHours(-2),
            DateTimeOffset.UtcNow.AddHours(-2))
    ];

    public IReadOnlyList<NoteItem> List()
    {
        lock (_gate)
        {
            return _notes
                .OrderByDescending(n => n.Pinned)
                .ThenByDescending(n => n.UpdatedAt)
                .ToArray();
        }
    }

    public NoteItem Create(
        string title,
        string body,
        string color,
        string subject,
        string displayName)
    {
        var now = DateTimeOffset.UtcNow;
        var note = new NoteItem(
            Guid.NewGuid(),
            title,
            body,
            NormalizeColor(color),
            false,
            subject,
            string.IsNullOrWhiteSpace(displayName) ? subject : displayName,
            now,
            now);

        lock (_gate)
        {
            _notes.Add(note);
        }

        return note;
    }

    public NoteItem? Update(Guid id, string? title, string? body, string? color, bool? pinned)
    {
        lock (_gate)
        {
            var index = _notes.FindIndex(n => n.Id == id);
            if (index < 0)
            {
                return null;
            }

            var current = _notes[index];
            var updated = current with
            {
                Title = string.IsNullOrWhiteSpace(title) ? current.Title : title,
                Body = body ?? current.Body,
                Color = string.IsNullOrWhiteSpace(color) ? current.Color : NormalizeColor(color),
                Pinned = pinned ?? current.Pinned,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _notes[index] = updated;
            return updated;
        }
    }

    public bool Delete(Guid id)
    {
        lock (_gate)
        {
            return _notes.RemoveAll(n => n.Id == id) > 0;
        }
    }

    public static string NormalizeColor(string? color) =>
        color is not null && AllowedColors.Contains(color, StringComparer.OrdinalIgnoreCase)
            ? color.ToLowerInvariant()
            : "amber";
}
