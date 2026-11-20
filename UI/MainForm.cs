using BookmarkEditor.Editing;
using BookmarkEditor.Exporting;
using BookmarkEditor.Model;
using BookmarkEditor.Parsing;
using System.Diagnostics;
using System.Text;

namespace BookmarkEditor.UI;

public sealed class MainForm : Form
{
    private BookmarkDocument _document;
    private readonly TreeView _tree;
    private readonly ImageList _icons;
    private readonly StatusBarControl _statusBar;
    private readonly ContextMenuStrip _contextMenu = new();
    private readonly Dictionary<BookmarkNode, TreeNode> _nodeMap = new();
    private readonly EditHistory _history = new();
    private BookmarkNode? _clipboardNode;
    private bool _clipboardIsCut;
    private BookmarkNode? _cutNode;
    private ToolStripMenuItem _undoItem = null!;
    private ToolStripMenuItem _redoItem = null!;
    private ToolStripMenuItem _cutItem = null!;
    private ToolStripMenuItem _copyItem = null!;
    private ToolStripMenuItem _pasteItem = null!;
    private ToolStripMenuItem _deleteItem = null!;
    private ToolStripMenuItem _editBookmarkItem = null!;
    private ToolStripMenuItem _renameItem = null!;
    private ToolStripMenuItem _moveUpItem = null!;
    private ToolStripMenuItem _moveDownItem = null!;

    public MainForm()
    {
        _document = new BookmarkDocument();

        _icons = new ImageList
        {
            ColorDepth = ColorDepth.Depth32Bit,
            ImageSize = new Size(16, 16),
        };
        _icons.Images.Add(ShellIcons.GetFolderBitmap());
        _icons.Images.Add(ShellIcons.GetShortcutBitmap());

        _tree = new TreeView
        {
            Dock = DockStyle.Fill,
            HideSelection = false,
            FullRowSelect = true,
            ShowLines = true,
            ShowRootLines = true,
            ShowPlusMinus = true,
            ImageList = _icons,
            AccessibleName = "书签列表",
        };
        _tree.AfterSelect += (_, _) => UpdateStatus();
        _tree.KeyDown += OnTreeKeyDown;
        _tree.MouseDown += OnTreeMouseDown;
        _tree.ContextMenuStrip = _contextMenu;
        _contextMenu.Opening += (_, _) => BuildContextMenu();

        _statusBar = new StatusBarControl();

        var menuStrip = BuildMenu();
        MainMenuStrip = menuStrip;

        Text = "书签编辑器";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(640, 600);
        MinimumSize = new Size(400, 300);
        AccessibleName = "书签编辑器";

        Controls.Add(_tree);
        Controls.Add(_statusBar);
        Controls.Add(menuStrip);

        AttachDocument(_document);
        LoadDocument();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _icons.Dispose();
            _contextMenu.Dispose();
        }

        base.Dispose(disposing);
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip();

        var fileMenu = new ToolStripMenuItem("文件");
        fileMenu.DropDownItems.Add(Item("打开", Keys.Control | Keys.O, (_, _) => OpenFile(), enabled: true));
        fileMenu.DropDownItems.Add(Item("保存", Keys.Control | Keys.S, (_, _) => SaveDocument(), enabled: true));
        fileMenu.DropDownItems.Add(Item("另存为", Keys.Control | Keys.Shift | Keys.S, (_, _) => SaveDocumentAs(), enabled: true));
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add(Item("退出", Keys.None, (_, _) => Close(), enabled: true));

        var editMenu = new ToolStripMenuItem("编辑");
        _undoItem = Item("撤销", Keys.Control | Keys.Z, (_, _) => Undo(), enabled: true);
        editMenu.DropDownItems.Add(_undoItem);
        _redoItem = Item("重做", Keys.Control | Keys.Y, (_, _) => Redo(), enabled: true);
        editMenu.DropDownItems.Add(_redoItem);
        editMenu.DropDownItems.Add(new ToolStripSeparator());
        _cutItem = Item("剪切", Keys.Control | Keys.X, (_, _) => CutSelected(), enabled: true);
        editMenu.DropDownItems.Add(_cutItem);
        _copyItem = Item("复制", Keys.Control | Keys.C, (_, _) => CopySelected(), enabled: true);
        editMenu.DropDownItems.Add(_copyItem);
        _pasteItem = Item("粘贴", Keys.Control | Keys.V, (_, _) => PasteSelected(), enabled: true);
        editMenu.DropDownItems.Add(_pasteItem);
        _deleteItem = Item("删除", Keys.Delete, (_, _) => DeleteSelected(), enabled: true);
        editMenu.DropDownItems.Add(_deleteItem);

        var actionMenu = new ToolStripMenuItem("操作");
        actionMenu.DropDownItems.Add(Item("新建书签", Keys.Control | Keys.Shift | Keys.N, (_, _) => CreateBookmark(), enabled: true));
        actionMenu.DropDownItems.Add(Item("新建文件夹", Keys.Control | Keys.N, (_, _) => CreateFolder(), enabled: true));
        actionMenu.DropDownItems.Add(new ToolStripSeparator());
        _editBookmarkItem = Item("编辑", Keys.None, (_, _) => EditSelectedBookmark(), enabled: true);
        actionMenu.DropDownItems.Add(_editBookmarkItem);
        _renameItem = Item("重命名", Keys.F2, (_, _) => RenameSelected(), enabled: true);
        actionMenu.DropDownItems.Add(_renameItem);
        actionMenu.DropDownItems.Add(new ToolStripSeparator());
        _moveUpItem = Item("上移", Keys.None, (_, _) => MoveSelected(-1), enabled: true);
        _moveUpItem.ShortcutKeyDisplayString = "Shift+上方向键";
        actionMenu.DropDownItems.Add(_moveUpItem);
        _moveDownItem = Item("下移", Keys.None, (_, _) => MoveSelected(1), enabled: true);
        _moveDownItem.ShortcutKeyDisplayString = "Shift+下方向键";
        actionMenu.DropDownItems.Add(_moveDownItem);

        var helpMenu = new ToolStripMenuItem("帮助");
        helpMenu.DropDownItems.Add(Item("关于", Keys.None, (_, _) => ShowAbout(), enabled: true));

        menu.Items.Add(fileMenu);
        menu.Items.Add(editMenu);
        menu.Items.Add(actionMenu);
        menu.Items.Add(helpMenu);
        return menu;
    }

    private static ToolStripMenuItem Item(string text, Keys shortcut = Keys.None, EventHandler? click = null, bool enabled = false)
    {
        var item = new ToolStripMenuItem(text) { Enabled = enabled };
        if (shortcut != Keys.None)
        {
            item.ShortcutKeys = shortcut;
        }

        if (click is not null)
        {
            item.Click += click;
        }

        return item;
    }

    private void ShowAbout()
    {
        MessageBox.Show(
            this,
            "书签编辑器\n版本 1.0",
            "关于",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!ConfirmUnsavedChanges())
        {
            e.Cancel = true;
            return;
        }

        base.OnFormClosing(e);
    }

    private bool ConfirmUnsavedChanges()
    {
        if (!_document.IsDirty)
        {
            return true;
        }

        using var dialog = new UnsavedChangesDialog();
        var result = dialog.ShowDialog(this);
        if (result == DialogResult.OK)
        {
            return SaveDocument();
        }

        return result == DialogResult.No;
    }

    private bool SaveDocument() =>
        _document.FilePath is null ? SaveDocumentAs() : SaveTo(_document.FilePath);

    private bool SaveDocumentAs()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "另存为",
            Filter = "书签文件 (*.html)|*.html|所有文件 (*.*)|*.*",
            DefaultExt = "html",
            AddExtension = true,
            OverwritePrompt = true,
            FileName = _document.FilePath is null ? "bookmarks.html" : Path.GetFileName(_document.FilePath),
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return false;
        }

        return SaveTo(dialog.FileName);
    }

    private bool SaveTo(string path)
    {
        try
        {
            var content = BookmarkHtmlWriter.Serialize(_document.Roots);
            WriteFileAtomically(path, content);
            _document.MarkSaved(path);
            UpdateStatus();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"无法保存文件：{ex.Message}",
                "保存书签文件",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }
    }

    private static void WriteFileAtomically(string path, string content)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException("无效的保存路径。");
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(tempPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(tempPath, fullPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            throw;
        }
    }

    private void AttachDocument(BookmarkDocument document)
    {
        document.NodeInserted += OnNodeInserted;
        document.NodeRemoved += OnNodeRemoved;
        document.NodeMoved += OnNodeMoved;
        document.NodeChanged += OnNodeChanged;
    }

    private void OpenFile()
    {
        if (!ConfirmUnsavedChanges())
        {
            return;
        }

        using var dialog = new OpenFileDialog
        {
            Title = "打开书签文件",
            Filter = "书签文件 (*.html;*.htm)|*.html;*.htm|所有文件 (*.*)|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var roots = BookmarkHtmlParser.Parse(dialog.FileName);
            var document = new BookmarkDocument { FilePath = dialog.FileName };
            document.Roots.AddRange(roots);

            ClearCutState();
            _clipboardNode = null;
            _clipboardIsCut = false;
            _history.Clear();

            _document = document;
            AttachDocument(document);
            LoadDocument();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"无法打开文件：{ex.Message}",
                "打开书签文件",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void CreateBookmark()
    {
        using var dialog = new BookmarkEditDialog("新建书签", string.Empty, ReadClipboardUrl());
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        InsertNewNode(BookmarkNode.CreateBookmark(dialog.ItemName, dialog.ItemUrl));
    }

    private void CreateFolder()
    {
        using var dialog = new TextInputDialog("新建文件夹", "文件夹名称", string.Empty);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        InsertNewNode(BookmarkNode.CreateFolder(dialog.Value));
    }

    private void InsertNewNode(BookmarkNode data)
    {
        var parent = GetTargetFolder();
        var index = parent is null ? _document.Roots.Count : parent.Children.Count;
        var command = new InsertNodeCommand(_document, data, parent, index);
        _history.Execute(command);
        SelectNode(data);
        MarkChanged();
    }

    private BookmarkNode? GetTargetFolder()
    {
        var data = _tree.SelectedNode?.Tag as BookmarkNode;
        if (data is null)
        {
            return null;
        }

        return data.Type == BookmarkNodeType.Folder ? data : data.Parent;
    }

    private void EditSelectedBookmark()
    {
        var data = SelectedBookmark();
        if (data is null)
        {
            return;
        }

        using var dialog = new BookmarkEditDialog("编辑书签", data.Name, data.Url);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var command = new UpdateNodeCommand(_document, data, dialog.ItemName, dialog.ItemUrl);
        _history.Execute(command);
        SelectNode(data);
        MarkChanged();
    }

    private void ModifySelectedUrl()
    {
        var data = SelectedBookmark();
        if (data is null)
        {
            return;
        }

        using var dialog = new TextInputDialog("修改网址", "网址", data.Url);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var command = new UpdateNodeCommand(_document, data, data.Name, dialog.Value);
        _history.Execute(command);
        SelectNode(data);
        MarkChanged();
    }

    private void RenameSelected()
    {
        var data = _tree.SelectedNode?.Tag as BookmarkNode;
        if (data is null)
        {
            return;
        }

        var fieldName = data.Type == BookmarkNodeType.Folder ? "文件夹名称" : "书签名称";
        using var dialog = new TextInputDialog("重命名", fieldName, data.Name);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var command = new UpdateNodeCommand(_document, data, dialog.Value, data.Url);
        _history.Execute(command);
        SelectNode(data);
        MarkChanged();
    }

    private void DeleteSelected()
    {
        var treeNode = _tree.SelectedNode;
        if (treeNode?.Tag is not BookmarkNode data)
        {
            return;
        }

        if (data.Type == BookmarkNodeType.Folder && data.Children.Count > 0)
        {
            var count = CountDescendants(data);
            var result = MessageBox.Show(
                this,
                $"文件夹“{data.Name}”包含 {count} 个子项目，确定要删除吗？",
                "删除文件夹",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (result != DialogResult.Yes)
            {
                return;
            }
        }

        var nextTreeNode = treeNode.NextNode ?? treeNode.PrevNode ?? treeNode.Parent;
        var focusAfter = nextTreeNode?.Tag as BookmarkNode;
        var command = new DeleteNodeCommand(_document, data, focusAfter);
        _history.Execute(command);
        SelectNode(focusAfter);
        MarkChanged();
    }

    private void CutSelected()
    {
        var data = _tree.SelectedNode?.Tag as BookmarkNode;
        if (data is null)
        {
            return;
        }

        var oldNode = _clipboardNode;
        var oldIsCut = _clipboardIsCut;
        _history.Execute(new ClipboardCommand(
            () => ApplyClipboard(data, isCut: true),
            () => ApplyClipboard(oldNode, oldIsCut)));
        UpdateStatus();
    }

    private void CopySelected()
    {
        var data = _tree.SelectedNode?.Tag as BookmarkNode;
        if (data is null)
        {
            return;
        }

        var oldNode = _clipboardNode;
        var oldIsCut = _clipboardIsCut;
        _history.Execute(new ClipboardCommand(
            () => ApplyClipboard(data, isCut: false),
            () => ApplyClipboard(oldNode, oldIsCut)));
        UpdateStatus();
    }

    private void ApplyClipboard(BookmarkNode? node, bool isCut)
    {
        ClearCutState();
        _clipboardNode = node;
        _clipboardIsCut = isCut;

        if (isCut && node != null)
        {
            _cutNode = node;
            if (_nodeMap.TryGetValue(node, out var treeNode))
            {
                treeNode.Text = GetNodeText(node);
                treeNode.ForeColor = SystemColors.GrayText;
            }
        }

        UpdateStatus();
    }

    private void PasteSelected()
    {
        var source = _clipboardNode;
        if (source is null)
        {
            return;
        }

        var parent = GetTargetFolder();
        if (source.Type == BookmarkNodeType.Folder && parent != null && IsSameOrDescendant(parent, source))
        {
            MessageBox.Show(
                this,
                "不能将文件夹粘贴到自身或其子文件夹中。",
                "粘贴",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        BookmarkNode pasted;
        if (_clipboardIsCut)
        {
            var index = parent is null ? _document.Roots.Count : parent.Children.Count;
            var command = new MoveNodeCommand(_document, source, parent, index);
            _history.Execute(command);
            _clipboardNode = null;
            _clipboardIsCut = false;
            ClearCutState();
            pasted = source;
        }
        else
        {
            var clone = CloneNode(source);
            var index = parent is null ? _document.Roots.Count : parent.Children.Count;
            var command = new InsertNodeCommand(_document, clone, parent, index);
            _history.Execute(command);
            pasted = clone;
        }

        SelectNode(pasted);
        MarkChanged();
    }

    private void MoveSelected(int offset)
    {
        var data = _tree.SelectedNode?.Tag as BookmarkNode;
        if (data is null)
        {
            return;
        }

        var siblings = data.Parent?.Children ?? _document.Roots;
        var index = siblings.IndexOf(data);
        var targetIndex = index + offset;
        if (targetIndex < 0 || targetIndex >= siblings.Count)
        {
            return;
        }

        var command = new MoveNodeCommand(_document, data, data.Parent, targetIndex);
        _history.Execute(command);
        SelectNode(data);
        MarkChanged();
    }

    private void Undo()
    {
        var command = _history.Undo();
        if (command is null)
        {
            return;
        }

        if (command.FocusNode != null)
        {
            SelectNode(command.FocusNode);
        }
        else
        {
            _tree.Focus();
        }

        MarkChanged();
    }

    private void Redo()
    {
        var command = _history.Redo();
        if (command is null)
        {
            return;
        }

        if (command.FocusNode != null)
        {
            SelectNode(command.FocusNode);
        }
        else
        {
            _tree.Focus();
        }

        MarkChanged();
    }

    private BookmarkNode? SelectedBookmark()
    {
        return _tree.SelectedNode?.Tag is BookmarkNode { Type: BookmarkNodeType.Bookmark } data ? data : null;
    }

    private static int CountDescendants(BookmarkNode node)
    {
        var count = 0;
        foreach (var child in node.Children)
        {
            count += 1 + CountDescendants(child);
        }

        return count;
    }

    private static bool IsSameOrDescendant(BookmarkNode candidate, BookmarkNode ancestor)
    {
        for (var current = candidate; current != null; current = current.Parent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private static BookmarkNode CloneNode(BookmarkNode source)
    {
        var clone = source.Type == BookmarkNodeType.Folder
            ? BookmarkNode.CreateFolder(source.Name)
            : BookmarkNode.CreateBookmark(source.Name, source.Url);
        clone.AddDate = source.AddDate;
        clone.LastModified = source.LastModified;

        foreach (var child in source.Children)
        {
            clone.AddChild(CloneNode(child));
        }

        return clone;
    }

    private static string ReadClipboardUrl()
    {
        try
        {
            if (!Clipboard.ContainsText())
            {
                return string.Empty;
            }

            var text = Clipboard.GetText().Trim();
            if (Uri.TryCreate(text, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                return text;
            }
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
        }

        return string.Empty;
    }

    private void MarkChanged()
    {
        _document.MarkDirty();
        UpdateStatus();
    }

    private void ClearCutState()
    {
        var cut = _cutNode;
        _cutNode = null;
        if (cut != null && _nodeMap.TryGetValue(cut, out var treeNode))
        {
            treeNode.Text = GetNodeText(cut);
            treeNode.ForeColor = _tree.ForeColor;
        }
    }

    private string GetNodeText(BookmarkNode data) =>
        ReferenceEquals(data, _cutNode) ? $"（已剪切）{data.Name}" : data.Name;

    private void SelectNode(BookmarkNode? data)
    {
        if (data != null && _nodeMap.TryGetValue(data, out var treeNode))
        {
            _tree.SelectedNode = treeNode;
            treeNode.EnsureVisible();
        }
        else
        {
            _tree.SelectedNode = null;
        }

        _tree.Focus();
    }

    private void OnTreeMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right)
        {
            return;
        }

        var hit = _tree.GetNodeAt(e.Location);
        if (hit != null)
        {
            _tree.SelectedNode = hit;
        }
    }

    private void BuildContextMenu()
    {
        _contextMenu.Items.Clear();
        if (_tree.SelectedNode?.Tag is not BookmarkNode data)
        {
            return;
        }

        if (data.Type == BookmarkNodeType.Folder)
        {
            AddContextItem("新建书签", "Ctrl+Shift+N", CreateBookmark);
            AddContextItem("新建文件夹", "Ctrl+N", CreateFolder);
            _contextMenu.Items.Add(new ToolStripSeparator());
            AddContextItem("重命名", "F2", RenameSelected);
            AddContextItem("删除", "Delete", DeleteSelected);
            _contextMenu.Items.Add(new ToolStripSeparator());
            AddContextItem("剪切", "Ctrl+X", CutSelected);
            AddContextItem("复制", "Ctrl+C", CopySelected);
            AddContextItem("粘贴", "Ctrl+V", PasteSelected, _clipboardNode != null);
            _contextMenu.Items.Add(new ToolStripSeparator());
            AddContextItem("上移", "Shift+上方向键", () => MoveSelected(-1));
            AddContextItem("下移", "Shift+下方向键", () => MoveSelected(1));
            _contextMenu.Items.Add(new ToolStripSeparator());
            AddContextItem("展开", null, () => _tree.SelectedNode?.Expand());
            AddContextItem("折叠", null, () => _tree.SelectedNode?.Collapse());
        }
        else
        {
            AddContextItem("打开网址", null, () => OpenUrl(data.Url));
            AddContextItem("编辑书签", null, EditSelectedBookmark);
            AddContextItem("重命名", "F2", RenameSelected);
            AddContextItem("修改网址", null, ModifySelectedUrl);
            _contextMenu.Items.Add(new ToolStripSeparator());
            AddContextItem("删除", "Delete", DeleteSelected);
            AddContextItem("剪切", "Ctrl+X", CutSelected);
            AddContextItem("复制", "Ctrl+C", CopySelected);
            AddContextItem("粘贴", "Ctrl+V", PasteSelected, _clipboardNode != null);
            _contextMenu.Items.Add(new ToolStripSeparator());
            AddContextItem("上移", "Shift+上方向键", () => MoveSelected(-1));
            AddContextItem("下移", "Shift+下方向键", () => MoveSelected(1));
        }
    }

    private void AddContextItem(string text, string? shortcut, Action action, bool enabled = true)
    {
        var item = new ToolStripMenuItem(text) { Enabled = enabled };
        if (shortcut != null)
        {
            item.ShortcutKeyDisplayString = shortcut;
        }

        item.Click += (_, _) => action();
        _contextMenu.Items.Add(item);
    }

    private void OnNodeInserted(BookmarkNode data, BookmarkNode? parent, int index)
    {
        var treeNode = CreateTreeNode(data);
        var collection = parent is null ? _tree.Nodes : _nodeMap[parent].Nodes;
        collection.Insert(Math.Clamp(index, 0, collection.Count), treeNode);
        if (parent != null)
        {
            _nodeMap[parent].Expand();
        }
    }

    private void OnNodeRemoved(BookmarkNode data)
    {
        if (_clipboardNode != null && IsSameOrDescendant(_clipboardNode, data))
        {
            _clipboardNode = null;
            _clipboardIsCut = false;
        }

        if (_cutNode != null && IsSameOrDescendant(_cutNode, data))
        {
            _cutNode = null;
        }

        if (_nodeMap.Remove(data, out var treeNode))
        {
            treeNode.Remove();
        }

        RemoveMapEntries(data.Children);
    }

    private void RemoveMapEntries(List<BookmarkNode> nodes)
    {
        foreach (var child in nodes)
        {
            _nodeMap.Remove(child);
            RemoveMapEntries(child.Children);
        }
    }

    private void OnNodeMoved(BookmarkNode data)
    {
        if (!_nodeMap.TryGetValue(data, out var treeNode))
        {
            return;
        }

        treeNode.Remove();
        var collection = data.Parent is null ? _tree.Nodes : _nodeMap[data.Parent].Nodes;
        var index = data.Parent is null
            ? _document.Roots.IndexOf(data)
            : data.Parent.Children.IndexOf(data);
        collection.Insert(Math.Clamp(index, 0, collection.Count), treeNode);

        if (data.Parent != null)
        {
            _nodeMap[data.Parent].Expand();
        }
    }

    private void OnNodeChanged(BookmarkNode data)
    {
        if (_nodeMap.TryGetValue(data, out var treeNode))
        {
            treeNode.Text = GetNodeText(data);
        }
    }

    private void LoadDocument()
    {
        _nodeMap.Clear();
        _tree.BeginUpdate();
        _tree.Nodes.Clear();
        foreach (var root in _document.Roots)
        {
            _tree.Nodes.Add(CreateTreeNode(root));
        }

        _tree.EndUpdate();

        if (_tree.Nodes.Count > 0)
        {
            _tree.SelectedNode = _tree.Nodes[0];
        }

        UpdateStatus();
    }

    private TreeNode CreateTreeNode(BookmarkNode data)
    {
        var isFolder = data.Type == BookmarkNodeType.Folder;
        var node = new TreeNode(GetNodeText(data))
        {
            Tag = data,
            ImageIndex = isFolder ? 0 : 1,
            SelectedImageIndex = isFolder ? 0 : 1,
        };
        _nodeMap[data] = node;

        foreach (var child in data.Children)
        {
            node.Nodes.Add(CreateTreeNode(child));
        }

        if (isFolder)
        {
            node.Expand();
        }

        return node;
    }

    private void OnTreeKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Shift && e.KeyCode == Keys.Up)
        {
            MoveSelected(-1);
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        if (e.Shift && e.KeyCode == Keys.Down)
        {
            MoveSelected(1);
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        if (e.KeyCode != Keys.Enter)
        {
            return;
        }

        e.Handled = true;
        e.SuppressKeyPress = true;

        if (_tree.SelectedNode?.Tag is not BookmarkNode data)
        {
            return;
        }

        if (data.Type == BookmarkNodeType.Folder)
        {
            _tree.SelectedNode.Toggle();
        }
        else
        {
            OpenUrl(data.Url);
        }
    }

    private void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"无法打开网址：{ex.Message}",
                "打开网址",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void UpdateStatus()
    {
        string selectionText;
        if (_tree.SelectedNode?.Tag is not BookmarkNode data)
        {
            selectionText = "未选中项目";
        }
        else
        {
            var typeText = data.Type == BookmarkNodeType.Folder ? "文件夹" : "书签";
            var parentText = data.Parent?.Name ?? "顶层";
            selectionText = $"类型：{typeText}；名称：{data.Name}；父文件夹：{parentText}";
        }

        _statusBar.SetTexts(selectionText, _document.IsDirty ? "已修改" : "未修改");

        var selected = _tree.SelectedNode?.Tag as BookmarkNode;
        _editBookmarkItem.Enabled = selected?.Type == BookmarkNodeType.Bookmark;
        _renameItem.Enabled = selected != null;
        _deleteItem.Enabled = selected != null;
        _cutItem.Enabled = selected != null;
        _copyItem.Enabled = selected != null;
        _pasteItem.Enabled = _clipboardNode != null;
        _moveUpItem.Enabled = selected != null;
        _moveDownItem.Enabled = selected != null;
        _undoItem.Enabled = _history.CanUndo;
        _redoItem.Enabled = _history.CanRedo;

        var title = _document.FilePath is null
            ? "书签编辑器"
            : $"书签编辑器 - {Path.GetFileName(_document.FilePath)}";
        Text = _document.IsDirty ? $"{title} *" : title;
    }

    private sealed class ClipboardCommand : IEditCommand
    {
        private readonly Action _redo;
        private readonly Action _undo;

        public ClipboardCommand(Action redo, Action undo)
        {
            _redo = redo;
            _undo = undo;
        }

        public BookmarkNode? FocusNode => null;

        public void Undo() => _undo();

        public void Redo() => _redo();
    }
}
