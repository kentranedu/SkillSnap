using System.Net.Http.Json;
using SkillSnap.Client.Models;

namespace SkillSnap.Client.Services;

public class ProjectService
{
    private readonly HttpClient _httpClient;
    private readonly AuthService _authService;

    public ProjectService(HttpClient httpClient, AuthService authService)
    {
        _httpClient = httpClient;
        _authService = authService;
    }

    public async Task<List<Project>> GetProjectsAsync()
    {
        try
        {
            await _authService.EnsureAuthHeaderAsync();
            var response = await _httpClient.GetAsync("api/projects");
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(await BuildErrorMessageAsync(response, "load projects"));
            }

            var projects = await response.Content.ReadFromJsonAsync<List<Project>>();
            return projects ?? new List<Project>();
        }
        catch (TaskCanceledException ex)
        {
            throw new HttpRequestException("The request timed out while loading projects.", ex);
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new HttpRequestException("An unexpected error occurred while loading projects.", ex);
        }
    }

    public async Task<Project?> AddProjectAsync(Project newProject)
    {
        try
        {
            await _authService.EnsureAuthHeaderAsync();
            var response = await _httpClient.PostAsJsonAsync("api/projects", newProject);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(await BuildErrorMessageAsync(response, "add project"));
            }

            return await response.Content.ReadFromJsonAsync<Project>();
        }
        catch (TaskCanceledException ex)
        {
            throw new HttpRequestException("The request timed out while adding a project.", ex);
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new HttpRequestException("An unexpected error occurred while adding a project.", ex);
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
