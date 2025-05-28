using TheAuctionHouse.Common.ErrorHandling;

namespace TheAuctionHouse.Domain.ServiceContracts;

public interface IPortalUserService
{
    Task<Result<bool>> SignUpAsync(SignUpRequest signUpRequest);
    Task<Result<bool>> LoginAsync(LoginRequest loginRequest);
    Task<Result<bool>> LogoutAsync(int UserId);
    Task<Result<bool>> ForgotPasswordAsync(ForgotPasswordRequest forgotPasswordRequest);
    Task<Result<bool>> ResetPasswordAsync(ResetPasswordRequest resetPasswordRequest);
}
