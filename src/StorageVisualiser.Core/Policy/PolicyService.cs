using System.Text.Json;

namespace StorageVisualiser.Core.Policy;

public sealed class PolicyService
{
    public const string PolicyFileName = "policy.json";

    public OrgPolicy ActivePolicy { get; private set; } = new();
    public bool IsPolicyLoaded { get; private set; }
    public string? LoadedPolicySource { get; private set; }
    public string? PolicyError { get; private set; }

    public PolicyService()
    {
        LoadPolicy();
    }

    public void LoadPolicy()
    {
        // 1. Check ProgramData first (admin priority): %ProgramData%\StorageVisualiser\policy.json
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var programDataPolicy = Path.Combine(programData, "StorageVisualiser", PolicyFileName);

        // 2. Check application directory next: <AppBase>\policy.json
        var appBasePolicy = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PolicyFileName);

        string? targetFile = null;
        if (File.Exists(programDataPolicy))
        {
            targetFile = programDataPolicy;
        }
        else if (File.Exists(appBasePolicy))
        {
            targetFile = appBasePolicy;
        }

        if (targetFile == null)
        {
            ActivePolicy = new OrgPolicy();
            IsPolicyLoaded = false;
            LoadedPolicySource = null;
            PolicyError = null;
            return;
        }

        try
        {
            var json = File.ReadAllText(targetFile);
            var policy = JsonSerializer.Deserialize(json, PolicyJsonContext.Default.OrgPolicy);
            if (policy != null)
            {
                ActivePolicy = policy;
                IsPolicyLoaded = true;
                LoadedPolicySource = targetFile;
                PolicyError = null;
            }
            else
            {
                ApplyRestrictiveFailsafe(targetFile, "Policy file contained null data.");
            }
        }
        catch (Exception ex)
        {
            // Requirement §7.2: If the policy file is malformed, fail safe (most restrictive settings apply)
            ApplyRestrictiveFailsafe(targetFile, $"Malformed policy file: {ex.Message}");
        }
    }

    private void ApplyRestrictiveFailsafe(string source, string error)
    {
        ActivePolicy = new OrgPolicy
        {
            DeleteMode = PolicyDeleteMode.Disabled,
            AllowSnapshots = false,
            AllowExports = false,
            AllowElevation = false,
            AllowNetworkPaths = false,
            RedactPathsInExports = true
        };
        IsPolicyLoaded = true;
        LoadedPolicySource = source;
        PolicyError = error;
    }
}
