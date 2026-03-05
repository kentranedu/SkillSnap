using System.Net.Http.Json;
using SkillSnap.Client.Models;

namespace SkillSnap.Client.Services;

public class ProjectService
{
    private readonly HttpClient _httpClient;

    public ProjectService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<Project>> GetProjectsAsync()
    {
        var projects = await _httpClient.GetFromJsonAsync<List<Project>>("api/projects");
        return projects ?? new List<Project>();
    }

    public async Task<Project?> AddProjectAsync(Project newProject)
    {
        var response = await _httpClient.PostAsJsonAsync("api/projects", newProject);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Project>();
    }
}
