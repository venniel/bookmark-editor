using BookmarkEditor.Model;

namespace BookmarkEditor.Editing;

public interface IEditCommand
{
    BookmarkNode? FocusNode { get; }

    void Undo();

    void Redo();
}

public sealed class EditHistory
{
    private readonly Stack<IEditCommand> _undo = new();
    private readonly Stack<IEditCommand> _redo = new();

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public void Execute(IEditCommand command)
    {
        command.Redo();
        _undo.Push(command);
        _redo.Clear();
    }

    public IEditCommand? Undo()
    {
        if (_undo.Count == 0)
        {
            return null;
        }

        var command = _undo.Pop();
        command.Undo();
        _redo.Push(command);
        return command;
    }

    public IEditCommand? Redo()
    {
        if (_redo.Count == 0)
        {
            return null;
        }

        var command = _redo.Pop();
        command.Redo();
        _undo.Push(command);
        return command;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}

public sealed class InsertNodeCommand : IEditCommand
{
    private readonly BookmarkDocument _document;
    private readonly BookmarkNode _node;
    private readonly BookmarkNode? _parent;
    private readonly int _index;

    public InsertNodeCommand(BookmarkDocument document, BookmarkNode node, BookmarkNode? parent, int index)
    {
        _document = document;
        _node = node;
        _parent = parent;
        _index = index;
    }

    public BookmarkNode? FocusNode { get; private set; }

    public void Redo()
    {
        _document.InsertNode(_node, _parent, _index);
        FocusNode = _node;
    }

    public void Undo()
    {
        _document.RemoveNode(_node);
        FocusNode = _parent;
    }
}

public sealed class DeleteNodeCommand : IEditCommand
{
    private readonly BookmarkDocument _document;
    private readonly BookmarkNode _node;
    private readonly BookmarkNode? _parent;
    private readonly int _index;
    private readonly BookmarkNode? _focusAfterDelete;

    public DeleteNodeCommand(BookmarkDocument document, BookmarkNode node, BookmarkNode? focusAfterDelete)
    {
        _document = document;
        _node = node;
        _parent = node.Parent;
        _index = (node.Parent?.Children ?? document.Roots).IndexOf(node);
        _focusAfterDelete = focusAfterDelete;
    }

    public BookmarkNode? FocusNode { get; private set; }

    public void Redo()
    {
        _document.RemoveNode(_node);
        FocusNode = _focusAfterDelete;
    }

    public void Undo()
    {
        _document.InsertNode(_node, _parent, _index);
        FocusNode = _node;
    }
}

public sealed class UpdateNodeCommand : IEditCommand
{
    private readonly BookmarkDocument _document;
    private readonly BookmarkNode _node;
    private readonly string _oldName;
    private readonly string _oldUrl;
    private readonly string _newName;
    private readonly string _newUrl;

    public UpdateNodeCommand(BookmarkDocument document, BookmarkNode node, string name, string url)
    {
        _document = document;
        _node = node;
        _oldName = node.Name;
        _oldUrl = node.Url;
        _newName = name;
        _newUrl = url;
    }

    public BookmarkNode? FocusNode => _node;

    public void Redo() => Apply(_newName, _newUrl);

    public void Undo() => Apply(_oldName, _oldUrl);

    private void Apply(string name, string url)
    {
        _node.Name = name;
        _node.Url = url;
        _document.NotifyNodeChanged(_node);
    }
}

public sealed class MoveNodeCommand : IEditCommand
{
    private readonly BookmarkDocument _document;
    private readonly BookmarkNode _node;
    private readonly BookmarkNode? _targetParent;
    private readonly int _targetIndex;
    private BookmarkNode? _originParent;
    private int _originIndex;
    private bool _originCaptured;

    public MoveNodeCommand(BookmarkDocument document, BookmarkNode node, BookmarkNode? targetParent, int targetIndex)
    {
        _document = document;
        _node = node;
        _targetParent = targetParent;
        _targetIndex = targetIndex;
    }

    public BookmarkNode? FocusNode => _node;

    public void Redo()
    {
        if (!_originCaptured)
        {
            _originParent = _node.Parent;
            _originIndex = (_originParent?.Children ?? _document.Roots).IndexOf(_node);
            _originCaptured = true;
        }

        _document.MoveNode(_node, _targetParent, _targetIndex);
    }

    public void Undo() => _document.MoveNode(_node, _originParent, _originIndex);
}
