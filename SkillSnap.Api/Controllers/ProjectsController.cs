using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SkillSnap.Api.Models;

namespace SkillSnap.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly SkillSnapContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ProjectsController> _logger;

    private const string ProjectsCacheKey = "projects:fresh";
    private const string ProjectsFallbackCacheKey = "projects:fallback";

    private static readonly MemoryCacheEntryOptions ProjectsCacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
    };

    private static readonly MemoryCacheEntryOptions ProjectsFallbackCacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
    };

    public ProjectsController(
        SkillSnapContext context,
        IMemoryCache cache,
        ILogger<ProjectsController> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetProjects()
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (_cache.TryGetValue(ProjectsCacheKey, out List<ProjectListItem>? cachedProjects))
            {
                Response.Headers.Append("X-Cache", "HIT");
                _logger.LogInformation(
                    "Projects cache HIT for key {CacheKey}. DurationMs={DurationMs}",
                    ProjectsCacheKey,
                    stopwatch.ElapsedMilliseconds);

                return Ok(cachedProjects ?? new List<ProjectListItem>());
            }

            _logger.LogInformation("Projects cache MISS for key {CacheKey}", ProjectsCacheKey);
            var freshProjects = await LoadProjectsFromDbAsync();
            SetProjectsCache(freshProjects);

            Response.Headers.Append("X-Cache", "MISS");
            _logger.LogInformation(
                "Projects cache MISS resolved from DB. Cached {Count} items. DurationMs={DurationMs}",
                freshProjects.Count,
                stopwatch.ElapsedMilliseconds);

            return Ok(freshProjects);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load projects from database. Attempting fallback cache.");

            if (_cache.TryGetValue(ProjectsFallbackCacheKey, out List<ProjectListItem>? fallbackProjects))
            {
                Response.Headers.Append("X-Cache", "FALLBACK");
                Response.Headers.Append("X-Data-Source", "cache-fallback");

                _logger.LogWarning(
                    "Projects cache FALLBACK served from key {CacheKey}. DurationMs={DurationMs}",
                    ProjectsFallbackCacheKey,
                    stopwatch.ElapsedMilliseconds);

                return Ok(fallbackProjects ?? new List<ProjectListItem>());
            }

            _logger.LogError(
                ex,
                "Projects request failed with no fallback cache available. DurationMs={DurationMs}",
                stopwatch.ElapsedMilliseconds);

            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Projects are temporarily unavailable. Please try again shortly."
            });
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetProjectById(int id)
    {
        var project = await _context.Projects
            .AsNoTracking()
            .Where(project => project.Id == id)
            .Select(project => new ProjectListItem(
                project.Id,
                project.Title,
                project.Description,
                project.ImageUrl,
                project.PortfolioUserId,
                project.PortfolioUser != null ? project.PortfolioUser.Name : null))
            .FirstOrDefaultAsync();

        return project is null ? NotFound() : Ok(project);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> AddProject([FromBody] Project project)
    {
        _context.Projects.Add(project);
        await _context.SaveChangesAsync();

        await RefreshProjectsCacheAsync("create", project.Id);

        return CreatedAtAction(nameof(GetProjectById), new { id = project.Id }, project);
    }

    [HttpPut("{id:int}")]
    [Authorize]
    public async Task<IActionResult> UpdateProject(int id, [FromBody] Project updatedProject)
    {
        if (id != updatedProject.Id)
        {
            return BadRequest(new { message = "Route id does not match payload id." });
        }

        var existingProject = await _context.Projects.FindAsync(id);
        if (existingProject is null)
        {
            return NotFound();
        }

        existingProject.Title = updatedProject.Title;
        existingProject.Description = updatedProject.Description;
        existingProject.ImageUrl = updatedProject.ImageUrl;
        existingProject.PortfolioUserId = updatedProject.PortfolioUserId;

        await _context.SaveChangesAsync();
        await RefreshProjectsCacheAsync("update", id);

        return Ok(existingProject);
    }

    [HttpDelete("{id:int}")]
    [Authorize]
    public async Task<IActionResult> DeleteProject(int id)
    {
        var project = await _context.Projects.FindAsync(id);
        if (project is null)
        {
            return NotFound();
        }

        _context.Projects.Remove(project);
        await _context.SaveChangesAsync();
        await RefreshProjectsCacheAsync("delete", id);

        return NoContent();
    }

    private async Task<List<ProjectListItem>> LoadProjectsFromDbAsync()
    {
        return await _context.Projects
            .Include(project => project.PortfolioUser)
            .AsNoTracking()
            .Select(project => new ProjectListItem(
                project.Id,
                project.Title,
                project.Description,
                project.ImageUrl,
                project.PortfolioUserId,
                project.PortfolioUser != null ? project.PortfolioUser.Name : null))
            .ToListAsync();
    }

    private void SetProjectsCache(List<ProjectListItem> projects)
    {
        _cache.Set(ProjectsCacheKey, projects, ProjectsCacheOptions);
        _cache.Set(ProjectsFallbackCacheKey, projects, ProjectsFallbackCacheOptions);
    }

    private async Task RefreshProjectsCacheAsync(string operation, int projectId)
    {
        _cache.Remove(ProjectsCacheKey);

        _logger.LogInformation(
            "Projects cache INVALIDATE after {Operation}. ProjectId={ProjectId}. Removed key={CacheKey}",
            operation,
            projectId,
            ProjectsCacheKey);

        var refreshedProjects = await LoadProjectsFromDbAsync();
        SetProjectsCache(refreshedProjects);

        _logger.LogInformation(
            "Projects cache REFRESH after {Operation}. ProjectId={ProjectId}. CachedCount={CachedCount}. Keys=[{FreshKey},{FallbackKey}]",
            operation,
            projectId,
            refreshedProjects.Count,
            ProjectsCacheKey,
            ProjectsFallbackCacheKey);
    }

    private sealed record ProjectListItem(
        int Id,
        string Title,
        string Description,
        string ImageUrl,
        int PortfolioUserId,
        string? PortfolioUserName);
}
