﻿using SelfGuidedTours.Core.Models.Auth;

namespace SelfGuidedTours.Core.Contracts
{
    public interface IAuthService
    {
        Task<string> RegisterAsync(RegisterInputModel model);
        Task<LoginResponse> LoginAsync(LoginInputModel model);
        Task<string> LogoutAsync(LogoutInputModel model);
    }
}
