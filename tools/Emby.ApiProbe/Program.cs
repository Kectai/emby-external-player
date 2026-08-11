using System.Reflection;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Model.Services;

var assemblyRoots = new[]
{
    typeof(BasePlugin).Assembly,
    typeof(IServerApplicationPaths).Assembly,
    typeof(IService).Assembly,
};

var assemblies = assemblyRoots
    .SelectMany(LoadClosure)
    .DistinctBy(assembly => assembly.FullName)
    .OrderBy(assembly => assembly.GetName().Name, StringComparer.Ordinal)
    .ToArray();

var filters = args.Length == 0
    ? new[]
    {
        "BasePluginSimpleUI",
        "IServerEntryPoint",
        "IHttpResultFactory",
        "IRequiresRequest",
        "IRequest",
        "IAuthorizationContext",
        "AuthorizationInfo",
        "ILibraryManager",
        "IUserManager",
        "IUserDataManager",
        "IMediaSourceManager",
        "BaseItem",
        "MediaSourceInfo",
        "MediaStream",
        "HttpRequestOptions",
        "HttpResponseInfo",
    }
    : args;

foreach (var type in assemblies
             .SelectMany(GetLoadableTypes)
             .Where(type => filters.Any(filter => Matches(type, filter)))
             .OrderBy(type => type.FullName, StringComparer.Ordinal))
{
    Console.WriteLine($"TYPE {type.FullName} [{type.Assembly.GetName().Name}]");

    foreach (var constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
    {
        Console.WriteLine($"  CTOR {FormatMethod(constructor)}");
    }

    foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                 .OrderBy(property => property.Name, StringComparer.Ordinal))
    {
        Console.WriteLine($"  PROP {FormatType(property.PropertyType)} {property.Name}");
    }

    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                 .Where(method => !method.IsSpecialName)
                 .OrderBy(method => method.Name, StringComparer.Ordinal)
                 .ThenBy(method => method.GetParameters().Length))
    {
        Console.WriteLine($"  METHOD {FormatMethod(method)}");
    }

    Console.WriteLine();
}

static bool Matches(Type type, string filter)
{
    if (filter.StartsWith("=", StringComparison.Ordinal))
    {
        var exact = filter[1..];
        return string.Equals(type.Name, exact, StringComparison.Ordinal) ||
               string.Equals(type.FullName, exact, StringComparison.Ordinal);
    }

    return type.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);
}

static IEnumerable<Assembly> LoadClosure(Assembly root)
{
    var pending = new Queue<Assembly>();
    var loaded = new Dictionary<string, Assembly>(StringComparer.Ordinal);
    pending.Enqueue(root);

    while (pending.Count > 0)
    {
        var assembly = pending.Dequeue();
        var name = assembly.GetName().Name;
        if (name is null || loaded.ContainsKey(name))
        {
            continue;
        }

        loaded[name] = assembly;
        yield return assembly;

        foreach (var reference in assembly.GetReferencedAssemblies())
        {
            if (loaded.ContainsKey(reference.Name ?? string.Empty))
            {
                continue;
            }

            try
            {
                pending.Enqueue(Assembly.Load(reference));
            }
            catch
            {
                // The probe reports only assemblies available from the selected SDK package.
            }
        }
    }
}

static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
{
    try
    {
        return assembly.GetTypes();
    }
    catch (ReflectionTypeLoadException exception)
    {
        return exception.Types.OfType<Type>();
    }
}

static string FormatMethod(MethodBase method)
{
    var returnType = method is MethodInfo methodInfo ? FormatType(methodInfo.ReturnType) + " " : string.Empty;
    var parameters = string.Join(", ", method.GetParameters().Select(parameter =>
        $"{FormatType(parameter.ParameterType)} {parameter.Name}"));
    return $"{returnType}{method.Name}({parameters})";
}

static string FormatType(Type type)
{
    if (!type.IsGenericType)
    {
        return type.FullName ?? type.Name;
    }

    var name = type.GetGenericTypeDefinition().FullName ?? type.Name;
    name = name[..name.IndexOf('`')];
    return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(FormatType))}>";
}
