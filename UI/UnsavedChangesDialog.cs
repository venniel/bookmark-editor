namespace BookmarkEditor.UI;

public sealed class UnsavedChangesDialog : Form
{
    public UnsavedChangesDialog()
    {
        Text = "未保存的修改";
        AccessibleName = Text;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(420, 130);

        var label = new Label
        {
            Text = "当前文档存在未保存的修改。",
            Location = new Point(16, 20),
            AutoSize = true,
        };

        var saveButton = new Button
        {
            Text = "保存",
            DialogResult = DialogResult.OK,
            Location = new Point(112, 76),
            Width = 90,
            AccessibleName = "保存",
        };
        var discardButton = new Button
        {
            Text = "不保存",
            DialogResult = DialogResult.No,
            Location = new Point(208, 76),
            Width = 90,
            AccessibleName = "不保存",
        };
        var cancelButton = new Button
        {
            Text = "取消",
            DialogResult = DialogResult.Cancel,
            Location = new Point(304, 76),
            Width = 90,
            AccessibleName = "取消",
        };

        AcceptButton = saveButton;
        CancelButton = cancelButton;

        Controls.Add(label);
        Controls.Add(saveButton);
        Controls.Add(discardButton);
        Controls.Add(cancelButton);
    }
}
