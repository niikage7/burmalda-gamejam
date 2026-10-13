// Builds the "inspector parameters" pages from [Header]/[Tooltip]/[Min]/[Range] attributes of serialized fields.
// DocFX reads only XML comments, so without this the tooltips (all the balance numbers) would not reach the site.
//
// Run from the repository root:  dotnet run Documentation/tools/InspectorParams.cs
// Writes:
//   Documentation/obj/overwrite/<Type>.md  — a "Fields in the inspector" table added to each class's API page;
//   Documentation/articles/inspector.md    — all tables on one page, grouped by namespace.
// Both are generated: edit the tooltips in code, not these files.

using System.Text;
using System.Text.RegularExpressions;

const string ScriptsDir = "Assets/_Project/Scripts";
const string OverwriteDir = "Documentation/obj/overwrite";
const string ArticlePath = "Documentation/articles/inspector.md";

if (!Directory.Exists(ScriptsDir))
{
    Console.Error.WriteLine($"{ScriptsDir} not found: run from the repository root.");
    return 1;
}

var namespaceRx = new Regex(@"^\s*namespace\s+([\w.]+)");
var typeRx = new Regex(@"^\s*(?<vis>(?:public|internal|private|protected)\s+)*(?:static\s+|abstract\s+|sealed\s+|readonly\s+)*(?:partial\s+)?(class|struct)\s+(\w+)");
var headerRx = new Regex(@"\[Header\(""((?:[^""\\]|\\.)*)""\)\]");
var tooltipRx = new Regex(@"Tooltip\(""((?:[^""\\]|\\.)*)""\)");
// "= value" is a default; "=> value" is a property, not a field.
var fieldRx = new Regex(@"^\s*(?<attrs>(?:\[[^\]]*\]\s*)*)(?<mods>(?:(?:public|private|protected|internal|static|readonly|const|event)\s+)*)(?<type>[\w.<>\[\], ]+?)\s+(?<name>\w+)\s*(?:=(?!>)\s*(?<def>[^;]+))?;\s*(?://.*)?$");

var tables = new List<TypeTable>();

foreach (string file in Directory.GetFiles(ScriptsDir, "*.cs", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
{
    string ns = "";
    var typeStack = new Stack<(string Name, int Depth, bool Serializable)>();
    int depth = 0;
    string? header = null;
    var pendingAttrs = new StringBuilder();

    foreach (string line in File.ReadLines(file))
    {
        string trimmed = line.Trim();

        Match nsMatch = namespaceRx.Match(line);
        if (nsMatch.Success)
            ns = nsMatch.Groups[1].Value;

        Match typeMatch = typeRx.Match(line);
        if (typeMatch.Success)
        {
            // Unity serializes fields of components, ScriptableObjects and [Serializable] types only.
            // A non-public type (such as a private save-data class) has no API page to link to, so it is skipped too.
            string outer = typeStack.Count > 0 ? typeStack.Peek().Name + "." : "";
            bool serializable = typeMatch.Groups["vis"].Value.Contains("public")
                                && (line.Contains("MonoBehaviour") || line.Contains("ScriptableObject")
                                    || pendingAttrs.ToString().Contains("Serializable"));
            typeStack.Push((outer + typeMatch.Groups[2].Value, depth + 1, serializable));
            header = null;
            pendingAttrs.Clear();
        }
        else if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
        {
            // Attribute lines above a field: collect them until the field itself.
            pendingAttrs.Append(trimmed).Append(' ');
        }
        else if (typeStack.Count > 0 && depth == typeStack.Peek().Depth && typeStack.Peek().Serializable)
        {
            Match field = fieldRx.Match(line);
            if (field.Success)
            {
                string attrs = pendingAttrs + field.Groups["attrs"].Value;
                string mods = field.Groups["mods"].Value;
                bool serialized = !mods.Contains("event")
                                  && (attrs.Contains("SerializeField")
                                      || (mods.Contains("public") && !mods.Contains("static") && !mods.Contains("readonly") && !mods.Contains("const")));
                bool hidden = attrs.Contains("NonSerialized") || attrs.Contains("HideInInspector");

                Match h = headerRx.Match(attrs);
                if (h.Success)
                    header = Unescape(h.Groups[1].Value);

                if (serialized && !hidden)
                {
                    string fullType = ns.Length > 0 ? ns + "." + typeStack.Peek().Name : typeStack.Peek().Name;
                    TypeTable table = tables.FirstOrDefault(t => t.FullName == fullType)
                                      ?? Add(tables, new TypeTable(fullType, ns, Path.GetRelativePath(".", file).Replace('\\', '/')));
                    table.Fields.Add(new FieldRow(
                        header,
                        field.Groups["name"].Value,
                        field.Groups["type"].Value.Trim(),
                        field.Groups["def"].Success ? field.Groups["def"].Value.Trim() : "",
                        Limits(attrs),
                        tooltipRx.Match(attrs) is { Success: true } tip ? Unescape(tip.Groups[1].Value) : ""));
                }
            }

            pendingAttrs.Clear();
        }
        else if (!trimmed.StartsWith("[") && trimmed.Length > 0 && !trimmed.StartsWith("//"))
        {
            pendingAttrs.Clear();
        }

        foreach (char c in StripStrings(line))
        {
            if (c == '{')
                depth++;
            else if (c == '}')
            {
                depth--;
                while (typeStack.Count > 0 && depth < typeStack.Peek().Depth)
                    typeStack.Pop();
            }
        }
    }
}

Directory.CreateDirectory(OverwriteDir);
foreach (string old in Directory.GetFiles(OverwriteDir, "*.md"))
    File.Delete(old);

foreach (TypeTable table in tables)
{
    var sb = new StringBuilder();
    sb.AppendLine("---");
    sb.AppendLine($"uid: {table.FullName}");
    sb.AppendLine("remarks: *content");
    sb.AppendLine("---");
    sb.AppendLine("### Поля в инспекторе");
    sb.AppendLine();
    AppendTable(sb, table);
    File.WriteAllText(Path.Combine(OverwriteDir, table.FullName + ".md"), sb.ToString());
}

var article = new StringBuilder();
article.AppendLine("<!-- Сгенерировано Documentation/tools/InspectorParams.cs. Не править вручную: меняйте [Tooltip] в коде. -->");
article.AppendLine("# Параметры в инспекторе");
article.AppendLine();
article.AppendLine("Все поля, которые видны в инспекторе Unity, с описанием из `[Tooltip]` и значением по умолчанию из кода.");
article.AppendLine("Реальные значения в ассетах (`ScriptableObjects/…`) и на префабах могут отличаться — смотрите в редакторе.");
article.AppendLine("Числа баланса берутся из `tank_balance_v0_2.md`.");
article.AppendLine();
article.AppendLine("Если вы не программист, начните со страницы [Как настраивать игру](../game/tuning.md): там по-русски разобрано, какой файл открыть и что крутить. «Не меньше» и «Диапазон» — ограничения, которые Unity не даст нарушить в инспекторе.");
article.AppendLine();
foreach (IGrouping<string, TypeTable> group in tables.GroupBy(t => t.Namespace))
{
    article.AppendLine($"## {group.Key}");
    article.AppendLine();
    foreach (TypeTable table in group)
    {
        article.AppendLine($"### <xref:{table.FullName}>");
        article.AppendLine();
        article.AppendLine($"Файл: `{table.File}`");
        article.AppendLine();
        AppendTable(article, table);
    }
}
Directory.CreateDirectory(Path.GetDirectoryName(ArticlePath)!);
File.WriteAllText(ArticlePath, article.ToString());

Console.WriteLine($"Inspector parameters: {tables.Count} types, {tables.Sum(t => t.Fields.Count)} fields.");
return 0;

static void AppendTable(StringBuilder sb, TypeTable table)
{
    string? currentHeader = null;
    bool open = false;
    foreach (FieldRow row in table.Fields)
    {
        if (!open || row.Header != currentHeader)
        {
            if (open)
                sb.AppendLine();
            if (row.Header != null)
                sb.AppendLine($"**{Cell(row.Header)}**").AppendLine();
            sb.AppendLine("| Поле | Тип | По умолчанию | Описание |");
            sb.AppendLine("| --- | --- | --- | --- |");
            currentHeader = row.Header;
            open = true;
        }

        string description = row.Limits.Length > 0
            ? (row.Tooltip.Length > 0 ? $"{Cell(row.Tooltip)} <br>*{row.Limits}*" : $"*{row.Limits}*")
            : Cell(row.Tooltip);
        string def = row.Default.Length > 0 ? $"`{Cell(row.Default)}`" : "";
        sb.AppendLine($"| `{row.Name}` | `{Cell(row.Type)}` | {def} | {description} |");
    }

    sb.AppendLine();
}

static string Limits(string attrs)
{
    if (rangeRx().Match(attrs) is { Success: true } r)
        return $"Диапазон {Num(r.Groups[1].Value)} … {Num(r.Groups[2].Value)}";
    if (minRx().Match(attrs) is { Success: true } m)
        return $"Не меньше {Num(m.Groups[1].Value)}";
    return "";
}

static Regex rangeRx() => new(@"\bRange\(([^,]+),([^)]+)\)");
static Regex minRx() => new(@"\bMin\(([^)]*)\)");

static string Num(string s) => s.Trim().TrimEnd('f', 'F');

static string Cell(string s) => s.Replace("|", "\\|").Replace("\n", " ");

static string Unescape(string s) => s.Replace("\\\"", "\"").Replace("\\\\", "\\");

// Braces inside string and char literals must not change the nesting depth.
static string StripStrings(string line)
{
    int comment = line.IndexOf("//", StringComparison.Ordinal);
    string code = comment >= 0 ? line[..comment] : line;
    return Regex.Replace(code, @"""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*'", "");
}

static T Add<T>(List<T> list, T item)
{
    list.Add(item);
    return item;
}

record FieldRow(string? Header, string Name, string Type, string Default, string Limits, string Tooltip);

record TypeTable(string FullName, string Namespace, string File)
{
    public List<FieldRow> Fields { get; } = new();
}
