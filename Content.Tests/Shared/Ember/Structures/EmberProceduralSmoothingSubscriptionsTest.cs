using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Content.Tests.Shared.Ember.Structures;

[TestFixture]
public sealed class EmberProceduralSmoothingSubscriptionsTest
{
    [TestCase("EmberProceduralStructureComponent", "ComponentStartup")]
    [TestCase("EmberProceduralStructureComponent", "ComponentShutdown")]
    [TestCase("EmberProceduralStructureComponent", "AnchorStateChangedEvent")]
    [TestCase("DoorComponent", "ComponentStartup")]
    [TestCase("DoorComponent", "ComponentShutdown")]
    [TestCase("DoorComponent", "AnchorStateChangedEvent")]
    public void EmberSmoothingSystemsDoNotRegisterDuplicateDirectedSubscriptions(string component, string eventName)
    {
        var root = FindRepoRoot();
        var emberClient = Path.Combine(root, "Content.Client", "Ember");
        var pattern = new Regex(
            $@"SubscribeLocalEvent\s*<\s*{Regex.Escape(component)}\s*,\s*{Regex.Escape(eventName)}\s*>",
            RegexOptions.Compiled);
        var count = 0;

        foreach (var path in Directory.EnumerateFiles(emberClient, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(path);
            count += pattern.Matches(source).Count;
        }

        Assert.That(count, Is.LessThanOrEqualTo(1),
            $"Robust directed subscriptions allow only one handler for {component}/{eventName}.");
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);

        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "Content.Client")) &&
                Directory.Exists(Path.Combine(current.FullName, "Content.Shared")))
                return current.FullName;

            current = current.Parent;
        }

        Assert.Fail("Could not find repository root.");
        return string.Empty;
    }
}
