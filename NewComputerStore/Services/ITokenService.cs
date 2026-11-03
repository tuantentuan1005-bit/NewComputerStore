using System;
using NewComputerStore.Models;

namespace NewComputerStore.Services
{
    public interface ITokenService
    {
        (string AccessToken, DateTime ExpiresAt) GenerateAccessToken(User user);
    }
}
