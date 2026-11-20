namespace BookmarkEditor.UI;

public sealed class TextInputDialog : Form
{
    private readonly TextBox _box;
    private readonly string _fieldName;

    public TextInputDialog(string title, string fieldName, string initialValue)
    {
        _fieldName = fieldName;
        Text = title;
        AccessibleName = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(400, 120);

        var label = new Label { Text = fieldName, Location = new Point(12, 15), AutoSize = true };
        _box = new TextBox
        {
            Location = new Point(100, 12),
            Width = 280,
            Text = initialValue,
            AccessibleName = fieldName,
        };

        var okButton = new Button { Text = "确定", Location = new Point(200, 70), Width = 80, AccessibleName = "确定" };
        var cancelButton = new Button
        {
            Text = "取消",
            Location = new Point(290, 70),
            Width = 80,
            DialogResult = DialogResult.Cancel,
            AccessibleName = "取消",
        };
        okButton.Click += (_, _) => Confirm();
        AcceptButton = okButton;
        CancelButton = cancelButton;

        Controls.Add(label);
        Controls.Add(_box);
        Controls.Add(okButton);
        Controls.Add(cancelButton);

        Shown += (_, _) => _box.Focus();
    }

    public string Value => _box.Text.Trim();

    private void Confirm()
    {
        if (Value.Length == 0)
        {
            MessageBox.Show(this, $"请输入{_fieldName}。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _box.Focus();
            return;
        }

        DialogResult = DialogResult.OK;
    }
}
