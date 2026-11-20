namespace BookmarkEditor.Model;

public enum BookmarkNodeType
{
    Folder,
    Bookmark,
}

public sealed class BookmarkNode
{
    public Guid Id { get; } = Guid.NewGuid();

    public required BookmarkNodeType Type { get; init; }

    public string Name { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public BookmarkNode? Parent { get; internal set; }

    public List<BookmarkNode> Children { get; } = [];

    public DateTime? AddDate { get; set; }

    public DateTime? LastModified { get; set; }

    public bool IsToolbarFolder { get; set; }

    public static BookmarkNode CreateFolder(string name) =>
        new() { Type = BookmarkNodeType.Folder, Name = name };

    public static BookmarkNode CreateBookmark(string name, string url) =>
        new() { Type = BookmarkNodeType.Bookmark, Name = name, Url = url };

    public void AddChild(BookmarkNode child) => InsertChild(Children.Count, child);

    public void InsertChild(int index, BookmarkNode child)
    {
        child.Parent?.Children.Remove(child);
        Children.Insert(index, child);
        child.Parent = this;
    }

    public bool RemoveChild(BookmarkNode child)
    {
        if (!Children.Remove(child))
        {
            return false;
        }

        child.Parent = null;
        return true;
    }
}
