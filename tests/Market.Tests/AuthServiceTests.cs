namespace Market.Tests;

public class AuthServiceTests
{
    private const string Password = "Passw0rd!";

    private static AuthService CreateService(TestEnv env) => new(env.Users, env.Tokens, env.Db);

    [Fact]
    public async Task Register_CreatesConsumer_AndReturnsTokens()
    {
        using var env = new TestEnv();
        var service = CreateService(env);

        var result = await service.RegisterAsync(
            new RegisterDto { FullName = "Ali", Email = "ali@test.com", Password = Password });

        Assert.True(result.Succeeded);
        Assert.NotEmpty(result.Response!.AccessToken);
        Assert.NotEmpty(result.Response.RefreshToken);
        Assert.Contains("Consumer", result.Response.Roles);
    }

    [Fact]
    public async Task Register_WeakPassword_ReturnsValidationError()
    {
        using var env = new TestEnv();
        var service = CreateService(env);

        var result = await service.RegisterAsync(
            new RegisterDto { FullName = "Ali", Email = "ali@test.com", Password = "weakpass" });

        Assert.Equal(AuthError.Validation, result.Error);
    }

    [Fact]
    public async Task Login_WrongPassword_And_UnknownEmail_GiveSameError()
    {
        using var env = new TestEnv();
        await env.CreateUserAsync("ali@test.com", password: Password);
        var service = CreateService(env);

        var wrongPassword = await service.LoginAsync(new LoginDto { Email = "ali@test.com", Password = "Wrong123!" });
        var unknownEmail = await service.LoginAsync(new LoginDto { Email = "nobody@test.com", Password = Password });

        Assert.Equal(AuthError.InvalidCredentials, wrongPassword.Error);
        Assert.Equal(AuthError.InvalidCredentials, unknownEmail.Error);
    }

    [Fact]
    public async Task Login_AfterFiveWrongPasswords_AccountIsLocked_EvenWithCorrectPassword()
    {
        using var env = new TestEnv();
        await env.CreateUserAsync("ali@test.com", password: Password);
        var service = CreateService(env);

        for (var i = 0; i < 5; i++)
            await service.LoginAsync(new LoginDto { Email = "ali@test.com", Password = "Wrong123!" });

        var result = await service.LoginAsync(new LoginDto { Email = "ali@test.com", Password = Password });

        Assert.Equal(AuthError.LockedOut, result.Error);
    }

    [Fact]
    public async Task Login_Success_ResetsFailedCounter()
    {
        using var env = new TestEnv();
        await env.CreateUserAsync("ali@test.com", password: Password);
        var service = CreateService(env);

        for (var i = 0; i < 4; i++)
            await service.LoginAsync(new LoginDto { Email = "ali@test.com", Password = "Wrong123!" });
        Assert.True((await service.LoginAsync(new LoginDto { Email = "ali@test.com", Password = Password })).Succeeded);

        // شمارنده صفر شده؛ پس چهار اشتباه دیگه هنوز قفل نمی‌کنه
        for (var i = 0; i < 4; i++)
            await service.LoginAsync(new LoginDto { Email = "ali@test.com", Password = "Wrong123!" });
        var result = await service.LoginAsync(new LoginDto { Email = "ali@test.com", Password = Password });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Refresh_ReturnsNewPair_AndOldTokenIsRevoked()
    {
        using var env = new TestEnv();
        var service = CreateService(env);
        var first = (await service.RegisterAsync(
            new RegisterDto { FullName = "Ali", Email = "ali@test.com", Password = Password })).Response!;

        var refreshed = await service.RefreshAsync(first.RefreshToken);

        Assert.True(refreshed.Succeeded);
        Assert.NotEqual(first.RefreshToken, refreshed.Response!.RefreshToken);
    }

    [Fact]
    public async Task Refresh_ReusingOldToken_RevokesAllTokensOfUser()
    {
        using var env = new TestEnv();
        var service = CreateService(env);
        var first = (await service.RegisterAsync(
            new RegisterDto { FullName = "Ali", Email = "ali@test.com", Password = Password })).Response!;
        var second = (await service.RefreshAsync(first.RefreshToken)).Response!;

        var reuse = await service.RefreshAsync(first.RefreshToken);          // توکن قدیمی دوباره استفاده شد
        var newestNow = await service.RefreshAsync(second.RefreshToken);     // توکن جدید هم باید باطل شده باشه

        Assert.Equal(AuthError.TokenReused, reuse.Error);
        Assert.Equal(AuthError.TokenReused, newestNow.Error);
    }

    [Fact]
    public async Task Refresh_UnknownToken_IsInvalid()
    {
        using var env = new TestEnv();
        var service = CreateService(env);

        var result = await service.RefreshAsync("not-a-real-token");

        Assert.Equal(AuthError.InvalidToken, result.Error);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        using var env = new TestEnv();
        var service = CreateService(env);
        var tokens = (await service.RegisterAsync(
            new RegisterDto { FullName = "Ali", Email = "ali@test.com", Password = Password })).Response!;

        await service.LogoutAsync(tokens.RefreshToken);
        var result = await service.RefreshAsync(tokens.RefreshToken);

        Assert.False(result.Succeeded);
    }
}
