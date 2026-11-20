namespace BookmarkEditor.Model;

public sealed class BookmarkDocument
{
    public List<BookmarkNode> Roots { get; } = [];

    public string? FilePath { get; set; }

    public bool IsDirty { get; private set; }

    public event Action<BookmarkNode, BookmarkNode?, int>? NodeInserted;

    public event Action<BookmarkNode>? NodeRemoved;

    public event Action<BookmarkNode>? NodeMoved;

    public event Action<BookmarkNode>? NodeChanged;

    public void MarkDirty() => IsDirty = true;

    public void MarkSaved(string? path)
    {
        FilePath = path;
        IsDirty = false;
    }

    public void InsertNode(BookmarkNode node, BookmarkNode? parent, int index)
    {
        var list = GetChildren(parent);
        index = Math.Clamp(index, 0, list.Count);
        list.Insert(index, node);
        node.Parent = parent;
        NodeInserted?.Invoke(node, parent, index);
    }

    public void RemoveNode(BookmarkNode node)
    {
        var list = GetChildren(node.Parent);
        if (!list.Remove(node))
        {
            return;
        }

        node.Parent = null;
        NodeRemoved?.Invoke(node);
    }

    public void MoveNode(BookmarkNode node, BookmarkNode? newParent, int index)
    {
        var newList = GetChildren(newParent);
        var oldList = GetChildren(node.Parent);
        oldList.Remove(node);
        index = Math.Clamp(index, 0, newList.Count);
        newList.Insert(index, node);
        node.Parent = newParent;
        NodeMoved?.Invoke(node);
    }

    public void NotifyNodeChanged(BookmarkNode node) => NodeChanged?.Invoke(node);

    private List<BookmarkNode> GetChildren(BookmarkNode? parent) => parent is null ? Roots : parent.Children;
}
