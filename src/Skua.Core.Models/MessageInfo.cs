namespace Skua.Core.Models;
public class MessageInfo
{
public string Content { get; set; }
public bool Send { get; set; } = true;
public MessageInfo(string content)
{
Content = content;
}
}
