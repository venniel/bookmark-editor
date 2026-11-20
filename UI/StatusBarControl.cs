namespace BookmarkEditor.UI;

internal sealed class StatusBarControl : Control
{
    private readonly int[] _parts = new int[2];
    private string _leftText = string.Empty;
    private string _rightText = string.Empty;

    public StatusBarControl()
    {
        SetStyle(ControlStyles.Selectable, false);
        SetStyle(ControlStyles.ResizeRedraw, true);
        Dock = DockStyle.Bottom;
        Height = Font.Height + 6;
        AccessibleRole = AccessibleRole.StatusBar;
    }

    public void SetTexts(string left, string right)
    {
        _leftText = left;
        _rightText = right;
        AccessibleName = $"{left} {right}";
        UpdateParts();
        Invalidate();
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new StatusBarAccessibleObject(this);

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateParts();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Height = Font.Height + 6;
        UpdateParts();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var bounds = ClientRectangle;
        e.Graphics.Clear(SystemColors.Control);

        using (var dark = new Pen(SystemColors.ControlDark))
        using (var light = new Pen(SystemColors.ControlLightLight))
        {
            e.Graphics.DrawLine(dark, 0, 0, bounds.Width, 0);
            e.Graphics.DrawLine(light, 0, 1, bounds.Width, 1);
        }

        var split = _parts[0] > 0 ? _parts[0] : bounds.Width;
        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine
            | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.Left;

        var leftBounds = new Rectangle(4, 2, Math.Max(0, split - 6), bounds.Height - 4);
        TextRenderer.DrawText(e.Graphics, _leftText, Font, leftBounds, SystemColors.ControlText, flags);

        if (split < bounds.Width)
        {
            using var separator = new Pen(SystemColors.ControlDark);
            e.Graphics.DrawLine(separator, split, 3, split, bounds.Height - 3);

            var rightBounds = new Rectangle(split + 4, 2, Math.Max(0, bounds.Width - split - 8), bounds.Height - 4);
            TextRenderer.DrawText(e.Graphics, _rightText, Font, rightBounds, SystemColors.ControlText,
                flags | TextFormatFlags.Right);
        }
    }

    private void UpdateParts()
    {
        var rightWidth = TextRenderer.MeasureText(_rightText, Font).Width + 16;
        _parts[0] = Math.Max(0, Width - Math.Max(40, rightWidth));
        _parts[1] = -1;
    }

    private sealed class StatusBarAccessibleObject : ControlAccessibleObject
    {
        private readonly StatusBarControl _owner;

        public StatusBarAccessibleObject(StatusBarControl owner)
            : base(owner)
        {
            _owner = owner;
        }

        public override AccessibleRole Role => AccessibleRole.StatusBar;

        public override string? Name => $"{_owner._leftText} {_owner._rightText}";

        public override int GetChildCount() => 2;

        public override AccessibleObject? GetChild(int index) =>
            index is 0 or 1 ? new StatusPartAccessibleObject(_owner, index) : null;
    }

    private sealed class StatusPartAccessibleObject : AccessibleObject
    {
        private readonly StatusBarControl _owner;
        private readonly int _index;

        public StatusPartAccessibleObject(StatusBarControl owner, int index)
        {
            _owner = owner;
            _index = index;
        }

        public override string? Name => _index == 0 ? _owner._leftText : _owner._rightText;

        public override AccessibleRole Role => AccessibleRole.StaticText;

        public override AccessibleObject? Parent => _owner.AccessibilityObject;

        public override Rectangle Bounds
        {
            get
            {
                var split = _owner._parts[0] > 0 ? _owner._parts[0] : _owner.Width;
                var x = _index == 0 ? 0 : split;
                var width = Math.Max(0, (_index == 0 ? split : _owner.Width) - x);
                return _owner.RectangleToScreen(new Rectangle(x, 0, width, _owner.Height));
            }
        }
    }
}
