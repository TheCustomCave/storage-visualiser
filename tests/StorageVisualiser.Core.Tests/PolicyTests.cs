using System.IO;
using Shouldly;
using StorageVisualiser.Core.Actions;
using StorageVisualiser.Core.Model;
using StorageVisualiser.Core.Policy;
using Xunit;

namespace StorageVisualiser.Core.Tests;

public class PolicyTests
{
    private sealed class DummyRecycleBinProvider : IRecycleBinProvider
    {
        public bool SendToRecycleBin(string path, out string? error)
        {
            error = null;
            return true;
        }
    }

    [Fact]
    public void CanDelete_WhenDeleteModeDisabled_ReturnsFalse()
    {
        var policy = new FileActionPolicy { Mode = DeleteMode.Disabled };
        var service = new FileActionService(new DummyRecycleBinProvider(), policy);

        var node = new StorageNode
        {
            Name = "sample.txt",
            Kind = StorageItemKind.File,
            Size = 100,
            Parent = new StorageNode { Name = @"C:\Users\John\Desktop", Kind = StorageItemKind.Directory }
        };

        var allowed = service.CanDelete(node, out var reason);

        allowed.ShouldBeFalse();
        reason.ShouldContain("disabled by policy");
    }

    [Fact]
    public void CanDelete_WhenPathInAdditionalProtectedPaths_ReturnsFalse()
    {
        var policy = new FileActionPolicy
        {
            Mode = DeleteMode.RecycleBinOnly,
            AdditionalProtectedPaths = [@"D:\CompanyConfidential"]
        };
        var service = new FileActionService(new DummyRecycleBinProvider(), policy);

        var parent = new StorageNode { Name = @"D:\CompanyConfidential", Kind = StorageItemKind.Directory };
        var node = new StorageNode
        {
            Name = "secret.docx",
            Kind = StorageItemKind.File,
            Size = 200,
            Parent = parent
        };

        var allowed = service.CanDelete(node, out var reason);

        allowed.ShouldBeFalse();
        reason.ShouldContain("protected by organisation policy");
    }

    [Fact]
    public void PolicyService_LoadsValidJsonCorrectly()
    {
        var tempPolicyFile = Path.Combine(Path.GetTempPath(), $"policy_test_{Guid.NewGuid():N}.json");
        try
        {
            var json = """
            {
              "deleteMode": "disabled",
              "allowExports": false,
              "redactPathsInExports": true,
              "protectedPaths": ["C:\\CustomProtected"]
            }
            """;
            File.WriteAllText(tempPolicyFile, json);

            var policy = System.Text.Json.JsonSerializer.Deserialize(json, PolicyJsonContext.Default.OrgPolicy);
            policy.ShouldNotBeNull();
            policy.DeleteMode.ShouldBe(PolicyDeleteMode.Disabled);
            policy.AllowExports.ShouldBe(false);
            policy.RedactPathsInExports.ShouldBe(true);
            policy.ProtectedPaths.ShouldNotBeNull();
            policy.ProtectedPaths.ShouldContain(@"C:\CustomProtected");
            policy.IsEnforced.ShouldBeTrue();
        }
        finally
        {
            if (File.Exists(tempPolicyFile))
            {
                File.Delete(tempPolicyFile);
            }
        }
    }
}
