using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

Console.OutputEncoding = Encoding.UTF8;

const string apiBaseUrl = "https://localhost:7248";

using HttpClient client = new HttpClient();

Console.WriteLine("========================================");
Console.WriteLine("               CITIES API");
Console.WriteLine("========================================");

// ========================================
// USER LOGIN
// ========================================

Console.Write("\nUsername: ");
string username = Console.ReadLine() ?? "";

Console.Write("Password: ");
string password = "";

ConsoleKeyInfo key;

do
{
    key = Console.ReadKey(intercept: true);

    if (key.Key == ConsoleKey.Backspace)
    {
        if (password.Length > 0)
        {
            password = password.Substring(0, password.Length - 1);
            Console.Write("\b \b");
        }
    }
    else if (!char.IsControl(key.KeyChar))
    {
        password += key.KeyChar;
        Console.Write("*");
    }

} while (key.Key != ConsoleKey.Enter);

Console.WriteLine();

if (string.IsNullOrWhiteSpace(username) ||
    string.IsNullOrWhiteSpace(password))
{
    Console.WriteLine("Kullanıcı adı ve şifre boş olamaz.");
    return;
}

try
{
    // LOGIN REQUEST

    var loginData = new
    {
        username,
        password
    };

    var loginResult = await SendApiRequestAsync(
        HttpMethod.Post,
        $"{apiBaseUrl}/api/auth/login",
        loginData);

    if (!loginResult.IsSuccess)
    {
        Console.WriteLine("Giriş başarısız!");
        return;
    }

    using JsonDocument loginDocument =
        JsonDocument.Parse(loginResult.Body);

    JsonElement loginRoot = loginDocument.RootElement;
    JsonElement loginResponseData =
        loginRoot.GetProperty("data");

    int userId =
        loginResponseData.GetProperty("userId").GetInt32();

    string apiUsername =
        loginResponseData.GetProperty("username").GetString() ?? "";

    string accessToken =
        loginResponseData.GetProperty("accessToken").GetString() ?? "";

    if (string.IsNullOrWhiteSpace(accessToken))
    {
        Console.WriteLine("API geçerli bir token döndürmedi.");
        return;
    }

    // JWT AUTHORIZATION

    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", accessToken);

    Console.WriteLine("\nGiriş başarılı!");
    Console.WriteLine($"Hoş geldin, {apiUsername}.");

    // PAGINATION SETTINGS

    int page = 1;
    int pageSize = 10;
    int totalPages = 0;

    // ========================================
    // MAIN MENU
    // ========================================

    bool isRunning = true;

    while (isRunning)
    {
        Console.WriteLine("\n========================================");
        Console.WriteLine("              CITIES API MENU");
        Console.WriteLine("========================================");

        Console.WriteLine("\n         ŞEHİR İŞLEMLERİ");
        Console.WriteLine("----------------------------------------");
        Console.WriteLine("1  - Şehirleri listele");
        Console.WriteLine("2  - Şehir ara");
        Console.WriteLine("3  - Şehir detayını görüntüle");
        Console.WriteLine("4  - Tek şehir ekle");
        Console.WriteLine("5  - Şehir bilgilerini güncelle");
        Console.WriteLine("6  - Aktif / Pasif durumunu değiştir");
        Console.WriteLine("7  - Şehir sil");
        Console.WriteLine("8  - Cache üzerinden şehir getir");

        Console.WriteLine("\n         SAYFALAMA");
        Console.WriteLine("----------------------------------------");
        Console.WriteLine("9  - Sayfa ayarlarını değiştir");
        Console.WriteLine("10 - Sonraki sayfa");
        Console.WriteLine("11 - Önceki sayfa");

        Console.WriteLine("\n         HESAP İŞLEMLERİ");
        Console.WriteLine("----------------------------------------");
        Console.WriteLine("12 - Kullanıcı bilgileri");
        Console.WriteLine("13 - Token bilgileri");
        Console.WriteLine("14 - Çıkış yap");

        Console.WriteLine("\n========================================");

        Console.Write("Seçiminiz: ");
        string choice = Console.ReadLine() ?? "";

        switch (choice)
        {
            case "1":
                totalPages = await GetCitiesAsync(
                    client, apiBaseUrl, page, pageSize);
                break;

            case "2":
                await SearchCityAsync(client, apiBaseUrl);
                break;

            case "3":
                await GetCityByPlateCodeAsync(client, apiBaseUrl);
                break;

            case "4":
                await AddCityAsync(client, apiBaseUrl);
                break;

            case "5":
                await UpdateCityAsync(client, apiBaseUrl);
                break;

            case "6":
                await ChangeCityStatusAsync(client, apiBaseUrl);
                break;

            case "7":
                await DeleteCityAsync(client, apiBaseUrl);
                break;

            case "8":
                await GetCityFromCacheAsync(client, apiBaseUrl);
                break;

            case "9":
                Console.Write("\nYeni sayfa numarası: ");

                if (!int.TryParse(Console.ReadLine(), out int newPage)
                    || newPage < 1)
                {
                    Console.WriteLine("Geçersiz sayfa numarası!");
                    break;
                }

                Console.Write("Sayfa başına şehir sayısı (1-100): ");

                if (!int.TryParse(Console.ReadLine(), out int newPageSize)
                    || newPageSize < 1
                    || newPageSize > 100)
                {
                    Console.WriteLine("Sayfa boyutu 1 ile 100 arasında olmalıdır!");
                    break;
                }

                page = newPage;
                pageSize = newPageSize;

                Console.WriteLine("\nSayfa ayarları güncellendi.");

                totalPages = await GetCitiesAsync(
                    client, apiBaseUrl, page, pageSize);
                break;

            case "10":
                if (totalPages == 0)
                {
                    totalPages = await GetCitiesAsync(
                        client, apiBaseUrl, page, pageSize);
                    break;
                }

                if (page >= totalPages)
                {
                    Console.WriteLine("\nSon sayfadasınız.");
                    break;
                }

                page++;

                totalPages = await GetCitiesAsync(
                    client, apiBaseUrl, page, pageSize);
                break;

            case "11":
                if (page <= 1)
                {
                    Console.WriteLine("\nİlk sayfadasınız.");
                    break;
                }

                page--;

                totalPages = await GetCitiesAsync(
                    client, apiBaseUrl, page, pageSize);
                break;

            case "12":
                Console.WriteLine("\n===== USER INFORMATION =====");
                Console.WriteLine($"User ID  : {userId}");
                Console.WriteLine($"Username : {apiUsername}");
                break;

            case "13":
                ShowTokenInformation(accessToken);
                break;

            case "14":
                Console.WriteLine("\nÇıkış yapılıyor...");
                isRunning = false;
                break;

            default:
                Console.WriteLine("\nGeçersiz seçim! Tekrar deneyin.");
                break;
        }
        if (isRunning)
        {
            Console.WriteLine("\nAna menüye dönmek için bir tuşa basın...");
            Console.ReadKey(true);
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine("\nBir hata oluştu: " + ex.Message);
}

Console.WriteLine("\nUygulama sonlandırıldı.");


// ========================================
// COMMON API REQUEST METHOD
// ========================================

async Task<(bool IsSuccess, string Body)> SendApiRequestAsync(
    HttpMethod method,
    string url,
    object? requestData = null)
{
    using HttpRequestMessage request =
        new HttpRequestMessage(method, url);

    if (requestData != null)
    {
        string json = JsonSerializer.Serialize(requestData);

        request.Content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");
    }

     using HttpResponseMessage response =
        await client.SendAsync(request);

    string responseBody =
        await response.Content.ReadAsStringAsync();

    Console.WriteLine(
        $"\nHTTP STATUS CODE: {(int)response.StatusCode} ({response.StatusCode})");

    try
    {
        using JsonDocument document =
            JsonDocument.Parse(responseBody);

        JsonElement root = document.RootElement;

        if (root.TryGetProperty("message", out JsonElement message))
        {
            Console.WriteLine("API MESSAGE: " +
                (message.GetString() ?? ""));
        }
    }
    catch (JsonException)
    {
        // JSON olmayan yanıtlar için gövdeyi aşağıda göstereceğiz.
    }

    if (!response.IsSuccessStatusCode)
    {
        Console.WriteLine("İşlem başarısız!");
        Console.WriteLine(responseBody);
    }

    return (response.IsSuccessStatusCode, responseBody);
}


// ========================================
// GET CITIES
// ========================================

async Task<int> GetCitiesAsync(
    HttpClient httpClient,
    string baseUrl,
    int pageNumber,
    int pageSizeValue)
{
    string url =
        $"{baseUrl}/api/cities?page={pageNumber}&pageSize={pageSizeValue}";

    var result = await SendApiRequestAsync(HttpMethod.Get, url);

    if (!result.IsSuccess)
        return 0;

    using JsonDocument document =
        JsonDocument.Parse(result.Body);

    JsonElement root = document.RootElement;
    JsonElement cities = root.GetProperty("data");
    JsonElement pagination = root.GetProperty("pagination");

    Console.WriteLine("\n================ CITIES ================\n");

    Console.WriteLine(
        "{0,-8} {1,-20} {2,-90} {3,-10}",
        "PLAKA", "ŞEHİR", "AÇIKLAMA", "DURUM");

    Console.WriteLine(new string('-', 135));

    foreach (JsonElement city in cities.EnumerateArray())
    {
        int plateCode =
            city.GetProperty("plateCode").GetInt32();

        string name =
            city.GetProperty("name").GetString() ?? "";

        string description =
            city.GetProperty("description").GetString() ?? "";

        bool isActive =
            city.GetProperty("isActive").GetBoolean();

        Console.WriteLine(
            "{0,-8} {1,-20} {2,-90} {3,-10}",
            plateCode,
            name,
            description,
            isActive ? "Aktif" : "Pasif");
    }

    Console.WriteLine(new string('-', 135));

    int currentPage =
        pagination.GetProperty("page").GetInt32();

    int currentPageSize =
        pagination.GetProperty("pageSize").GetInt32();

    int totalCount =
        pagination.GetProperty("totalCount").GetInt32();

    int pages =
        pagination.GetProperty("totalPages").GetInt32();

    Console.WriteLine($"Sayfa          : {currentPage}");
    Console.WriteLine($"Sayfa Boyutu   : {currentPageSize}");
    Console.WriteLine($"Toplam Kayıt   : {totalCount}");
    Console.WriteLine($"Toplam Sayfa   : {pages}");

    return pages;
}


// ========================================
// SEARCH CITY
// ========================================

async Task SearchCityAsync(
    HttpClient httpClient,
    string baseUrl)
{
    Console.Write("\nAranacak kelime veya plaka kodu: ");
    string keyword = Console.ReadLine() ?? "";

    if (string.IsNullOrWhiteSpace(keyword))
    {
        Console.WriteLine("Arama değeri boş olamaz.");
        return;
    }

    string url;

    // Girilen değer integer ise plaka koduyla arama yap.
    if (int.TryParse(keyword, out int plateCode))
    {
        url = $"{baseUrl}/api/cities/{plateCode}";
    }
    else
    {
        // Integer değilse metin tabanlı arama yap.
        url = $"{baseUrl}/api/cities/search?keyword={Uri.EscapeDataString(keyword)}";
    }

    var result = await SendApiRequestAsync(HttpMethod.Get, url);

    if (!result.IsSuccess)
        return;

    using JsonDocument document =
        JsonDocument.Parse(result.Body);

    JsonElement data =
        document.RootElement.GetProperty("data");

    // Plaka koduyla aramada tek bir şehir döner.
    if (int.TryParse(keyword, out _))
    {
        Console.WriteLine("\n===== CITY RESULT =====");
        PrintCity(data);
        return;
    }

    // Metin tabanlı aramada şehir listesi döner.
    if (data.GetArrayLength() == 0)
    {
        Console.WriteLine("Arama sonucunda şehir bulunamadı.");
        return;
    }

    Console.WriteLine("\n===== SEARCH RESULTS =====");

    foreach (JsonElement city in data.EnumerateArray())
    {
        PrintCity(city);
    }
}


// ========================================
// GET CITY BY PLATE CODE
// ========================================

async Task GetCityByPlateCodeAsync(
    HttpClient httpClient,
    string baseUrl)
{
    int plateCode = ReadPlateCode();

    if (plateCode == -1)
        return;

    string url = $"{baseUrl}/api/cities/{plateCode}";

    var result = await SendApiRequestAsync(HttpMethod.Get, url);

    if (!result.IsSuccess)
        return;

    using JsonDocument document =
        JsonDocument.Parse(result.Body);

    JsonElement city =
        document.RootElement.GetProperty("data");

    PrintCity(city);
}


// ========================================
// ADD SINGLE CITY USING BULK ENDPOINT
// ========================================

async Task AddCityAsync(
    HttpClient httpClient,
    string baseUrl)
{
    Console.WriteLine("\n===== ADD NEW CITY =====");

    Console.Write("Şehir adı: ");
    string name = Console.ReadLine() ?? "";

    if (string.IsNullOrWhiteSpace(name))
    {
        Console.WriteLine("Şehir adı boş olamaz.");
        return;
    }

    Console.Write("Plaka kodu: ");

    if (!int.TryParse(Console.ReadLine(), out int plateCode)
        || plateCode < 1)
    {
        Console.WriteLine("Geçerli bir plaka kodu girin.");
        return;
    }

    Console.Write("Açıklama: ");
    string description = Console.ReadLine() ?? "";

    Console.Write("Aktif mi? (E/H): ");
    string statusInput = Console.ReadLine() ?? "";

    if (!statusInput.Equals("E", StringComparison.OrdinalIgnoreCase)
        && !statusInput.Equals("H", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("E veya H girmelisiniz.");
        return;
    }

    bool isActive =
        statusInput.Equals("E", StringComparison.OrdinalIgnoreCase);

    var cityList = new[]
    {
        new
        {
            name,
            plateCode,
            description,
            isActive
        }
    };

    string url = $"{baseUrl}/api/cities/bulk";

    var result = await SendApiRequestAsync(
        HttpMethod.Post,
        url,
        cityList);

    if (result.IsSuccess)
        Console.WriteLine("\nŞehir ekleme işlemi tamamlandı.");
}


// ========================================
// UPDATE CITY
// ========================================

async Task UpdateCityAsync(
    HttpClient httpClient,
    string baseUrl)
{
    Console.WriteLine("\n===== UPDATE CITY =====");

    int plateCode = ReadPlateCode();

    if (plateCode == -1)
        return;

    // Önce mevcut şehri getiriyoruz.

    var getResult = await SendApiRequestAsync(
        HttpMethod.Get,
        $"{baseUrl}/api/cities/{plateCode}");

    if (!getResult.IsSuccess)
        return;

    using JsonDocument document =
        JsonDocument.Parse(getResult.Body);

    JsonElement city =
        document.RootElement.GetProperty("data");

    string currentName =
        city.GetProperty("name").GetString() ?? "";

    string currentDescription =
        city.GetProperty("description").GetString() ?? "";

    bool currentStatus =
        city.GetProperty("isActive").GetBoolean();

    Console.WriteLine("\nMevcut şehir bilgileri:");
    PrintCity(city);

    Console.WriteLine("\nYeni bilgileri girin:");

    Console.Write($"Şehir adı ({currentName}): ");
    string name = Console.ReadLine() ?? "";

    if (string.IsNullOrWhiteSpace(name))
        name = currentName;

    Console.Write($"Açıklama ({currentDescription}): ");
    string description = Console.ReadLine() ?? "";

    if (string.IsNullOrWhiteSpace(description))
        description = currentDescription;

    Console.Write($"Aktif mi? (E/H, mevcut: {(currentStatus ? "E" : "H")}): ");
    string statusInput = Console.ReadLine() ?? "";

    bool isActive = currentStatus;

    if (statusInput.Equals("E", StringComparison.OrdinalIgnoreCase))
        isActive = true;
    else if (statusInput.Equals("H", StringComparison.OrdinalIgnoreCase))
        isActive = false;

    Console.Write("\nGüncellemek istediğinize emin misiniz? (E/H): ");

    if (!ConfirmAction())
        return;

    var updateData = new
    {
        name,
        description,
        isActive
    };

    var result = await SendApiRequestAsync(
        HttpMethod.Put,
        $"{baseUrl}/api/cities/{plateCode}",
        updateData);

    if (result.IsSuccess)
        Console.WriteLine("\nŞehir güncellendi.");
}


// ========================================
// CHANGE CITY STATUS
// ========================================

async Task ChangeCityStatusAsync(
    HttpClient httpClient,
    string baseUrl)
{
    Console.WriteLine("\n===== CHANGE CITY STATUS =====");

    int plateCode = ReadPlateCode();

    if (plateCode == -1)
        return;

    Console.Write("Yeni durum (E = Aktif, H = Pasif): ");
    string input = Console.ReadLine() ?? "";

    if (!input.Equals("E", StringComparison.OrdinalIgnoreCase)
        && !input.Equals("H", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("E veya H girmelisiniz.");
        return;
    }

    bool isActive =
        input.Equals("E", StringComparison.OrdinalIgnoreCase);

    var statusData = new
    {
        isActive
    };

    var result = await SendApiRequestAsync(
        HttpMethod.Patch,
        $"{baseUrl}/api/cities/{plateCode}/status",
        statusData);

    if (result.IsSuccess)
        Console.WriteLine("\nŞehir durumu değiştirildi.");
}


// ========================================
// DELETE CITY
// ========================================

async Task DeleteCityAsync(
    HttpClient httpClient,
    string baseUrl)
{
    Console.WriteLine("\n===== DELETE CITY =====");

    int plateCode = ReadPlateCode();

    if (plateCode == -1)
        return;

    Console.WriteLine(
        $"\n{plateCode} plakalı şehri silmek istediğinize emin misiniz?");

    Console.Write("Onay (E/H): ");

    if (!ConfirmAction())
    {
        Console.WriteLine("Silme işlemi iptal edildi.");
        return;
    }

    var result = await SendApiRequestAsync(
        HttpMethod.Delete,
        $"{baseUrl}/api/cities/{plateCode}");

    if (result.IsSuccess)
        Console.WriteLine("\nŞehir silindi.");
}


// ========================================
// GET CITY FROM CACHE
// ========================================

async Task GetCityFromCacheAsync(
    HttpClient httpClient,
    string baseUrl)
{
    Console.WriteLine("\n===== GET CITY FROM CACHE =====");

    int plateCode = ReadPlateCode();

    if (plateCode == -1)
        return;

    string url =
        $"{baseUrl}/api/cities/cache/{plateCode}";

    var result = await SendApiRequestAsync(
        HttpMethod.Get,
        url);

    if (!result.IsSuccess)
        return;

    using JsonDocument document =
        JsonDocument.Parse(result.Body);

    JsonElement city =
        document.RootElement.GetProperty("data");

    PrintCity(city);
}


// ========================================
// USER TOKEN INFORMATION
// ========================================

void ShowTokenInformation(string token)
{
    Console.WriteLine("\n===== TOKEN INFORMATION =====");

    Console.WriteLine($"Token uzunluğu: {token.Length} karakter");
    Console.WriteLine($"Token önizleme: {token.Substring(0, Math.Min(20, token.Length))}...");

    try
    {
        string[] parts = token.Split('.');

        if (parts.Length != 3)
        {
            Console.WriteLine("Token JWT formatında değil.");
            return;
        }

        string payload = parts[1]
            .Replace('-', '+')
            .Replace('_', '/');

        switch (payload.Length % 4)
        {
            case 2:
                payload += "==";
                break;
            case 3:
                payload += "=";
                break;
        }

        byte[] bytes = Convert.FromBase64String(payload);

        string json = Encoding.UTF8.GetString(bytes);

        using JsonDocument document = JsonDocument.Parse(json);

        JsonElement root = document.RootElement;

        Console.WriteLine("\nToken Claims:");

        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (property.Name == "exp" &&
                property.Value.TryGetInt64(out long exp))
            {
                DateTimeOffset expiry =
                    DateTimeOffset.FromUnixTimeSeconds(exp);

                Console.WriteLine($"Bitiş zamanı (UTC): {expiry.UtcDateTime}");
                Console.WriteLine($"Bitiş zamanı (Yerel): {expiry.ToLocalTime()}");
            }
            else
            {
                Console.WriteLine($"{property.Name}: {property.Value}");
            }
        }

        Console.WriteLine(
            "\nNot: Bu işlem token imzasını doğrulamaz; yalnızca payload içeriğini okur.");
    }
    catch
    {
        Console.WriteLine("Token bilgileri okunamadı.");
    }
}


// ========================================
// HELPER METHODS
// ========================================

int ReadPlateCode()
{
    Console.Write("\nPlaka kodu: ");

    if (!int.TryParse(Console.ReadLine(), out int plateCode)
        || plateCode < 1)
    {
        Console.WriteLine("Geçersiz plaka kodu.");
        return -1;
    }

    return plateCode;
}


bool ConfirmAction()
{
    string answer = Console.ReadLine() ?? "";

    return answer.Equals(
        "E",
        StringComparison.OrdinalIgnoreCase);
}


void PrintCity(JsonElement city)
{
    int plateCode =
        city.GetProperty("plateCode").GetInt32();

    string name =
        city.GetProperty("name").GetString() ?? "";

    string description =
        city.GetProperty("description").GetString() ?? "";

    bool isActive =
        city.GetProperty("isActive").GetBoolean();

    Console.WriteLine("----------------------------------------");
    Console.WriteLine($"Plaka       : {plateCode}");
    Console.WriteLine($"Şehir       : {name}");
    Console.WriteLine($"Açıklama    : {description}");
    Console.WriteLine($"Durum       : {(isActive ? "Aktif" : "Pasif")}");
    Console.WriteLine("----------------------------------------");
}
