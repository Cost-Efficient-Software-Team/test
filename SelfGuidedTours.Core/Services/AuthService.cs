﻿using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SelfGuidedTours.Core.Contracts;
using SelfGuidedTours.Core.Models;
using SelfGuidedTours.Core.Models.Auth;
using SelfGuidedTours.Core.Models.ExternalLogin;
using SelfGuidedTours.Core.Services.TokenGenerators;
using SelfGuidedTours.Core.Services.TokenValidators;
using SelfGuidedTours.Infrastructure.Common;
using SelfGuidedTours.Infrastructure.Data.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
namespace SelfGuidedTours.Core.Services
{
    public class AuthService : IAuthService
    {
        private readonly IRepository repository;
        private readonly AccessTokenGenerator accessTokenGenerator;
        private readonly RefreshTokenGenerator refreshTokenGenerator;
        private readonly RefreshTokenValidator refreshTokenValidator;
        private readonly IRefreshTokenService refreshTokenService;
        private readonly UserManager<ApplicationUser> userManager;

        public AuthService(IRepository repository,
            AccessTokenGenerator accessTokenGenerator,
            RefreshTokenGenerator refreshTokenGenerator,
            RefreshTokenValidator refreshTokenValidator,
            IRefreshTokenService refreshTokenService,
            UserManager<ApplicationUser> userManager
        )
        {
            this.repository = repository;
            this.accessTokenGenerator = accessTokenGenerator;
            this.refreshTokenGenerator = refreshTokenGenerator;
            this.refreshTokenValidator = refreshTokenValidator;
            this.refreshTokenService = refreshTokenService;
            this.userManager = userManager;
        }

        public async Task<ApplicationUser?> GetByEmailAsync(string email)
        {
            return await repository.AllReadOnly<ApplicationUser>()
                .FirstOrDefaultAsync(au => au.Email == email);
        }

        private async Task<ApplicationUser?> GetByIdAsync(string userId)
        {
            return await repository.AllReadOnly<ApplicationUser>()
                .FirstOrDefaultAsync(au => au.Id == userId);
        }

        private long GetTokenExpirationTime(string token)
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtSecurityToken = handler.ReadJwtToken(token);

            var tokenExp = jwtSecurityToken.Claims.First(claim => claim.Type.Equals("exp")).Value;
            var ticks = long.Parse(tokenExp);

            return ticks;
        }

        private async Task<AuthenticateResponse> AuthenticateAsync(ApplicationUser user, string responesMessage)
        {
            var accessToken = accessTokenGenerator.GenerateToken(user);
            var refreshToken = refreshTokenGenerator.GenerateToken();

            RefreshToken refreshTokenDTO = new RefreshToken()
            {
                Token = refreshToken,
                UserId = user.Id
            };

            await refreshTokenService.CreateAsync(refreshTokenDTO);

            return new AuthenticateResponse()
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ResponseMessage = responesMessage,
                AccessTokenExpiration = GetTokenExpirationTime(accessToken)
            };
        }

        public async Task<AuthenticateResponse> RegisterAsync(RegisterInputModel model)
        {
            if (await GetByEmailAsync(model.Email) != null)
            {
                throw new ArgumentException("User already exists!");
            }

            if (model.Password != model.RepeatPassword)
            {
                throw new ArgumentException("Passwords do not match!");
            }

            var hasher = new PasswordHasher<ApplicationUser>();

            var user = new ApplicationUser
            {
                Email = model.Email,
                NormalizedEmail = model.Email.ToUpper(),
                Name = model.Name,
                PasswordHash = hasher.HashPassword(null!, model.Password) // Hash the password
            };
            //Assign user role
            var userRole = AssignUserRole(user.Id);

            await repository.AddAsync(user);
            await repository.AddAsync(userRole);
            await repository.SaveChangesAsync();

            return await AuthenticateAsync(user, "User registered successfully!");
        }

        public async Task<AuthenticateResponse> LoginAsync(LoginInputModel model)
        {
            var user = await GetByEmailAsync(model.Email);

            if (user == null)
            {
                throw new ArgumentException("Email or password is incorrect!");
            }

            var isPassCorrect = new PasswordHasher<ApplicationUser>().VerifyHashedPassword(user, user.PasswordHash!, model.Password);

            if (isPassCorrect != PasswordVerificationResult.Success)
            {
                throw new ArgumentException("Email or password is incorrect!");
            }

            return await AuthenticateAsync(user, "Successfully logged in!");
        }

        public async Task<AuthenticateResponse> GoogleSignInAsync(GoogleUserDto googleUser)
        {
            if (googleUser == null)
            {
                throw new ArgumentException("Invalid Google Id Token");
            }

            var user = await GetByEmailAsync(googleUser.Email);

            if (user == null)
            {
                user = new ApplicationUser
                {
                    Email = googleUser.Email,
                    UserName = googleUser.Email,
                    Name = googleUser.Name,
                };
                var userRole = AssignUserRole(user.Id);

                await repository.AddAsync(userRole);
                await repository.AddAsync(user);
                await repository.SaveChangesAsync();
            }

            return await AuthenticateAsync(user, "Successfully logged in!");
        }

        public async Task LogoutAsync(string userId)
        {
            await refreshTokenService.DeleteAllAsync(userId);
        }

        public async Task<AuthenticateResponse> RefreshAsync(RefreshRequestModel model)
        {
            var isValidRefreshToken = refreshTokenValidator.Validate(model.RefreshToken);

            if (!isValidRefreshToken)
            {
                throw new ArgumentException("Invalid refresh token!");
            }

            var refreshTokenDTO = await refreshTokenService.GetByTokenAsync(model.RefreshToken);

            if (refreshTokenDTO == null)
            {
                throw new ArgumentException("Refresh token was not found!");
            }

            await refreshTokenService.DeleteAsync(refreshTokenDTO.Id);

            var user = await GetByIdAsync(refreshTokenDTO.UserId);

            if (user == null)
            {
                throw new ArgumentException("User was not found!");
            }

            return await AuthenticateAsync(user, "Successfully got new tokens!");
        }

        private IdentityUserRole<string> AssignUserRole(string userId)
        {
            return new IdentityUserRole<string>
            {
                UserId = userId,
                RoleId = "4f8554d2-cfaa-44b5-90ce-e883c804ae90" //User Role Id
            };
        }

        public async Task<ApiResponse> ChangePasswordAsync(ChangePasswordModel model)
        {
            if (model.CurrentPassword == model.NewPassword) throw new ArgumentException("New password can't be the same as the current one!");

            var user = await GetByIdAsync(model.UserId);

            if (user is null) throw new UnauthorizedAccessException("User not found");
            //User created via external login doesent have an assigned password and shouldnt be able to acces this method
            if (user.PasswordHash is null) throw new UnauthorizedAccessException("User has no assigned password!");

            var hasher = new PasswordHasher<ApplicationUser>();

            var result = hasher.VerifyHashedPassword(user, user.PasswordHash, model.CurrentPassword);

            if (result != PasswordVerificationResult.Success) throw new UnauthorizedAccessException("Invalid password");
            //Update the user's password with the new one
            user.PasswordHash = hasher.HashPassword(user, model.NewPassword);

            await repository.UpdateAsync(user);
            await repository.SaveChangesAsync();
            //TODO: Fix response
            var response = new ApiResponse
            {
                StatusCode = HttpStatusCode.OK,
                Result = "Password changed successfully!"
            };
            return response;
        }

        public async Task<string> GeneratePasswordResetTokenAsync(ApplicationUser user)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            return token;
        }

        public async Task<IdentityResult> ResetPasswordAsync(string token, string newPassword)
        {
            var users = await userManager.Users.ToListAsync(); // Извличане на всички потребители
            var user = users.FirstOrDefault(u => userManager.VerifyUserTokenAsync(u, userManager.Options.Tokens.PasswordResetTokenProvider, "ResetPassword", token).Result);

            if (user == null)
            {
                return IdentityResult.Failed(new IdentityError { Description = "Invalid token." });
            }

            // Логиране на потребителското име за диагностика
            Console.WriteLine($"User attempting password reset: {user.UserName}");

            var result = await userManager.ResetPasswordAsync(user, token, newPassword);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return IdentityResult.Failed(new IdentityError { Description = $"Password reset failed: {errors}" });
            }

            return result;
        }

    }
}
