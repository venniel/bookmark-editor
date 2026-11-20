namespace BookmarkEditor.UI;

public sealed class BookmarkEditDialog : Form
{
    private readonly TextBox _nameBox;
    private readonly TextBox _urlBox;

    public BookmarkEditDialog(string title, string name, string url)
    {
        Text = title;
        AccessibleName = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(480, 160);

        var nameLabel = new Label { Text = "名称", Location = new Point(12, 15), AutoSize = true };
        _nameBox = new TextBox
        {
            Location = new Point(80, 12),
            Width = 380,
            Text = name,
            AccessibleName = "书签名称",
        };

        var urlLabel = new Label { Text = "网址", Location = new Point(12, 55), AutoSize = true };
        _urlBox = new TextBox
        {
            Location = new Point(80, 52),
            Width = 380,
            Text = url,
            AccessibleName = "书签网址",
        };

        var okButton = new Button { Text = "确定", Location = new Point(280, 110), Width = 80, AccessibleName = "确定" };
        var cancelButton = new Button
        {
            Text = "取消",
            Location = new Point(370, 110),
            Width = 80,
            DialogResult = DialogResult.Cancel,
            AccessibleName = "取消",
        };
        okButton.Click += (_, _) => Confirm();
        AcceptButton = okButton;
        CancelButton = cancelButton;

        Controls.Add(nameLabel);
        Controls.Add(_nameBox);
        Controls.Add(urlLabel);
        Controls.Add(_urlBox);
        Controls.Add(okButton);
        Controls.Add(cancelButton);

        Shown += (_, _) => _nameBox.Focus();
    }

    public string ItemName => _nameBox.Text.Trim();

    public string ItemUrl => _urlBox.Text.Trim();

    private void Confirm()
    {
        if (ItemName.Length == 0)
        {
            MessageBox.Show(this, "请输入名称。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _nameBox.Focus();
            return;
        }

        if (ItemUrl.Length == 0)
        {
            MessageBox.Show(this, "请输入网址。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _urlBox.Focus();
            return;
        }

        DialogResult = DialogResult.OK;
    }
}
