using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NetArchTest.Rules;
using Shirobot.Plugin.KotoSub.Infrastructure;
using Xunit;

namespace Shirobot.Plugin.KotoSub.Tests;

/// <summary>
/// Keeps the module layout honest: small files, small classes, no god-class names, and no module reaching
/// into another module's non-Contracts types. Break a limit only with [ArchitectureException("reason")].
/// </summary>
public sealed class ArchitectureTests
{
    private const int MaxFileLines = 300;
    private const int MaxConstructorDependencies = 5;
    private const int MaxPublicMethods = 10;
    private const int MaxExceptions = 1;

    private const string Root = "Shirobot.Plugin.KotoSub";
    private static readonly Assembly PluginAssembly = typeof(KotoSubPlugin).Assembly;

    private static readonly string[] ForbiddenNames =
        ["Manager", "Helper", "Helpers", "Util", "Utils", "Utility", "Common", "Misc", "Base"];

    private static readonly string[] Modules =
        ["Cards", "Commands", "Delivery", "Polling", "Sources", "Subscriptions"];

    [Fact]
    public void SourceFilesStayUnderTheLineLimit()
    {
        var tooLong = SourceFiles()
            .Select(path => (Path: path, Lines: File.ReadLines(path).Count()))
            .Where(file => file.Lines > MaxFileLines)
            .Select(file => $"{Path.GetRelativePath(ProjectDirectory(), file.Path)} ({file.Lines} 行)")
            .ToList();

        Assert.True(tooLong.Count == 0, "超过 " + MaxFileLines + " 行: " + string.Join(", ", tooLong));
    }

    [Fact]
    public void ClassesStayWithinDependencyAndSizeLimits()
    {
        var problems = new List<string>();
        foreach (var type in OwnTypes().Where(IsPlainClass).Where(type => !HasException(type)))
        {
            var constructor = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();
            if (constructor is not null && constructor.GetParameters().Length > MaxConstructorDependencies)
                problems.Add($"{type.Name}: 构造函数 {constructor.GetParameters().Length} 个依赖");

            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Count(method => !method.IsSpecialName && method.GetCustomAttribute<CompilerGeneratedAttribute>() is null);
            if (methods > MaxPublicMethods)
                problems.Add($"{type.Name}: {methods} 个 public 方法");
        }

        Assert.True(problems.Count == 0, string.Join("; ", problems));
    }

    [Fact]
    public void TypeNamesAvoidGodClassWords()
    {
        var offenders = OwnTypes()
            .Select(type => type.Name)
            .Where(name => ForbiddenNames.Any(word => ContainsWord(name, word)))
            .ToList();

        Assert.True(offenders.Count == 0, "含有禁用词: " + string.Join(", ", offenders));
    }

    [Theory]
    [MemberData(nameof(ModulePairs))]
    public void ModulesOnlyUseEachOthersContracts(string module, string other)
    {
        var privateTypes = PluginAssembly.GetTypes()
            .Where(type => type.Namespace is not null && !type.Name.StartsWith('<') &&
                           (type.Namespace == $"{Root}.Features.{other}" ||
                            type.Namespace.StartsWith($"{Root}.Features.{other}.", StringComparison.Ordinal)) &&
                           type.Namespace != $"{Root}.Features.{other}.Contracts")
            .Select(type => type.FullName!)
            .ToArray();

        var result = Types.InAssembly(PluginAssembly)
            .That().ResideInNamespace($"{Root}.Features.{module}")
            .ShouldNot().HaveDependencyOnAny(privateTypes)
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"{module} 使用了 {other} 的非 Contracts 类型: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void InfrastructureKnowsNothingAboutFeatures()
    {
        var result = Types.InAssembly(PluginAssembly)
            .That().ResideInNamespace($"{Root}.Infrastructure")
            .ShouldNot().HaveDependencyOn($"{Root}.Features")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void EveryChatCommandIsASealedUseCaseInsideAFeature()
    {
        var commands = OwnTypes().Where(type => typeof(Features.Commands.Contracts.ISubCommand).IsAssignableFrom(type) && type.IsClass).ToList();

        Assert.NotEmpty(commands);
        Assert.All(commands, type =>
        {
            Assert.True(type.IsSealed, $"{type.Name} 应为 sealed");
            Assert.StartsWith($"{Root}.Features.", type.Namespace);
        });
    }

    [Fact]
    public void ArchitectureExceptionsOnlyShrink()
    {
        var exceptions = OwnTypes().Where(HasException)
            .Select(type => $"{type.Name}: {type.GetCustomAttribute<ArchitectureExceptionAttribute>()!.Reason}")
            .ToList();

        Assert.True(exceptions.Count <= MaxExceptions, "例外只能减少: " + string.Join("; ", exceptions));
    }

    // ---- Naming conventions -------------------------------------------------------------------------
    // Namespace      = Shirobot.Plugin.KotoSub + the folder path.
    // File           = named after one type it declares; at most one class per file (records, enums and
    //                  interfaces that belong to it may share the file).
    // Use case       = a sealed ISubCommand named "Verb + Noun". Every chat command lives under Features/Commands,
    //                  grouped by purpose (Subscriptions, Items, Admin); the other modules hold the services they use.
    // Module layout  = Features/<Module>/ holds the use cases and the module's private parts side by side;
    //                  Features/<Module>/Contracts/ holds the only types other modules may depend on.
    // Interface      = starts with "I". Tests: "<Subject>Tests", one test class per file named like the file.

    private static readonly string[] UseCaseVerbs =
        ["Add", "Remove", "Rename", "List", "Show", "Test", "Manage", "Set", "Reload"];

    private static readonly Regex NamespaceLine = new(@"^namespace ([\w.]+);", RegexOptions.Multiline);
    private static readonly Regex TypeDeclaration = new(
        @"^(?:public |internal )?(?:static |sealed |abstract |partial |readonly )*(class|record struct|record|interface|enum|struct) (\w+)",
        RegexOptions.Multiline);

    [Fact]
    public void NamespacesMatchFolders()
    {
        var mismatches = new List<string>();
        foreach (var path in SourceFiles().Where(path => !path.EndsWith("GlobalUsings.cs")))
        {
            var relative = Path.GetRelativePath(ProjectDirectory(), path);
            var folder = Path.GetDirectoryName(relative)!.Replace(Path.DirectorySeparatorChar, '.');
            var expected = folder.Length == 0 ? Root : $"{Root}.{folder}";
            var match = NamespaceLine.Match(File.ReadAllText(path));
            if (!match.Success || match.Groups[1].Value != expected)
                mismatches.Add($"{relative}: {match.Groups[1].Value} ≠ {expected}");
        }

        Assert.True(mismatches.Count == 0, string.Join("; ", mismatches));
    }

    [Fact]
    public void FilesAreNamedAfterTheirTypeAndHoldOneClass()
    {
        var problems = new List<string>();
        foreach (var path in SourceFiles().Where(path => !path.EndsWith("GlobalUsings.cs")))
        {
            var stem = Path.GetFileName(path).Replace(".axaml.cs", "").Replace(".cs", "");
            var declared = TypeDeclaration.Matches(File.ReadAllText(path)).ToList();
            if (declared.All(match => match.Groups[2].Value != stem))
                problems.Add($"{Path.GetFileName(path)}: 没有同名类型");
            if (declared.Count(match => match.Groups[1].Value == "class") > 1)
                problems.Add($"{Path.GetFileName(path)}: 含多个类");
        }

        Assert.True(problems.Count == 0, string.Join("; ", problems));
    }

    [Fact]
    public void ChatCommandsAreNamedVerbNounAndLiveUnderCommands()
    {
        var useCases = OwnTypes()
            .Where(type => type.IsClass && typeof(Features.Commands.Contracts.ISubCommand).IsAssignableFrom(type)).ToList();

        Assert.All(useCases, type =>
        {
            Assert.Contains(UseCaseVerbs, verb => type.Name.StartsWith(verb, StringComparison.Ordinal) &&
                                                  type.Name.Length > verb.Length && char.IsUpper(type.Name[verb.Length]));
            Assert.Matches($@"^{Regex.Escape(Root)}\.Features\.Commands(\.(Subscriptions|Items|Admin))?$", type.Namespace!);
        });
    }

    [Fact]
    public void InterfacesStartWithI()
    {
        var offenders = OwnTypes().Where(type => type.IsInterface)
            .Where(type => type.Name.Length < 2 || type.Name[0] != 'I' || !char.IsUpper(type.Name[1]))
            .Select(type => type.Name).ToList();

        Assert.True(offenders.Count == 0, string.Join(", ", offenders));
    }

    [Fact]
    public void TestClassesAreNamedLikeTheirFiles()
    {
        var testDirectory = Path.GetFullPath(Path.Combine(ProjectDirectory(), "..", "tests", "Shirobot.Plugin.KotoSub.Tests"));
        var problems = new List<string>();
        foreach (var path in Directory.EnumerateFiles(testDirectory, "*Tests.cs"))
        {
            var stem = Path.GetFileNameWithoutExtension(path);
            var classes = TypeDeclaration.Matches(File.ReadAllText(path)).Where(match => match.Groups[1].Value == "class")
                .Select(match => match.Groups[2].Value).ToList();
            if (!classes.Contains(stem)) problems.Add($"{stem}.cs 缺少同名测试类");
        }

        Assert.True(problems.Count == 0, string.Join("; ", problems));
    }

    public static IEnumerable<object[]> ModulePairs() =>
        from module in Modules
        from other in Modules
        where module != other
        select new object[] { module, other };

    // "BaseHandler" and "ClientBase" are god-class names; "BaselineStep" merely starts with the same letters.
    private static bool ContainsWord(string name, string word)
    {
        var index = name.IndexOf(word, StringComparison.Ordinal);
        while (index >= 0)
        {
            var end = index + word.Length;
            var startsWord = index == 0 || char.IsLower(name[index - 1]);
            var endsWord = end == name.Length || char.IsUpper(name[end]);
            if (startsWord && endsWord) return true;
            index = name.IndexOf(word, index + 1, StringComparison.Ordinal);
        }

        return false;
    }

    private static IEnumerable<Type> OwnTypes() =>
        PluginAssembly.GetTypes().Where(type =>
            type.Namespace is not null && type.Namespace.StartsWith(Root, StringComparison.Ordinal) &&
            type.GetCustomAttribute<CompilerGeneratedAttribute>() is null && !type.Name.StartsWith('<'));

    private static bool IsPlainClass(Type type) =>
        type.IsClass && !type.IsAbstract && !type.IsNested && !typeof(Delegate).IsAssignableFrom(type) &&
        !type.GetMethods().Any(method => method.Name == "<Clone>$") &&
        type.GetCustomAttribute<System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute>() is null &&
        !typeof(Avalonia.Controls.Control).IsAssignableFrom(type);

    private static bool HasException(Type type) => type.GetCustomAttribute<ArchitectureExceptionAttribute>() is not null;

    private static IEnumerable<string> SourceFiles() =>
        Directory.EnumerateFiles(ProjectDirectory(), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                           !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    private static string ProjectDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Shirobot.Plugin.KotoSub.sln")))
            directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("找不到解决方案目录。"), Root);
    }
}
