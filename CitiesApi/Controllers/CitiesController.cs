
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace CitiesApi.Controllers
{
    [ApiController]
    [Route("api/cities")]
    public class CitiesController : ControllerBase
    {
        private readonly string connectionString =
            "Host=localhost;Port=5432;Database=staj;Username=postgres;Password=123456";



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

            await using var connection =
                new NpgsqlConnection(connectionString);

            await connection.OpenAsync();

            await using var transaction =
                await connection.BeginTransactionAsync();

            try
            {
                foreach (var city in cities)
                {
                    await using var command = new NpgsqlCommand(
                        """
                        INSERT INTO cities
                            ("name", "platecode", "description", "isactive")
                        VALUES
                            (@name, @platecode, @description, @isactive)
                        """,
                        connection,
                        transaction);

                    command.Parameters.AddWithValue("name", city.Name);
                    command.Parameters.AddWithValue("platecode", city.PlateCode);
                    command.Parameters.AddWithValue("description", city.Description);
                    command.Parameters.AddWithValue("isactive", city.IsActive);

                    await command.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();

                return Ok(new
                {
                    message = "Şehirler başarıyla eklendi.",
                    count = cities.Count
                });
            }
            catch (PostgresException ex)
            {
                await transaction.RollbackAsync();

                if (ex.SqlState == "23505")
                {
                    return Conflict(new
                    {
                        message = "Bu plaka kodlarından biri zaten kayıtlı."
                    });
                }

                return StatusCode(500, new
                {
                    message = "Veritabanına kayıt sırasında hata oluştu.",
                    detail = ex.Message,
                    sqlState = ex.SqlState
                });
            }
        }



        // GET: api/cities/34
        [HttpGet("{platecode:int}")]
        public async Task<IActionResult> GetCityByPlateCode(int platecode)
        {
            await using var connection =
                new NpgsqlConnection(connectionString);

            await connection.OpenAsync();

            const string sql = """
                SELECT "name", "platecode", "description", "isactive"
                FROM cities
                WHERE "platecode" = @plateCode
                """;

            await using var command =
                new NpgsqlCommand(sql, connection);

            command.Parameters.AddWithValue("platecode", platecode);

            await using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return NotFound(new
                {
                    message = "Bu plaka koduna ait şehir bulunamadı."
                });
            }

            var city = new
            {
                name = reader.GetString(0),
                plateCode = reader.GetInt32(1),
                description = reader.GetString(2),
                isActive = reader.GetBoolean(3)
            };

            return Ok(city);
        }



        // PUT: api/cities/34
        [HttpPut("{plateCode:int}")]
        public async Task<IActionResult> UpdateCity(
            int platecode,
            [FromBody] CityUpdate city)
        {
            if (city == null)
            {
                return BadRequest(new
                {
                    message = "Güncellenecek şehir bilgileri gönderilmelidir."
                });
            }

            await using var connection =
                new NpgsqlConnection(connectionString);

            await connection.OpenAsync();

            const string sql = """
                UPDATE cities
                SET "name" = @name,
                    "description" = @description,
                    "isactive" = @isactive
                WHERE "platecode" = @plateCode
                """;

            await using var command =
                new NpgsqlCommand(sql, connection);

            command.Parameters.AddWithValue("name", city.Name);
            command.Parameters.AddWithValue("description", city.Description);
            command.Parameters.AddWithValue("isactive", city.IsActive);
            command.Parameters.AddWithValue("platecode", platecode);

            int affectedRows = await command.ExecuteNonQueryAsync();

            if (affectedRows == 0)
            {
                return NotFound(new
                {
                    message = "Bu plaka koduna ait şehir bulunamadı."
                });
            }

            return Ok(new
            {
                message = "Şehir bilgileri başarıyla güncellendi.",
                plateCode = platecode,
                name = city.Name,
                description = city.Description,
                isActive = city.IsActive
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

            await using var connection =
                new NpgsqlConnection(connectionString);

            await connection.OpenAsync();

            const string sql = """
                UPDATE cities
                SET "isactive" = @isActive
                WHERE "platecode" = @plateCode
                """;

            await using var command =
                new NpgsqlCommand(sql, connection);

            command.Parameters.AddWithValue(
                "isactive", request.IsActive);

            command.Parameters.AddWithValue(
                "platecode", plateCode);

            int affectedRows = await command.ExecuteNonQueryAsync();

            if (affectedRows == 0)
            {
                return NotFound(new
                {
                    message = "Bu plaka koduna ait şehir bulunamadı."
                });
            }

            return Ok(new
            {
                message = request.IsActive
                    ? "Şehir aktif duruma getirildi."
                    : "Şehir pasif duruma getirildi.",
                plateCode = plateCode,
                isActive = request.IsActive
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

            await using var connection =
                new NpgsqlConnection(connectionString);

            await connection.OpenAsync();

            const string sql = """
        SELECT "name", "platecode", "description", "isactive"
        FROM cities
        WHERE "description" ILIKE @keyword
        ORDER BY "platecode"
        """;

            await using var command =
                new NpgsqlCommand(sql, connection);

            command.Parameters.AddWithValue("keyword", $"%{keyword}%");

            await using var reader = await command.ExecuteReaderAsync();

            var cities = new List<object>();

            while (await reader.ReadAsync())
            {
                cities.Add(new
                {
                    name = reader.GetString(0),
                    plateCode = reader.GetInt32(1),
                    description = reader.GetString(2),
                    isActive = reader.GetBoolean(3)
                });
            }

            return Ok(cities);
        }

    }

    public class CityInput
    {
        public string Name { get; set; } = string.Empty;
        public int PlateCode { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class CityUpdate
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class CityStatusUpdate
    {
        public bool IsActive { get; set; }
    }
}

