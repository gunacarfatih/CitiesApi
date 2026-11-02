using CitiesApi.Data;
using CitiesApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReusableCaching.Interfaces;

namespace CitiesApi.Controllers
{
    [ApiController]
    [Route("api/cities")]
    public class CitiesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ICacheService _cacheService;
        private const string CityCacheVersionKey = "cities_cache_version_v2";
        private static readonly TimeSpan CacheVersionDuration = TimeSpan.FromDays(365);
        private readonly ILogger<CitiesController> _logger;

        public CitiesController(
            ApplicationDbContext context,
            ICacheService cacheService,
            ILogger<CitiesController> logger)
        {
            _context = context;
            _cacheService = cacheService;
            _logger = logger;
        }


        // POST: api/cities/bulk
        [HttpPost("bulk")]
        public async Task<IActionResult> BulkInsert(
            [FromBody] List<CityInput> cities)
        {
            if (cities == null || cities.Count == 0)
            {
                return BadRequest(new
                {
                    message = "En az bir şehir göndermelisiniz."
                });
            }

            var plateCodes = cities.Select(c => c.PlateCode).ToList();

            var hasExistingCity = await _context.Cities
                .AnyAsync(c => plateCodes.Contains(c.PlateCode));

            if (hasExistingCity ||
                plateCodes.Distinct().Count() != plateCodes.Count)
            {
                return Conflict(new
                {
                    message = "Bu plaka kodlarından biri zaten kayıtlı veya gönderilen listede tekrarlanıyor."
                });
            }

            var newCities = cities.Select(c => new City
            {
                Name = c.Name,
                PlateCode = c.PlateCode,
                Description = c.Description,
                IsActive = c.IsActive
            }).ToList();

            try
            {
                await _context.Cities.AddRangeAsync(newCities);
                await _context.SaveChangesAsync();

                await ClearCacheAsync();

                _logger.LogInformation(
                    "Bulk city insert completed. Count: {Count}",
                    newCities.Count);

                return Ok(new
                {
                    message = "Şehirler başarıyla eklendi.",
                    count = newCities.Count
                });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex,
                    "Bulk city insert failed due to database constraints.");

                return Conflict(new
                {
                    message = "Şehirler eklenemedi. Plaka kodlarını ve veritabanı kısıtlamalarını kontrol edin."
                });
            }
        }


        // GET: api/cities/34
        [HttpGet("{platecode:int}")]
        public async Task<IActionResult> GetCityByPlateCode(int platecode)
        {
            string cacheKey =
                $"city_{await GetCacheVersionAsync()}_{platecode}";

            var cachedCity =
                await _cacheService.GetAsync<City>(cacheKey);

            if (cachedCity != null)
            {
                return Ok(cachedCity);
            }

            var city = await _context.Cities
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.PlateCode == platecode);

            if (city == null)
            {
                return NotFound(new
                {
                    message = "Bu plaka koduna ait şehir bulunamadı."
                });
            }

            await _cacheService.SetAsync(cacheKey, city);

            return Ok(city);
        }


        // PUT: api/cities/34
        [HttpPut("{plateCode:int}")]
        public async Task<IActionResult> UpdateCity(
            int plateCode,
            [FromBody] CityUpdate city)
        {
            if (city == null)
            {
                return BadRequest(new
                {
                    message = "Güncellenecek şehir bilgileri gönderilmelidir."
                });
            }

            var existingCity = await _context.Cities
                .FirstOrDefaultAsync(c => c.PlateCode == plateCode);

            if (existingCity == null)
            {
                return NotFound(new
                {
                    message = "Bu plaka koduna ait şehir bulunamadı."
                });
            }

            existingCity.Name = city.Name;
            existingCity.Description = city.Description;
            existingCity.IsActive = city.IsActive;

            await _context.SaveChangesAsync();
            _logger.LogInformation(
                "City updated. PlateCode: {PlateCode}, CityName: {CityName}, IsActive: {IsActive}",
                existingCity.PlateCode,
                existingCity.Name,
                existingCity.IsActive);
            await ClearCacheAsync();

            return Ok(new
            {
                message = "Şehir bilgileri başarıyla güncellendi.",
                plateCode = existingCity.PlateCode,
                name = existingCity.Name,
                description = existingCity.Description,
                isActive = existingCity.IsActive
            });
        }


        // PATCH: api/cities/34/status
        [HttpPatch("{plateCode:int}/status")]
        public async Task<IActionResult> ChangeCityStatus(
            int plateCode,
            [FromBody] CityStatusUpdate request)
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    message = "Aktiflik durumu gönderilmelidir."
                });
            }

            var city = await _context.Cities
                .FirstOrDefaultAsync(c => c.PlateCode == plateCode);

            if (city == null)
            {
                return NotFound(new
                {
                    message = "Bu plaka koduna ait şehir bulunamadı."
                });
            }

            city.IsActive = request.IsActive;

            await _context.SaveChangesAsync();
            await ClearCacheAsync();

            return Ok(new
            {
                message = request.IsActive
                    ? "Şehir aktif duruma getirildi."
                    : "Şehir pasif duruma getirildi.",
                plateCode = city.PlateCode,
                isActive = city.IsActive
            });
        }


        // GET: api/cities/search?keyword=hamsi
        [HttpGet("search")]
        public async Task<IActionResult> SearchCities(
            [FromQuery] string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return BadRequest(new
                {
                    message = "Aranacak kelime boş olamaz."
                });
            }

            string normalizedKeyword =
                keyword.Trim().ToLowerInvariant();

            string cacheKey =
                $"city_search_{await GetCacheVersionAsync()}_{normalizedKeyword}";

            var cachedCities =
                await _cacheService.GetAsync<List<City>>(cacheKey);

            if (cachedCities != null)
            {
                return Ok(cachedCities);
            }

            var cities = await _context.Cities
                .AsNoTracking()
                .Where(c => EF.Functions.ILike(
                    c.Description, $"%{normalizedKeyword}%"))
                .OrderBy(c => c.PlateCode)
                .ToListAsync();

            await _cacheService.SetAsync(cacheKey, cities);

            return Ok(cities);
        }


        // GET: api/cities/cache/34
        [HttpGet("cache/{plateCode:int}")]
        public async Task<IActionResult> GetFromCache(int plateCode)
        {
            string cacheKey =
                $"city_{await GetCacheVersionAsync()}_{plateCode}";

            var city = await _cacheService.GetAsync<City>(cacheKey);

            if (city == null)
            {
                return NotFound(new
                {
                    message = "Şehir cache içerisinde bulunamadı."
                });
            }

            return Ok(city);
        }


        private async Task<string> GetCacheVersionAsync()
        {
            string? version =
                await _cacheService.GetAsync<string>(
                    CityCacheVersionKey);

            if (version == null)
            {
                version = Guid.NewGuid().ToString();

                await _cacheService.SetAsync(
                    CityCacheVersionKey,
                    version,
                    CacheVersionDuration);
            }

            return version;
        }


        private async Task ClearCacheAsync()
        {
            string newVersion = Guid.NewGuid().ToString();

            await _cacheService.SetAsync(
                CityCacheVersionKey,
                newVersion,
                CacheVersionDuration);
        }

    }
}
