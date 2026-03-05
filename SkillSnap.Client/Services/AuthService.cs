using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.JSInterop;

namespace SkillSnap.Client.Services;

public class AuthService
{
    private const string TokenStorageKey = "skillsnap.auth.token";

    private readonly HttpClient _httpClient;
    private readonly IJSRuntime _jsRuntime;
    private readonly UserSessionService _userSession;

    private bool _isInitialized;

    public AuthService(HttpClient httpClient, IJSRuntime jsRuntime, UserSessionService userSession)
    {
        _httpClient = httpClient;
        _jsRuntime = jsRuntime;
        _userSession = userSession;
    }

    public event Action? AuthStateChanged;

    public bool IsAuthenticated { get; private set; }

    public string? Token { get; private set; }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;

        var token = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", TokenStorageKey);
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        SetAuthenticatedState(token);
    }

    public async Task RegisterAsync(string email, string password)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/register", new RegisterRequest(email, password));
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(await BuildErrorMessageAsync(response, "register"));
        }
    }

    public async Task LoginAsync(string email, string password)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/login", new LoginRequest(email, password));
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(await BuildErrorMessageAsync(response, "log in"));
        }

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        if (string.IsNullOrWhiteSpace(loginResponse?.Token))
        {
            throw new HttpRequestException("Login succeeded but no token was returned by the server.");
        }

        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", TokenStorageKey, loginResponse.Token);
        SetAuthenticatedState(loginResponse.Token);
    }

    public async Task LogoutAsync()
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", TokenStorageKey);

        Token = null;
        IsAuthenticated = false;
        _httpClient.DefaultRequestHeaders.Authorization = null;
        _userSession.ClearAll();

        AuthStateChanged?.Invoke();
    }

    public async Task EnsureAuthHeaderAsync()
    {
        if (!_isInitialized)
        {
            await InitializeAsync();
        }

        if (!string.IsNullOrWhiteSpace(Token) && _httpClient.DefaultRequestHeaders.Authorization is null)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        }
    }

    private void SetAuthenticatedState(string token)
    {
        Token = token;
        IsAuthenticated = true;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _userSession.SetUserContextFromJwt(token);

        AuthStateChanged?.Invoke();
    }

    private static async Task<string> BuildErrorMessageAsync(HttpResponseMessage response, string operation)
    {
        var responseBody = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return $"Unable to {operation}. Server returned {(int)response.StatusCode} ({response.ReasonPhrase}).";
        }

        return $"Unable to {operation}. Server returned {(int)response.StatusCode} ({response.ReasonPhrase}): {responseBody}";
    }

    private sealed record RegisterRequest(string Email, string Password);

    private sealed record LoginRequest(string Email, string Password);

    private sealed record LoginResponse(string Token, int ExpiresInMinutes);
}
