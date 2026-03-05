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
        if (_cache.TryGetValue(ProjectsCacheKey, out List<Project>? cachedProjects))
        {
            return Ok(cachedProjects ?? new List<Project>());
        }

        try
        {
            var freshProjects = await _context.Projects
                .Include(project => project.PortfolioUser)
                .AsNoTracking()
                .ToListAsync();

            _cache.Set(ProjectsCacheKey, freshProjects, ProjectsCacheOptions);
            _cache.Set(ProjectsFallbackCacheKey, freshProjects, ProjectsFallbackCacheOptions);

            return Ok(freshProjects);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load projects from database. Attempting fallback cache.");

            if (_cache.TryGetValue(ProjectsFallbackCacheKey, out List<Project>? fallbackProjects))
            {
                Response.Headers.Append("X-Data-Source", "cache-fallback");
                return Ok(fallbackProjects ?? new List<Project>());
            }

            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Projects are temporarily unavailable. Please try again shortly."
            });
        }
    }
}
