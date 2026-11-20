using BookmarkEditor.Model;
using System.Text;

namespace BookmarkEditor.Exporting;

public static class BookmarkHtmlWriter
{
    public static string Serialize(IReadOnlyList<BookmarkNode> roots)
    {
        Validate(roots);

        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE NETSCAPE-Bookmark-file-1>");
        builder.AppendLine("<!-- This is an automatically generated file.");
        builder.AppendLine("     It will be read and overwritten.");
        builder.AppendLine("     DO NOT EDIT! -->");
        builder.AppendLine("<META HTTP-EQUIV=\"Content-Type\" CONTENT=\"text/html; charset=UTF-8\">");
        builder.AppendLine("<TITLE>Bookmarks</TITLE>");
        builder.AppendLine("<H1>Bookmarks</H1>");
        builder.AppendLine("<DL><p>");

        foreach (var root in roots)
        {
            WriteNode(builder, root, 1);
        }

        builder.AppendLine("</DL><p>");
        return builder.ToString();
    }

    public static void Validate(IReadOnlyList<BookmarkNode> roots)
    {
        var visited = new HashSet<BookmarkNode>();
        foreach (var root in roots)
        {
            if (root.Parent != null)
            {
                throw new InvalidDataException("书签数据结构异常：顶层节点仍有父节点。");
            }

            Walk(root, visited);
        }
    }

    private static void Walk(BookmarkNode node, HashSet<BookmarkNode> visited)
    {
        if (!visited.Add(node))
        {
            throw new InvalidDataException("书签数据结构异常：存在重复或循环引用的节点。");
        }

        foreach (var child in node.Children)
        {
            if (!ReferenceEquals(child.Parent, node))
            {
                throw new InvalidDataException($"书签数据结构异常：节点“{node.Name}”的子节点父链接不一致。");
            }

            Walk(child, visited);
        }
    }

    private static void WriteNode(StringBuilder builder, BookmarkNode node, int depth)
    {
        var indent = new string(' ', depth * 4);

        if (node.Type == BookmarkNodeType.Folder)
        {
            builder.Append(indent).Append("<DT><H3");
            AppendDate(builder, " ADD_DATE", node.AddDate);
            AppendDate(builder, " LAST_MODIFIED", node.LastModified);
            if (node.IsToolbarFolder)
            {
                builder.Append(" PERSONAL_TOOLBAR_FOLDER=\"true\"");
            }

            builder.Append('>').Append(Encode(node.Name)).AppendLine("</H3>");
            builder.Append(indent).AppendLine("<DL><p>");

            foreach (var child in node.Children)
            {
                WriteNode(builder, child, depth + 1);
            }

            builder.Append(indent).AppendLine("</DL><p>");
        }
        else
        {
            builder.Append(indent).Append("<DT><A HREF=\"").Append(Encode(node.Url)).Append('"');
            AppendDate(builder, " ADD_DATE", node.AddDate);
            builder.Append('>').Append(Encode(node.Name)).AppendLine("</A>");
        }
    }

    private static void AppendDate(StringBuilder builder, string attribute, DateTime? value)
    {
        var seconds = ToUnixSeconds(value);
        if (seconds is null)
        {
            return;
        }

        builder.Append(attribute).Append("=\"").Append(seconds.Value).Append('"');
    }

    private static long? ToUnixSeconds(DateTime? value)
    {
        if (value is null)
        {
            return null;
        }

        var moment = value.Value.Kind == DateTimeKind.Utc
            ? new DateTimeOffset(value.Value)
            : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Local));
        return moment.ToUnixTimeSeconds();
    }

    private static string Encode(string text)
    {
        var builder = new StringBuilder(text.Length + 8);
        foreach (var ch in text)
        {
            switch (ch)
            {
                case '&':
                    builder.Append("&amp;");
                    break;
                case '<':
                    builder.Append("&lt;");
                    break;
                case '>':
                    builder.Append("&gt;");
                    break;
                case '"':
                    builder.Append("&quot;");
                    break;
                default:
                    builder.Append(ch);
                    break;
            }
        }

        return builder.ToString();
    }
}
