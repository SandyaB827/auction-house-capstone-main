using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using TheAuctionHouse.Common.ErrorHandling;
using TheAuctionHouse.Common.Validation;
using TheAuctionHouse.Domain.DataContracts;
using TheAuctionHouse.Domain.Entities;
using TheAuctionHouse.Domain.ServiceContracts;

namespace TheAuctionHouse.Domain.Services;

public class PortalUserService : IPortalUserService
{
    private readonly IAppUnitOfWork _appUnitOfWork;
    private readonly IEmailService _emailService;
    
    public PortalUserService(IAppUnitOfWork appUnitOfWork, IEmailService emailService)
    {
        _appUnitOfWork = appUnitOfWork;
        _emailService = emailService;
    }
    
    public async Task<Result<bool>> ForgotPasswordAsync(ForgotPasswordRequest forgotPasswordRequest)
    {
        try
        {
            //Check if the email id is not null or empty.
            //validate if the email address is in the expected format.
            Error validationError = Error.ValidationFailures();
            if (!ValidationHelper.Validate(forgotPasswordRequest, validationError))
            {
                return validationError;
            }
            //Check if the email id exists.
            PortalUser? portalUser = await _appUnitOfWork.PortalUserRepository.GetUserByEmailAsync(forgotPasswordRequest.EmailId);

            if (portalUser == null)
                return Error.NotFound("User Not Found.");

            //Send a reset password link to the email id.
            string resetLink = $"https://TheAuctionHouse/ResetPassword?userId={portalUser.Id}&token={GeneratePasswordResetToken()}";
            await _emailService.SendEmailAsync(
                portalUser.EmailId, 
                "Password Reset | The Auction House", 
                $"Dear {portalUser.Name},<br><br>Please <a href='{resetLink}'>click here</a> to reset your password.<br><br>Regards,<br>Admin Team", 
                true);
            
            return true;
        }
        catch (Exception ex)
        {
            return Error.InternalServerError(ex.Message);
        }
    }

    public async Task<Result<bool>> LoginAsync(LoginRequest loginRequest)
    {
        try
        {
            // Validate login request
            Error validationError = Error.ValidationFailures();
            if (!ValidationHelper.Validate(loginRequest, validationError))
            {
                return validationError;
            }

            // Get user by email
            PortalUser? portalUser = await _appUnitOfWork.PortalUserRepository.GetUserByEmailAsync(loginRequest.Email);
            if (portalUser == null)
                return Error.NotFound("Invalid email or password.");

            // Verify password
            string hashedPassword = HashPassword(loginRequest.Password);
            if (!portalUser.HashedPassword.Equals(hashedPassword))
                return Error.BadRequest("Invalid email or password.");

            // Login successful
            // In a real application, you would create a session or token here
            return true;
        }
        catch (Exception ex)
        {
            return Error.InternalServerError(ex.Message);
        }
    }

    public async Task<Result<bool>> LogoutAsync(int userId)
    {
        try
        {
            // Get user by ID to verify they exist
            var user = await _appUnitOfWork.PortalUserRepository.GetByIdAsync(userId);
            if (user == null)
                return Error.NotFound("User not found.");
            
            // In a real application, you would invalidate the session or token here
            
            return true;
        }
        catch (Exception ex)
        {
            return Error.InternalServerError(ex.Message);
        }
    }

    public async Task<Result<bool>> ResetPasswordAsync(ResetPasswordRequest resetPasswordRequest)
    {
        try
        {
            // Validate reset password request
            Error validationError = Error.ValidationFailures();
            if (!ValidationHelper.Validate(resetPasswordRequest, validationError))
            {
                return validationError;
            }

            // In a real application, validate the token here
            
            // Get user
            var user = await _appUnitOfWork.PortalUserRepository.GetByIdAsync(resetPasswordRequest.UserId);
            if (user == null)
                return Error.NotFound("User not found.");

            // Update password
            user.HashedPassword = HashPassword(resetPasswordRequest.NewPassword);
            await _appUnitOfWork.PortalUserRepository.UpdateAsync(user);
            await _appUnitOfWork.CommitAsync();

            return true;
        }
        catch (Exception ex)
        {
            return Error.InternalServerError(ex.Message);
        }
    }

    public async Task<Result<bool>> SignUpAsync(SignUpRequest signUpRequest)
    {
        try
        {
            // Validate signup request
            Error validationError = Error.ValidationFailures();
            if (!ValidationHelper.Validate(signUpRequest, validationError))
            {
                return validationError;
            }

            // Check if email already exists
            var existingUser = await _appUnitOfWork.PortalUserRepository.GetUserByEmailAsync(signUpRequest.Email);
            if (existingUser != null)
                return Error.BadRequest("Email already registered.");

            // Create new user
            var newUser = new PortalUser
            {
                Name = signUpRequest.Name,
                EmailId = signUpRequest.Email,
                HashedPassword = HashPassword(signUpRequest.Password),
                WalletBalence = 0,
                WalletBalenceBlocked = 0
            };

            await _appUnitOfWork.PortalUserRepository.AddAsync(newUser);
            await _appUnitOfWork.CommitAsync();

            return true;
        }
        catch (Exception ex)
        {
            return Error.InternalServerError(ex.Message);
        }
    }
    
    private string GeneratePasswordResetToken()
    {
        // In a real application, this would generate a secure random token
        // For simplicity, we're just creating a random string
        byte[] tokenData = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(tokenData);
        }
        return Convert.ToBase64String(tokenData);
    }
      private string HashPassword(string password)
    {
        // In a real application, use a secure password hashing algorithm
        // For simplicity, we're just using SHA256
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }
    }
}
}
