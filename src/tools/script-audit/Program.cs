using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
string scripts = args[0], refsDir = args[1], servicesCs = args[2], shimsCs = args[3], outFile = args[4];
string svc = File.ReadAllText(servicesCs);
int a = svc.IndexOf("compiler.AddNamespaces(new[]");
int b = svc.IndexOf("});", a);
var namespaces = Regex.Matches(svc[a..b], "\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
string usings = string.Concat(namespaces.Select(n => $"using {n};\n"));
int usingLines = namespaces.Count;
string shims = File.ReadAllText(shimsCs);
int s0 = shims.IndexOf("\"\"\"") + 3, s1 = shims.LastIndexOf("\"\"\"");
string prelude = shims[s0..s1];
var parse = new CSharpParseOptions(LanguageVersion.Latest, DocumentationMode.None);
var trees = new List<SyntaxTree>();
foreach (string f in Directory.EnumerateFiles(scripts, "*.cs", SearchOption.AllDirectories))
{
string rel = Path.GetRelativePath(scripts, f).Replace(Path.DirectorySeparatorChar, '/');
if (rel.StartsWith(".")) continue;
trees.Add(CSharpSyntaxTree.ParseText((usings + Regex.Replace(File.ReadAllText(f), @"(?<![\w.])(System\.Diagnostics\.)?Process\.Start\(", "global::ScriptShell.Start(")).Trim(), parse, rel));
}
trees.Add(CSharpSyntaxTree.ParseText(prelude, parse, "<prelude>"));
var refs = Directory.EnumerateFiles(refsDir, "*.dll").Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToList();
var comp = CSharpCompilation.Create("audit", trees, refs,
new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, concurrentBuild: true));
var together = new HashSet<string> { "CS0101", "CS0111", "CS0102", "CS0229", "CS0121", "CS0104", "CS0260", "CS0579", "CS0262", "CS8863", "CS0100", "CS0128", "CS0756", "CS0759", "CS0263", "CS0433", "CS0436" };
var groups = new Dictionary<string, List<string>>();
int total = 0;
foreach (Diagnostic d in comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error))
{
if (together.Contains(d.Id) && d.Location.GetLineSpan().Path != "<prelude>") continue;
total++;
var span = d.Location.GetLineSpan();
string key = d.Id + " " + d.GetMessage();
if (!groups.TryGetValue(key, out var list)) groups[key] = list = new();
list.Add($"{span.Path}:{span.StartLinePosition.Line + 1 - usingLines}");
}
var sb = new StringBuilder();
sb.AppendLine($"scripts {trees.Count - 1}, refs {refs.Count}, errors {total}, kinds {groups.Count}, files {groups.Values.SelectMany(v => v).Select(v => v.Split(':')[0]).Distinct().Count()}");
foreach (var g in groups.OrderByDescending(g => g.Value.Count))
{
sb.AppendLine($"[{g.Value.Count}] {g.Key}");
foreach (string w in g.Value.Distinct().Take(6)) sb.AppendLine("     " + w);
}
File.WriteAllText(outFile, sb.ToString());
Console.WriteLine(sb.ToString().Length > 9000 ? sb.ToString()[..9000] : sb.ToString());
