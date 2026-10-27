
using Microsoft.AspNetCore.Mvc;
using CitiesApi.Models;
using CitiesApi.Data;
using Microsoft.EntityFrameworkCore;

namespace CitiesApi.Controllers
{
    [ApiController]
    [Route("api/cities")]
    public class CitiesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CitiesController(ApplicationDbContext context)
        {
            _context = context;
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

                    if (hasExistingCity || plateCodes.Distinct().Count() != plateCodes.Count)
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

                        return Ok(new
                        {
                            message = "Şehirler başarıyla eklendi.",
                            count = newCities.Count
                        });
                    }
                    catch (DbUpdateException)
                    {
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
            var city = await _context.Cities
                .AsNoTracking()
                .Where(c => c.PlateCode == platecode)
                .Select(c => new
                {
                    name = c.Name,
                    plateCode = c.PlateCode,
                    description = c.Description,
                    isActive = c.IsActive
                })
                .FirstOrDefaultAsync();

            if (city == null)
            {
                return NotFound(new
                {
                    message = "Bu plaka koduna ait şehir bulunamadı."
                });
            }

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

                    var cities = await _context.Cities
                        .AsNoTracking()
                        .Where(c => EF.Functions.ILike(c.Description, $"%{keyword}%"))
                        .OrderBy(c => c.PlateCode)
                        .Select(c => new
                        {
                            name = c.Name,
                            plateCode = c.PlateCode,
                            description = c.Description,
                            isActive = c.IsActive
                        })
                        .ToListAsync();

                    return Ok(cities);
                }

            }

   
}

