using System.ComponentModel.DataAnnotations;
using PawConnect.Data;

namespace PawConnect.Entities;

public class UserSavedView
{
    public int Id { get; set; }

    [StringLength(450)]
    public string? UserId { get; set; }

    public ApplicationUser? User { get; set; }

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string PageKey { get; set; } = string.Empty;

    public SavedViewRoleScope RoleScope { get; set; } = SavedViewRoleScope.Global;

    [StringLength(300)]
    public string? Description { get; set; }

    [Required]
    public string FilterStateJson { get; set; } = "{}";

    public string? SortStateJson { get; set; }

    public string? ColumnStateJson { get; set; }

    [StringLength(80)]
    public string? ViewMode { get; set; }

    public string? FilterSummaryJson { get; set; }

    public bool IsPinned { get; set; }

    public bool IsDefault { get; set; }

    public bool IsSystemView { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastUsedAtUtc { get; set; }
}

public enum SavedViewRoleScope
{
    Global = 0,
    Admin = 1,
    Shelter = 2,
    Adopter = 3,
    Volunteer = 4,
    Foster = 5
}
