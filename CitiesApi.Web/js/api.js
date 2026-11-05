
const API_BASE_URL = "https://localhost:7248/api";

// Genel API istek fonksiyonu
async function apiRequest(endpoint, options = {}) {
    const token = localStorage.getItem("token");

    const headers = {
        ...(options.body ? { "Content-Type": "application/json" } : {}),
        ...(token ? { "Authorization": `Bearer ${token}` } : {}),
        ...options.headers
    };

    let response;

    try {
        response = await fetch(`${API_BASE_URL}${endpoint}`, {
            ...options,
            headers
        });
    } catch {
        throw new Error("API sunucusuna bağlanılamadı. Backend çalışıyor mu?");
    }

    const text = await response.text();

    let result = null;

    if (text) {
        try {
            result = JSON.parse(text);
        } catch {
            result = { message: text };
        }
    }

    if (!response.ok || result?.success === false) {
        const error = new Error(
            result?.message || `İstek başarısız. HTTP ${response.status}`
        );

        error.status = response.status;
        error.details = result;
        throw error;
    }

    return result;
}

// GİRİŞ
async function login(username, password) {
    const result = await apiRequest("/auth/login", {
        method: "POST",
        body: JSON.stringify({
            userName: username,
            password: password
        })
    });

    localStorage.setItem("token", result.data.accessToken);

    return result.data;
}

// ÇIKIŞ
function logout() {
    localStorage.removeItem("token");
}

// ŞEHİRLERİ SAYFALI GETİR
async function getCities(page = 1, pageSize = 10) {
    return await apiRequest(
        `/cities?page=${page}&pageSize=${pageSize}`
    );
}

// TÜM ŞEHİRLERİ GETİR
async function getAllCities() {
    const firstResult = await getCities(1, 100);
    const cities = [...(firstResult.data || [])];

    const totalPages = firstResult.pagination?.totalPages || 1;

    for (let page = 2; page <= totalPages; page++) {
        const result = await getCities(page, 100);
        cities.push(...(result.data || []));
    }

    return cities;
}

// PLAKA KODUYLA ŞEHİR GETİR
async function getCityByPlateCode(plateCode) {
    return await apiRequest(`/cities/${plateCode}`);
}

// ŞEHİR ARAMA
async function searchCities(keyword) {
    return await apiRequest(
        `/cities/search?keyword=${encodeURIComponent(keyword)}`
    );
}

// ŞEHİR EKLE
// Backend'deki bulk endpoint tek elemanlı liste kabul ediyor.
async function addCity(city) {
    return await apiRequest("/cities/bulk", {
        method: "POST",
        body: JSON.stringify([city])
    });
}

// ŞEHİR GÜNCELLE
async function updateCity(plateCode, city) {
    return await apiRequest(`/cities/${plateCode}`, {
        method: "PUT",
        body: JSON.stringify({
            name: city.name,
            description: city.description,
            isActive: city.isActive
        })
    });
}

// AKTİFLİK DURUMU GÜNCELLE
async function updateCityStatus(plateCode, isActive) {
    return await apiRequest(`/cities/${plateCode}/status`, {
        method: "PATCH",
        body: JSON.stringify({ isActive })
    });
}

// ŞEHİR SİL
async function deleteCity(plateCode) {
    return await apiRequest(`/cities/${plateCode}`, {
        method: "DELETE"
    });
}
