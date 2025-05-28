using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TheAuctionHouse.Domain.Entities;
using TheAuctionHouse.Models;
using TheAuctionHouse.Services;

namespace TheAuctionHouse.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<PortalUser> _userManager;
    private readonly SignInManager<PortalUser> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IJwtService _jwtService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<PortalUser> userManager,
        SignInManager<PortalUser> signInManager,
        RoleManager<IdentityRole> roleManager,
        IJwtService jwtService,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _jwtService = jwtService;
        _logger = logger;
    }

    /// <summary>
    /// Register a new user
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        try
        {
            // Check if user already exists
            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser != null)
            {
                return BadRequest(new AuthResponse
                {
                    Success = false,
                    Message = "User with this email already exists."
                });
            }

            // Create new user
            var user = new PortalUser
            {
                UserName = request.Email,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                WalletBalance = 0,
                BlockedAmount = 0
            };

            var result = await _userManager.CreateAsync(user, request.Password);

            if (result.Succeeded)
            {
                // Assign default roles for auction functionality
                await _userManager.AddToRoleAsync(user, UserRoles.User);
                await _userManager.AddToRoleAsync(user, UserRoles.Seller);
                await _userManager.AddToRoleAsync(user, UserRoles.Bidder);
                
                // Generate JWT token
                var token = await _jwtService.GenerateTokenAsync(user);
                var roles = await _userManager.GetRolesAsync(user);

                _logger.LogInformation("User {Email} created successfully", request.Email);
                
                return Ok(new AuthResponse
                {
                    Success = true,
                    Message = "User registered successfully.",
                    UserId = user.Id,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Token = token,
                    TokenExpiration = DateTime.UtcNow.AddMinutes(60),
                    Roles = roles.ToList()
                });
            }

            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = string.Join(", ", result.Errors.Select(e => e.Description))
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during user registration");
            return StatusCode(500, new AuthResponse
            {
                Success = false,
                Message = "An error occurred during registration."
            });
        }
    }

    /// <summary>
    /// Login user and get JWT token
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        try
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                return BadRequest(new AuthResponse
                {
                    Success = false,
                    Message = "Invalid email or password."
                });
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);

            if (result.Succeeded)
            {
                // Ensure existing users have required roles for auction functionality
                var roles = await _userManager.GetRolesAsync(user);
                
                // Check and assign missing roles
                if (!roles.Contains(UserRoles.Seller))
                {
                    await _userManager.AddToRoleAsync(user, UserRoles.Seller);
                }
                
                if (!roles.Contains(UserRoles.Bidder))
                {
                    await _userManager.AddToRoleAsync(user, UserRoles.Bidder);
                }
                
                // Refresh roles after potential additions
                roles = await _userManager.GetRolesAsync(user);
                
                // Generate JWT token
                var token = await _jwtService.GenerateTokenAsync(user);

                _logger.LogInformation("User {Email} logged in successfully", request.Email);
                
                return Ok(new AuthResponse
                {
                    Success = true,
                    Message = "Login successful.",
                    UserId = user.Id,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Token = token,
                    TokenExpiration = DateTime.UtcNow.AddMinutes(60),
                    Roles = roles.ToList()
                });
            }

            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = "Invalid email or password."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during user login");
            return StatusCode(500, new AuthResponse
            {
                Success = false,
                Message = "An error occurred during login."
            });
        }
    }

    /// <summary>
    /// Get current user info (requires authentication)
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<AuthResponse>> GetCurrentUser()
    {
        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new AuthResponse
                {
                    Success = false,
                    Message = "User not authenticated."
                });
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new AuthResponse
                {
                    Success = false,
                    Message = "User not found."
                });
            }

            var roles = await _userManager.GetRolesAsync(user);

            return Ok(new AuthResponse
            {
                Success = true,
                Message = "User found.",
                UserId = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Roles = roles.ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting current user");
            return StatusCode(500, new AuthResponse
            {
                Success = false,
                Message = "An error occurred while retrieving user information."
            });
        }
    }

    /// <summary>
    /// Assign role to user (Admin only)
    /// </summary>
    [HttpPost("assign-role")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<AuthResponse>> AssignRole(AssignRoleRequest request)
    {
        try
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                return NotFound(new AuthResponse
                {
                    Success = false,
                    Message = "User not found."
                });
            }

            if (!await _roleManager.RoleExistsAsync(request.Role))
            {
                return BadRequest(new AuthResponse
                {
                    Success = false,
                    Message = "Role does not exist."
                });
            }

            var result = await _userManager.AddToRoleAsync(user, request.Role);
            if (result.Succeeded)
            {
                var roles = await _userManager.GetRolesAsync(user);
                
                return Ok(new AuthResponse
                {
                    Success = true,
                    Message = $"Role '{request.Role}' assigned successfully.",
                    UserId = user.Id,
                    Email = user.Email,
                    Roles = roles.ToList()
                });
            }

            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = string.Join(", ", result.Errors.Select(e => e.Description))
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while assigning role");
            return StatusCode(500, new AuthResponse
            {
                Success = false,
                Message = "An error occurred while assigning role."
            });
        }
    }

    /// <summary>
    /// Remove role from user (Admin only)
    /// </summary>
    [HttpPost("remove-role")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<AuthResponse>> RemoveRole(AssignRoleRequest request)
    {
        try
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                return NotFound(new AuthResponse
                {
                    Success = false,
                    Message = "User not found."
                });
            }

            var result = await _userManager.RemoveFromRoleAsync(user, request.Role);
            if (result.Succeeded)
            {
                var roles = await _userManager.GetRolesAsync(user);
                
                return Ok(new AuthResponse
                {
                    Success = true,
                    Message = $"Role '{request.Role}' removed successfully.",
                    UserId = user.Id,
                    Email = user.Email,
                    Roles = roles.ToList()
                });
            }

            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = string.Join(", ", result.Errors.Select(e => e.Description))
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while removing role");
            return StatusCode(500, new AuthResponse
            {
                Success = false,
                Message = "An error occurred while removing role."
            });
        }
    }

    /// <summary>
    /// Get all available roles (Admin only)
    /// </summary>
    [HttpGet("roles")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<List<string>>> GetRoles()
    {
        try
        {
            var roles = _roleManager.Roles.Select(r => r.Name).ToList();
            return Ok(roles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting roles");
            return StatusCode(500, "An error occurred while retrieving roles.");
        }
    }

    /// <summary>
    /// Get all users with their roles (Admin only)
    /// </summary>
    [HttpGet("users")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<List<object>>> GetUsers()
    {
        try
        {
            var users = _userManager.Users.ToList();
            var userList = new List<object>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userList.Add(new
                {
                    user.Id,
                    user.Email,
                    user.FirstName,
                    user.LastName,
                    user.WalletBalance,
                    user.BlockedAmount,
                    Roles = roles
                });
            }

            return Ok(userList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting users");
            return StatusCode(500, "An error occurred while retrieving users.");
        }
    }
} 