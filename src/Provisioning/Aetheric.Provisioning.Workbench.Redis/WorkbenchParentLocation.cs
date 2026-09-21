using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aetheric.Provisioning.Workbench.Redis;

/// <summary>Versioned JSON stored in ParentIdentity.ResourceLocations["IWorkbench"].
/// Identifies an existing workspace registration, never a request to create or adopt one.</summary>
public sealed record WorkbenchParentLocation(int Version, string Environment, string Institution, string Resource, string Stage)
{
    public static bool TryParse(string json, out WorkbenchParentLocation? location)
    {
        location = null;
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            var value = JsonSerializer.Deserialize<WorkbenchParentLocation>(json);
            if (value is null || value.Version != 1 || string.IsNullOrWhiteSpace(value.Environment)
                || string.IsNullOrWhiteSpace(value.Institution) || string.IsNullOrWhiteSpace(value.Resource)
                || value.Stage is null || !Regex.IsMatch(value.Stage, @"\A[a-z0-9][a-z0-9-]{0,62}\z")) return false;
            location = value;
            return true;
        }
        catch (JsonException) { return false; }
    }
}
