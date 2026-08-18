// Publicize.cs
// Reads an assembly and rewrites all non-public types and members to public,
// mirroring Krafs.Publicizer with IncludeVirtualMembers=false and
// IncludeCompilerGeneratedMembers=false.
//
// Usage: Publicize <input.dll> <output.dll>

using Mono.Cecil;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: Publicize <input.dll> <output.dll>");
    return 1;
}

string inputPath = args[0];
string outputPath = args[1];

const string CompilerGeneratedAttribute = "System.Runtime.CompilerServices.CompilerGeneratedAttribute";

using var assembly = AssemblyDefinition.ReadAssembly(inputPath);

foreach (var type in assembly.MainModule.GetTypes())
{
    PublicizeType(type);
}

assembly.Write(outputPath);
Console.WriteLine($"Publicized {inputPath} -> {outputPath}");
return 0;

static bool IsCompilerGenerated(ICustomAttributeProvider member) =>
    member.CustomAttributes.Any(a => a.AttributeType.FullName == CompilerGeneratedAttribute);

static void PublicizeType(TypeDefinition type)
{
    if (IsCompilerGenerated(type))
        return;

    if (!type.IsPublic && !type.IsNestedPublic)
    {
        if (type.IsNested)
            type.IsNestedPublic = true;
        else
            type.IsPublic = true;
    }

    foreach (var field in type.Fields)
    {
        if (!field.IsPublic && !IsCompilerGenerated(field))
            field.IsPublic = true;
    }

    foreach (var method in type.Methods)
    {
        if (method.IsPublic || IsCompilerGenerated(method))
            continue;
        // Skip virtual/abstract members (IncludeVirtualMembers=false)
        if (method.IsVirtual || method.IsAbstract)
            continue;
        method.IsPublic = true;
    }

    foreach (var prop in type.Properties)
    {
        if (IsCompilerGenerated(prop))
            continue;
        if (prop.GetMethod is { IsPublic: false } getter && !getter.IsVirtual && !getter.IsAbstract && !IsCompilerGenerated(getter))
            getter.IsPublic = true;
        if (prop.SetMethod is { IsPublic: false } setter && !setter.IsVirtual && !setter.IsAbstract && !IsCompilerGenerated(setter))
            setter.IsPublic = true;
    }

    foreach (var ev in type.Events)
    {
        if (IsCompilerGenerated(ev))
            continue;
        if (ev.AddMethod is { IsPublic: false } add && !add.IsVirtual && !add.IsAbstract && !IsCompilerGenerated(add))
            add.IsPublic = true;
        if (ev.RemoveMethod is { IsPublic: false } remove && !remove.IsVirtual && !remove.IsAbstract && !IsCompilerGenerated(remove))
            remove.IsPublic = true;
    }
}
