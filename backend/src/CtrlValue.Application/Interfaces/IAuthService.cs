using CtrlValue.Application.DTOs;
using CtrlValue.Domain.Entities;

namespace CtrlValue.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, string? inviteToken = null);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request);
    Task<UpdateProfileResponse> UpdateProfileAsync(Guid userId, UpdateProfileRequest request);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
    Task ForgotPasswordAsync(ForgotPasswordRequest request);
    Task ResetPasswordAsync(ResetPasswordRequest request);
    // Email verification
    Task VerifyEmailAsync(string token);
    Task ResendVerificationEmailAsync(string email, User? unverifiedUser = null);

    // Onboarding
    Task CompleteOnboardingAsync(Guid userId);

    // Session
    Task LogoutAsync(Guid userId, string tenantId);

    // Account deletion
    Task RequestAccountDeletionAsync(Guid userId);
    Task RequestExpeditedDeletionAsync(Guid userId);
    Task CancelDeletionRequestAsync(Guid userId);
}
