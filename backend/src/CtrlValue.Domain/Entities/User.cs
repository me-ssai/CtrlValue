using CtrlValue.Domain.Enums;

namespace CtrlValue.Domain.Entities;

public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsEmailConfirmed { get; set; } = false;
    public UserRole Role { get; set; } = UserRole.User;
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryTime { get; set; }

    // Email verification
    public string? EmailVerificationToken { get; set; }
    public DateTime? EmailVerificationExpiry { get; set; }

    // Password Reset
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetExpiry { get; set; }
    // Account lockout
    public int FailedLoginAttempts { get; set; } = 0;
    public DateTime? LockoutUntil { get; set; }

    // Invite flow
    public string? InviteToken { get; set; }
    public DateTime? InviteTokenExpiry { get; set; }
    public Guid? InvitedEntityId { get; set; }

    // Onboarding
    public DateTime? OnboardingCompletedAt { get; set; }

    // Delegation
    /// <summary>
    /// When true, this user can approve expedited account-deletion requests.
    /// Only a SuperAdmin can set this flag.
    /// </summary>
    public bool CanApproveDeletions { get; set; } = false;

    // Navigation properties
    public ICollection<EntityUser> EntityUsers { get; set; } = new List<EntityUser>();
}
