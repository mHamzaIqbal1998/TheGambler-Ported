using System.Reflection;

// Dumps public members of requested SPT types so we can port against the exact API.
// Also handles enums and searches across the core + DI assemblies.
if (args.Length > 0 && args[0] == "TESTJSON")
{
    Probe.JsonTest.Run(args.Length > 1 ? args[1] : Directory.GetCurrentDirectory());
    return;
}

var core = typeof(SPTarkov.Server.Core.Servers.DatabaseServer).Assembly;
var diPath = Path.Combine(Path.GetDirectoryName(core.Location)!, "SPTarkov.DI.dll");
var assemblies = new List<Assembly> { core };
if (File.Exists(diPath)) assemblies.Add(Assembly.LoadFrom(diPath));
else
{
    var alt = Directory.GetFiles(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\.nuget\packages\sptarkov.di", "SPTarkov.DI.dll", SearchOption.AllDirectories);
    if (alt.Length > 0) assemblies.Add(Assembly.LoadFrom(alt[^1]));
}

var all = assemblies.SelectMany(a =>
{
    try { return a.GetTypes(); }
    catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null)!; }
}).ToArray();

foreach (var query in args)
{
    var t = Array.Find(all, x => x!.Name == query)
            ?? Array.Find(all, x => x!.FullName != null && x.FullName.Contains(query));
    if (t == null) { Console.WriteLine($"NOT FOUND: {query}\n"); continue; }

    Console.WriteLine($"===== {t.FullName} (sealed={t.IsSealed}, enum={t.IsEnum}) =====");

    if (t.IsEnum)
    {
        foreach (var n in Enum.GetNames(t)) Console.WriteLine($"  {n}");
        Console.WriteLine();
        continue;
    }

    foreach (var c in t.GetConstructors())
    {
        var ps = string.Join(", ", Array.ConvertAll(c.GetParameters(), x => $"{Pretty(x.ParameterType)} {x.Name}"));
        Console.WriteLine($"  ctor ({ps})");
    }

    foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
        Console.WriteLine($"  field {Pretty(f.FieldType)} {f.Name}");

    foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        Console.WriteLine($"  prop {Pretty(p.PropertyType)} {p.Name}");

    foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
    {
        
        var ps = string.Join(", ", Array.ConvertAll(m.GetParameters(), x => $"{Pretty(x.ParameterType)} {x.Name}"));
        var mods = (m.IsVirtual ? "virtual " : "");
        Console.WriteLine($"  meth {mods}{Pretty(m.ReturnType)} {m.Name}({ps})");
    }
    Console.WriteLine();
}

static string Pretty(Type t)
{
    if (t.IsGenericType)
    {
        var name = t.Name.Split('`')[0];
        var args = string.Join(", ", Array.ConvertAll(t.GetGenericArguments(), Pretty));
        return $"{name}<{args}>";
    }
    return t.Name;
}
