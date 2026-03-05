using System.Net.Http.Json;
using SkillSnap.Client.Models;

namespace SkillSnap.Client.Services;

public class SkillService
{
    private readonly HttpClient _httpClient;
    private readonly AuthService _authService;

    public SkillService(HttpClient httpClient, AuthService authService)
    {
        _httpClient = httpClient;
        _authService = authService;
    }

    public async Task<List<Skill>> GetSkillsAsync()
    {
        try
        {
            await _authService.EnsureAuthHeaderAsync();
            var response = await _httpClient.GetAsync("api/skills");
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(await BuildErrorMessageAsync(response, "load skills"));
            }

            var skills = await response.Content.ReadFromJsonAsync<List<Skill>>();
            return skills ?? new List<Skill>();
        }
        catch (TaskCanceledException ex)
        {
            throw new HttpRequestException("The request timed out while loading skills.", ex);
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new HttpRequestException("An unexpected error occurred while loading skills.", ex);
        }
    }

    public async Task<Skill?> AddSkillAsync(Skill newSkill)
    {
        try
        {
            await _authService.EnsureAuthHeaderAsync();
            var response = await _httpClient.PostAsJsonAsync("api/skills", newSkill);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(await BuildErrorMessageAsync(response, "add skill"));
            }

            return await response.Content.ReadFromJsonAsync<Skill>();
        }
        catch (TaskCanceledException ex)
        {
            throw new HttpRequestException("The request timed out while adding a skill.", ex);
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new HttpRequestException("An unexpected error occurred while adding a skill.", ex);
        }
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
}
