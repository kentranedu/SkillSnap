using System.Diagnostics;
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

            var freshProjects = await _context.Projects
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

            _cache.Set(ProjectsCacheKey, freshProjects, ProjectsCacheOptions);
            _cache.Set(ProjectsFallbackCacheKey, freshProjects, ProjectsFallbackCacheOptions);

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

    private sealed record ProjectListItem(
        int Id,
        string Title,
        string Description,
        string ImageUrl,
        int PortfolioUserId,
        string? PortfolioUserName);
}
