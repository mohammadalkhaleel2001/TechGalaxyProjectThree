using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TechGalaxyProject.Data;
using TechGalaxyProject.Data.Models;
using TechGalaxyProject.Models;

namespace TechGalaxyProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    
    public class RoadmapsController : ControllerBase
    {
        public RoadmapsController (AppDbContext db)
        {
            _db = db;
        }
        private readonly AppDbContext _db;

        [HttpPost("like/{roadmapId}")]
        public async Task<IActionResult> LikeRoadmap(int roadmapId)
        {
            var roadmap = await _db.roadmaps.FindAsync(roadmapId);

            if (roadmap == null)
                return NotFound(new { message = "Roadmap not found." });

            roadmap.LikesCount += 1;
            await _db.SaveChangesAsync();

            return Ok(new
            {
                roadmap.Id,
                roadmap.LikesCount
            });
        }

        [HttpGet("by-difficulty/{level}")]
        public async Task<IActionResult> GetByDifficulty(string level)
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            var roadmaps = await _db.roadmaps
                .Include(r => r.User)
                .Where(r => r.DifficultyLevel.ToLower() == level.ToLower())
                .OrderByDescending(r => r.LikesCount)
                .Select(r => new
                {
                    r.Id,
                    r.Title,
                    r.Description,
                    CoverImageUrl = string.IsNullOrEmpty(r.CoverImageUrl)
                    ? $"{baseUrl}/Images/default.png"
                    : $"{baseUrl}{r.CoverImageUrl}",

                    r.Category,
                    r.DifficultyLevel,
                    CreatedAt = r.CreatedAt.ToString("yyyy-MM-dd"),
                    LikesCount = r.LikesCount,
                    ExpertName = r.User.UserName
                })
                .ToListAsync();

            return Ok(roadmaps);
        }


        [HttpGet("by-category/{category}")]
        public async Task<IActionResult> GetByCategory(string category)
        {
            var roadmaps = await _db.roadmaps
                .Include(r => r.User)
                .Where(r => r.Category.ToLower() == category.ToLower())
                .OrderByDescending(r => r.LikesCount)
                .Select(r => new
                {
                    r.Id,
                    r.Title,
                    r.Description,
                    CoverImageUrl = string.IsNullOrEmpty(r.CoverImageUrl)
    ? $"{Request.Scheme}://{Request.Host}/Images/default.png"
    : $"{Request.Scheme}://{Request.Host}{r.CoverImageUrl}",

                    r.Category,
                    r.DifficultyLevel,
                    CreatedAt = r.CreatedAt.ToString("yyyy-MM-dd"),
                    LikesCount = r.LikesCount,
                    ExpertName = r.User.UserName
                })
                .ToListAsync();

            return Ok(roadmaps);
        }

        [HttpGet("all")]
        public async Task<IActionResult> GetAllRoadmaps()
        {
            var roadmaps = await _db.roadmaps
                .Include(r => r.User) // جلب بيانات المستخدم (الإكسبيرت)
                .OrderByDescending(r => r.LikesCount)
                .Select(r => new
                {
                    r.Id,
                    r.Title,
                    r.Description,
                    CoverImageUrl = string.IsNullOrEmpty(r.CoverImageUrl)
    ? $"{Request.Scheme}://{Request.Host}/Images/default.png"
    : $"{Request.Scheme}://{Request.Host}{r.CoverImageUrl}",

                    r.Category,
                    r.DifficultyLevel,
                    CreatedAt = r.CreatedAt.ToString("dd/MM/yyyy"), // ⬅ التاريخ بشكل مرتب,
                    r.LikesCount,
                    ExpertName = r.User.UserName // أو r.User.FullName إذا عندك هيك خاصية
                })
                .ToListAsync();

            return Ok(roadmaps);
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var total = await _db.roadmaps.CountAsync();
            var beginner = await _db.roadmaps.CountAsync(r => r.DifficultyLevel == "beginner");
            var intermediate = await _db.roadmaps.CountAsync(r => r.DifficultyLevel == "intermediate");
            var advanced = await _db.roadmaps.CountAsync(r => r.DifficultyLevel == "advanced");

            return Ok(new
            {
                total,
                beginner,
                intermediate,
                advanced
            });
        }


        [HttpPost("create-or-update")]
        public async Task<IActionResult> CreateOrUpdate([FromForm] CreateOrUpdateRoadmapDto dto)
        {
            var existingRoadmap = await _db.roadmaps
                .FirstOrDefaultAsync(r => r.Tag == dto.Tag);

            if (existingRoadmap == null)
            {
                var newRoadmap = new Roadmap
                {
                    Title = dto.Title,
                    Description = dto.Description,
                    Category = dto.Category,
                    Tag = dto.Tag,
                    DifficultyLevel = dto.DifficultyLevel,
                    CreatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier),
                    CreatedAt = DateTime.UtcNow
                };

                // معالجة الصورة
                if (dto.CoverImage != null)
                {
                    var uploads = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                    var fileName = $"{Guid.NewGuid()}{Path.GetExtension(dto.CoverImage.FileName)}";
                    var filePath = Path.Combine(uploads, fileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await dto.CoverImage.CopyToAsync(fileStream);
                    }

                    newRoadmap.CoverImageUrl = $"/uploads/{fileName}";
                }

                _db.roadmaps.Add(newRoadmap);
                await _db.SaveChangesAsync();

                var field = new Field
                {
                    Title = dto.StepTitle,
                    Description = dto.StepDescription,
                    RoadmapId = newRoadmap.Id,
                    Order = 1
                };

                _db.fields.Add(field);
                await _db.SaveChangesAsync();

                var fields = new List<FieldDto>
                {
                    new FieldDto
                    {
                        Id = field.Id,
                        Title = field.Title,
                        Description = field.Description,
                        Order = field.Order
                    }
                };

                return Ok(fields);
            }
            else
            {
                existingRoadmap.Title = dto.Title;
                existingRoadmap.Description = dto.Description;
                existingRoadmap.Category = dto.Category;
                existingRoadmap.DifficultyLevel = dto.DifficultyLevel;

                // معالجة الصورة
                if (dto.CoverImage != null)
                {
                    var uploads = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                    var fileName = $"{Guid.NewGuid()}{Path.GetExtension(dto.CoverImage.FileName)}";
                    var filePath = Path.Combine(uploads, fileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await dto.CoverImage.CopyToAsync(fileStream);
                    }

                    existingRoadmap.CoverImageUrl = $"/uploads/{fileName}";
                }

                await _db.SaveChangesAsync();

                var field = new Field
                {
                    Title = dto.StepTitle,
                    Description = dto.StepDescription,
                    RoadmapId = existingRoadmap.Id,
                    Order = existingRoadmap.fields.Count + 1
                };

                _db.fields.Add(field);
                await _db.SaveChangesAsync();

                var fields = await _db.fields
                    .Where(f => f.RoadmapId == existingRoadmap.Id)
                    .Select(f => new FieldDto
                    {
                        Id = f.Id,
                        Title = f.Title,
                        Description = f.Description,
                        Order = f.Order
                    })
                    .ToListAsync();

                return Ok(fields);
            }
        }
    }
}
