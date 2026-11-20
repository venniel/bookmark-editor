using BookmarkEditor.Model;
using HtmlAgilityPack;
using System.Text;
using System.Text.RegularExpressions;
using HtmlDocument = HtmlAgilityPack.HtmlDocument;

namespace BookmarkEditor.Parsing;

public static class BookmarkHtmlParser
{
    // 浏览器导出的 </DL><p> 与 <DL><p> 会让 HAP 把后续同级目录错误嵌套进上一个目录，
    // 而 <p> 在书签格式中没有实际含义，加载前移除。
    private static readonly Regex ParagraphTagPattern = new(@"</?p(\s[^>]*)?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static List<BookmarkNode> Parse(string path)
    {
        var probe = new HtmlDocument();
        probe.Load(path);
        var encoding = probe.Encoding ?? Encoding.UTF8;

        var html = new HtmlDocument();
        html.LoadHtml(ParagraphTagPattern.Replace(File.ReadAllText(path, encoding), string.Empty));

        var roots = new List<BookmarkNode>();
        var consumedLists = new HashSet<HtmlNode>();
        var topLevelLists = html.DocumentNode.SelectNodes("//dl[not(ancestor::dl)]");
        if (topLevelLists != null)
        {
            foreach (var list in topLevelLists)
            {
                ParseList(list, roots, null, consumedLists);
            }
        }

        if (roots.Count == 0)
        {
            throw new InvalidDataException("文件中没有找到书签或文件夹。请确认选择的是浏览器导出的书签 HTML 文件。");
        }

        return roots;
    }

    private static void ParseList(HtmlNode list, List<BookmarkNode> target, BookmarkNode? parent, HashSet<HtmlNode> consumedLists)
    {
        foreach (var child in list.ChildNodes)
        {
            if (child.NodeType != HtmlNodeType.Element || consumedLists.Contains(child))
            {
                continue;
            }

            switch (child.Name)
            {
                case "dt":
                    ParseItem(child, target, parent, consumedLists);
                    break;
                case "p":
                case "dl":
                    ParseList(child, target, parent, consumedLists);
                    break;
            }
        }
    }

    private static void ParseItem(HtmlNode item, List<BookmarkNode> target, BookmarkNode? parent, HashSet<HtmlNode> consumedLists)
    {
        var heading = FirstElementChild(item, "h3");
        if (heading != null)
        {
            var folder = BookmarkNode.CreateFolder(DecodeText(heading.InnerText));
            folder.AddDate = ParseTimestamp(heading, "add_date");
            folder.LastModified = ParseTimestamp(heading, "last_modified");
            folder.IsToolbarFolder = string.Equals(
                heading.GetAttributeValue("personal_toolbar_folder", null),
                "true",
                StringComparison.OrdinalIgnoreCase);

            var nestedList = FirstElementChild(item, "dl");
            if (nestedList == null)
            {
                var next = NextElementSibling(item);
                if (next != null && next.Name == "dl")
                {
                    nestedList = next;
                    consumedLists.Add(next);
                }
            }

            if (nestedList != null)
            {
                ParseList(nestedList, folder.Children, folder, consumedLists);
            }

            AddNode(target, parent, folder);
            return;
        }

        var link = FirstElementChild(item, "a");
        if (link != null)
        {
            var bookmark = BookmarkNode.CreateBookmark(
                DecodeText(link.InnerText),
                DecodeText(link.GetAttributeValue("href", string.Empty)));
            bookmark.AddDate = ParseTimestamp(link, "add_date");
            bookmark.LastModified = ParseTimestamp(link, "last_modified");
            AddNode(target, parent, bookmark);
        }
    }

    private static void AddNode(List<BookmarkNode> target, BookmarkNode? parent, BookmarkNode node)
    {
        if (parent is null)
        {
            target.Add(node);
        }
        else
        {
            parent.AddChild(node);
        }
    }

    private static readonly Regex NumericEntityPattern =
        new(@"&#(?:(?<dec>\d+)|[xX](?<hex>[0-9a-fA-F]+));", RegexOptions.Compiled);

    private static string DecodeText(string raw)
    {
        var decoded = HtmlEntity.DeEntitize(raw);
        decoded = NumericEntityPattern.Replace(decoded, match =>
        {
            int value;
            if (match.Groups["dec"].Success)
            {
                int.TryParse(match.Groups["dec"].Value, out value);
            }
            else
            {
                value = Convert.ToInt32(match.Groups["hex"].Value, 16);
            }

            return value is > 0 and <= 0x10FFFF ? char.ConvertFromUtf32(value) : match.Value;
        });

        return decoded.Trim();
    }

    private static HtmlNode? FirstElementChild(HtmlNode parent, string name) =>
        parent.ChildNodes.FirstOrDefault(n => n.NodeType == HtmlNodeType.Element && n.Name == name);

    private static HtmlNode? NextElementSibling(HtmlNode node)
    {
        var next = node.NextSibling;
        while (next != null && next.NodeType != HtmlNodeType.Element)
        {
            next = next.NextSibling;
        }

        return next;
    }

    private static DateTime? ParseTimestamp(HtmlNode node, string attributeName)
    {
        var value = node.GetAttributeValue(attributeName, null);
        if (value == null || !long.TryParse(value, out var seconds) || seconds <= 0)
        {
            return null;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds).LocalDateTime;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
