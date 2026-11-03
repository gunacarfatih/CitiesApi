using CitiesApi.Data;
using CitiesApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReusableCaching.Interfaces;
using Microsoft.AspNetCore.Authorization;
using CitiesApi.Responses;

namespace CitiesApi.Controllers
{
    [ApiController]
    [Route("api/cities")]
    [Authorize]
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
        public async Task<IActionResult> BulkInsert([FromBody] List<CityInput> cities)
        {
            if (cities == null || cities.Count == 0)
            {
                return BadRequest(new BaseResponse<object>
                {
                    Success = false,
                    Message = "En az bir şehir göndermelisiniz.",
                    Status = StatusCodes.Status400BadRequest,
                    ErrorCode = "EMPTY_CITY_LIST",
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            var plateCodes = cities.Select(c => c.PlateCode).ToList();

            var hasExistingCity = await _context.Cities
                .AnyAsync(c => plateCodes.Contains(c.PlateCode));

            if (hasExistingCity || plateCodes.Distinct().Count() != plateCodes.Count)
            {
                return Conflict(new BaseResponse<object>
                {
                    Success = false,
                    Message = "Bu plaka kodlarından biri zaten kayıtlı veya gönderilen listede tekrarlanıyor.",
                    Status = StatusCodes.Status409Conflict,
                    ErrorCode = "DUPLICATE_PLATE_CODE",
                    TraceId = HttpContext.TraceIdentifier
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

                return Ok(new BaseResponse<object>
                {
                    Success = true,
                    Message = "Şehirler başarıyla eklendi.",
                    Status = StatusCodes.Status200OK,
                    Data = new
                    {
                        Count = newCities.Count
                    },
                    TraceId = HttpContext.TraceIdentifier
                });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex,
                    "Bulk city insert failed due to database constraints.");

                return Conflict(new BaseResponse<object>
                {
                    Success = false,
                    Message = "Şehirler eklenemedi. Plaka kodlarını ve veritabanı kısıtlamalarını kontrol edin.",
                    Status = StatusCodes.Status409Conflict,
                    ErrorCode = "DATABASE_CONSTRAINT_ERROR",
                    TraceId = HttpContext.TraceIdentifier
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
                return Ok(new BaseResponse<City>
                {
                    Success = true,
                    Message = "Şehir başarıyla getirildi.",
                    Status = StatusCodes.Status200OK,
                    Data = cachedCity,
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            var city = await _context.Cities
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.PlateCode == platecode);

            if (city == null)
            {
                return NotFound(new BaseResponse<City>
                {
                    Success = false,
                    Message = "Bu plaka koduna ait şehir bulunamadı.",
                    Status = StatusCodes.Status404NotFound,
                    ErrorCode = "CITY_NOT_FOUND",
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            await _cacheService.SetAsync(cacheKey, city);

            return Ok(new BaseResponse<City>
            {
                Success = true,
                Message = "Şehir başarıyla getirildi.",
                Status = StatusCodes.Status200OK,
                Data = city,
                TraceId = HttpContext.TraceIdentifier
            });
        }


        // PUT: api/cities/34
        [HttpPut("{plateCode:int}")]
        public async Task<IActionResult> UpdateCity(
            int plateCode,
            [FromBody] CityUpdate city)
        {
            if (city == null)
            {
                return BadRequest(new BaseResponse<object>
                {
                    Success = false,
                    Message = "Güncellenecek şehir bilgileri gönderilmelidir.",
                    Status = StatusCodes.Status400BadRequest,
                    ErrorCode = "INVALID_CITY_DATA",
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            var existingCity = await _context.Cities
                .FirstOrDefaultAsync(c => c.PlateCode == plateCode);

            if (existingCity == null)
            {
                return NotFound(new BaseResponse<object>
                {
                    Success = false,
                    Message = "Bu plaka koduna ait şehir bulunamadı.",
                    Status = StatusCodes.Status404NotFound,
                    ErrorCode = "CITY_NOT_FOUND",
                    TraceId = HttpContext.TraceIdentifier
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

            return Ok(new BaseResponse<City>
            {
                Success = true,
                Message = "Şehir bilgileri başarıyla güncellendi.",
                Status = StatusCodes.Status200OK,
                Data = existingCity,
                TraceId = HttpContext.TraceIdentifier
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
                return BadRequest(new BaseResponse<object>
                {
                    Success = false,
                    Message = "Aktiflik durumu gönderilmelidir.",
                    Status = StatusCodes.Status400BadRequest,
                    ErrorCode = "INVALID_STATUS_DATA",
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            var city = await _context.Cities
                .FirstOrDefaultAsync(c => c.PlateCode == plateCode);

            if (city == null)
            {
                return NotFound(new BaseResponse<object>
                {
                    Success = false,
                    Message = "Bu plaka koduna ait şehir bulunamadı.",
                    Status = StatusCodes.Status404NotFound,
                    ErrorCode = "CITY_NOT_FOUND",
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            city.IsActive = request.IsActive;

            await _context.SaveChangesAsync();
            await ClearCacheAsync();

            _logger.LogInformation(
                "City status changed. PlateCode: {PlateCode}, IsActive: {IsActive}",
                city.PlateCode,
                city.IsActive);

            return Ok(new BaseResponse<City>
            {
                Success = true,
                Message = request.IsActive
                    ? "Şehir aktif duruma getirildi."
                    : "Şehir pasif duruma getirildi.",
                Status = StatusCodes.Status200OK,
                Data = city,
                TraceId = HttpContext.TraceIdentifier
            });
        }


        // GET: api/cities/search?keyword=hamsi
        [HttpGet("search")]
        public async Task<IActionResult> SearchCities([FromQuery] string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return BadRequest(new BaseResponse<object>
                {
                    Success = false,
                    Message = "Aranacak kelime boş olamaz.",
                    Status = StatusCodes.Status400BadRequest,
                    ErrorCode = "EMPTY_SEARCH_KEYWORD",
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            string normalizedKeyword = keyword.Trim().ToLowerInvariant();

            string cacheKey =
                $"city_search_{await GetCacheVersionAsync()}_{normalizedKeyword}";

            var cachedCities =
                await _cacheService.GetAsync<List<City>>(cacheKey);

            if (cachedCities != null)
            {
                return Ok(new BaseResponse<List<City>>
                {
                    Success = true,
                    Message = "Şehirler başarıyla arandı.",
                    Status = StatusCodes.Status200OK,
                    Data = cachedCities,
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            var cities = await _context.Cities
                .AsNoTracking()
                .Where(c => EF.Functions.ILike(
                    c.Description, $"%{normalizedKeyword}%"))
                .OrderBy(c => c.PlateCode)
                .ToListAsync();

            await _cacheService.SetAsync(cacheKey, cities);

            return Ok(new BaseResponse<List<City>>
            {
                Success = true,
                Message = cities.Count > 0
                    ? "Şehirler başarıyla arandı."
                    : "Aramayla eşleşen şehir bulunamadı.",
                Status = StatusCodes.Status200OK,
                Data = cities,
                TraceId = HttpContext.TraceIdentifier
            });
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
                return NotFound(new BaseResponse<object>
                {
                    Success = false,
                    Message = "Şehir cache içerisinde bulunamadı.",
                    Status = StatusCodes.Status404NotFound,
                    ErrorCode = "CITY_NOT_IN_CACHE",
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            return Ok(new BaseResponse<City>
            {
                Success = true,
                Message = "Şehir cache içerisinden başarıyla getirildi.",
                Status = StatusCodes.Status200OK,
                Data = city,
                TraceId = HttpContext.TraceIdentifier
            });
        }


        // GET: api/cities?page=1&pageSize=10
        [HttpGet]
        public async Task<IActionResult> GetCities(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            if (page < 1 || pageSize < 1 || pageSize > 100)
            {
                return BadRequest(new BaseResponse<object>
                {
                    Success = false,
                    Message = "Sayfa numarası en az 1, sayfa boyutu ise 1 ile 100 arasında olmalıdır.",
                    Status = StatusCodes.Status400BadRequest,
                    ErrorCode = "INVALID_PAGINATION_PARAMETERS",
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            // Cache sürümünü al
            string cacheVersion = await GetCacheVersionAsync();

            // Sayfa ve sayfa boyutuna özel cache anahtarı
            string cacheKey =
                $"cities_page_{cacheVersion}_{page}_{pageSize}";

            // Önce cache kontrolü
            var cities = await _cacheService.GetAsync<List<City>>(cacheKey);
            if (cities != null)
            {
                _logger.LogInformation(
                    "Cities pagination cache HIT. Page: {Page}, PageSize: {PageSize}",
                    page,
                    pageSize);
            }
            else
            {
                _logger.LogInformation(
                    "Cities pagination cache MISS. Page: {Page}, PageSize: {PageSize}",
                    page,
                    pageSize);
            }

            // Cache boşsa veritabanından getir
            if (cities == null)
            {
                cities = await _context.Cities
                    .AsNoTracking()
                    .OrderBy(c => c.PlateCode)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                await _cacheService.SetAsync(cacheKey, cities);
            }

            // Toplam kayıt sayısı
            int totalCount = await _context.Cities.CountAsync();

            var response = new BasePagedResponse<List<City>>
            {
                Success = true,
                Message = "Şehirler başarıyla listelendi.",
                Status = StatusCodes.Status200OK,
                Data = cities,
                Pagination = new PaginationMetadata
                {
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(
                        totalCount / (double)pageSize)
                },
                TraceId = HttpContext.TraceIdentifier
            };

            return Ok(response);
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
