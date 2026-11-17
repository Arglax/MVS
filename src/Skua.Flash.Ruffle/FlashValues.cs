using System.Dynamic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
namespace Skua.Flash.Ruffle;
public static class FlashValues
{
public static string SerializeArgs(object?[] args)
{
using MemoryStream ms = new();
using (Utf8JsonWriter w = new(ms))
{
w.WriteStartArray();
foreach (object? arg in args)
WriteValue(w, arg);
w.WriteEndArray();
}
return Encoding.UTF8.GetString(ms.ToArray());
}
private static void WriteValue(Utf8JsonWriter w, object? o)
{
switch (o)
{
case null:
w.WriteNullValue();
return;
case bool b:
w.WriteBooleanValue(b);
return;
case int i:
w.WriteNumberValue(i);
return;
case long l:
w.WriteNumberValue(l);
return;
case float f:
w.WriteNumberValue(float.IsFinite(f) ? f : 0);
return;
case double d:
w.WriteNumberValue(double.IsFinite(d) ? d : 0);
return;
case ExpandoObject expando:
w.WriteStartObject();
foreach (KeyValuePair<string, object?> kvp in expando)
{
w.WritePropertyName(kvp.Key);
WriteValue(w, kvp.Value);
}
w.WriteEndObject();
return;
case Array array:
w.WriteStartArray();
foreach (object? el in array)
WriteValue(w, el);
w.WriteEndArray();
return;
default:
w.WriteStringValue(o.ToString());
return;
}
}
public static string? ResultToString(string? rawJson)
{
if (rawJson is null)
return null;
using JsonDocument doc = JsonDocument.Parse(rawJson);
JsonElement el = doc.RootElement;
return el.ValueKind switch
{
JsonValueKind.String => el.GetString(),
JsonValueKind.Number => el.GetRawText(),
JsonValueKind.True => "true",
JsonValueKind.False => "false",
JsonValueKind.Null or JsonValueKind.Undefined => null,
_ => el.GetRawText()
};
}
public static object[] ExternalArgs(string argsJson)
{
StringBuilder sb = new();
using (JsonDocument doc = JsonDocument.Parse(argsJson))
AppendText(sb, doc.RootElement);
return new object[] { sb.ToString() };
}
private static void AppendText(StringBuilder sb, JsonElement el)
{
switch (el.ValueKind)
{
case JsonValueKind.String:
sb.Append(el.GetString());
break;
case JsonValueKind.Number:
sb.Append(el.GetRawText());
break;
case JsonValueKind.Array:
foreach (JsonElement child in el.EnumerateArray())
AppendText(sb, child);
break;
case JsonValueKind.Object:
foreach (JsonProperty prop in el.EnumerateObject())
AppendText(sb, prop.Value);
break;
}
}
public static object? ConvertResult(string? value, Type type)
{
if (value is null)
return null;
if (type == typeof(string) || type == typeof(object))
return value;
Type target = Nullable.GetUnderlyingType(type) ?? type;
if (target.IsEnum)
return Enum.Parse(target, value, true);
return Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
}
public static object FromFlashXml(XElement el)
{
switch (el.Name.ToString())
{
case "number":
return int.TryParse(el.Value, out int i) ? i : float.TryParse(el.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : 0;
case "true":
return true;
case "false":
return false;
case "null":
return null!;
case "array":
return el.Elements().Select(e => FromFlashXml(e)).ToArray();
case "object":
IDictionary<string, object?> d = new ExpandoObject();
foreach (XElement e in el.Elements())
d[e.Attribute("id")!.Value] = FromFlashXml(e.Elements().First());
return d;
default:
return el.Value;
}
}
}
