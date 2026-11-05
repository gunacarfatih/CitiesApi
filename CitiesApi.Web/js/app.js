const PAGE_SIZE = 10;

let currentPage = 1;
let currentSearchMode = null;
let modalMode = "add";
let editingPlateCode = null;
let toastTimer = null;

// DOM ELEMENTLERİ
const loginView = document.getElementById("loginView");
const dashboardView = document.getElementById("dashboardView");

const loginForm = document.getElementById("loginForm");
const loginButton = document.getElementById("loginButton");
const loginMessage = document.getElementById("loginMessage");

const dashboardPage = document.getElementById("dashboardPage");
const citiesPage = document.getElementById("citiesPage");

const cityModal = document.getElementById("cityModal");
const cityForm = document.getElementById("cityForm");

const cityTableBody = document.getElementById("cityTableBody");
const dashboardTableBody = document.getElementById("dashboardTableBody");

const toast = document.getElementById("toast");

// SAYFA BAŞLANGICI
document.addEventListener("DOMContentLoaded", initializeApp);

async function initializeApp() {
    registerEvents();

    if (localStorage.getItem("token")) {
        await openDashboard();
    } else {
        showLogin();
    }
}

// OLAYLARI TANIMLA
function registerEvents() {
    loginForm.addEventListener("submit", handleLogin);

    document.getElementById("logoutButton")
        .addEventListener("click", handleLogout);

    document.querySelectorAll(".nav-link[data-page]")
        .forEach(link => {
            link.addEventListener("click", event => {
                event.preventDefault();
                navigateTo(link.dataset.page);
            });
        });

    document.getElementById("addCityNav")
        .addEventListener("click", openAddCityModal);

    document.getElementById("dashboardAddButton")
        .addEventListener("click", openAddCityModal);

    document.getElementById("citiesAddButton")
        .addEventListener("click", openAddCityModal);

    document.getElementById("showAllCitiesButton")
        .addEventListener("click", () => navigateTo("cities"));

    document.getElementById("searchButton")
        .addEventListener("click", handleSearch);

    document.getElementById("keywordSearch")
        .addEventListener("keydown", event => {
            if (event.key === "Enter") handleSearch();
        });

    document.getElementById("plateSearch")
        .addEventListener("keydown", event => {
            if (event.key === "Enter") handleSearch();
        });

    document.getElementById("clearSearchButton")
        .addEventListener("click", clearSearch);

    cityForm.addEventListener("submit", handleCitySave);

    document.getElementById("closeModalButton")
        .addEventListener("click", closeCityModal);

    document.getElementById("cancelModalButton")
        .addEventListener("click", closeCityModal);

    cityModal.addEventListener("click", event => {
        if (event.target === cityModal) closeCityModal();
    });

    cityTableBody.addEventListener("click", handleCityTableAction);
}

// GİRİŞ
async function handleLogin(event) {
    event.preventDefault();

    loginButton.disabled = true;
    loginButton.textContent = "Giriş yapılıyor...";
    loginMessage.textContent = "";

    try {
        const username = document.getElementById("username").value.trim();
        const password = document.getElementById("password").value;

        const user = await login(username, password);

        document.getElementById("currentUser").textContent =
            user.username || username;

        showToast("Giriş başarılı.", "success");

        await openDashboard();
    } catch (error) {
        loginMessage.textContent = error.message;
    } finally {
        loginButton.disabled = false;
        loginButton.textContent = "Giriş Yap";
    }
}

// ÇIKIŞ
function handleLogout() {
    logout();
    showLogin();
    showToast("Oturum kapatıldı.", "success");
}

// GİRİŞ EKRANINI GÖSTER
function showLogin() {
    loginView.classList.remove("hidden");
    dashboardView.classList.add("hidden");

    loginForm.reset();
    loginMessage.textContent = "";
    document.getElementById("apiStatus").textContent =
        "● Bağlantı bekleniyor";

    document.getElementById("apiStatus").className = "api-status";
}

// DASHBOARD AÇ
async function openDashboard() {
    loginView.classList.add("hidden");
    dashboardView.classList.remove("hidden");

    navigateTo("dashboard");

    try {
        await Promise.all([
            loadStatistics(),
            loadDashboardPreview()
        ]);

        document.getElementById("apiStatus").textContent =
            "● API bağlantısı başarılı";

        document.getElementById("apiStatus").className =
            "api-status connected";
    } catch (error) {
        handleApiError(error);
    }
}

// SAYFA GEÇİŞLERİ
async function navigateTo(page) {
    const isDashboard = page === "dashboard";

    dashboardPage.classList.toggle("hidden", !isDashboard);
    citiesPage.classList.toggle("hidden", isDashboard);

    document.getElementById("pageTitle").textContent =
        isDashboard ? "Dashboard" : "Şehir Yönetimi";

    document.querySelectorAll(".nav-link").forEach(link => {
        link.classList.toggle(
            "active",
            link.dataset.page === page
        );
    });

    if (isDashboard) {
        await Promise.all([
            loadStatistics(),
            loadDashboardPreview()
        ]).catch(handleApiError);
    } else {
        currentSearchMode = null;
        await loadCityPage(1);
    }
}

// İSTATİSTİKLER
async function loadStatistics() {
    const cities = await getAllCities();

    const total = cities.length;
    const active = cities.filter(city => city.isActive).length;
    const passive = total - active;

    document.getElementById("totalCityCount").textContent = total;
    document.getElementById("activeCityCount").textContent = active;
    document.getElementById("passiveCityCount").textContent = passive;
}

// DASHBOARD TABLOSU
async function loadDashboardPreview() {
    const result = await getCities(1, 5);

    renderCityRows(
        dashboardTableBody,
        result.data || [],
        false
    );
}

// ŞEHİR SAYFASI
async function loadCityPage(page = 1) {
    currentPage = page;

    cityTableBody.innerHTML = "";

    const loadingRow = createMessageRow(
        "Şehirler yükleniyor...",
        5
    );

    cityTableBody.appendChild(loadingRow);

    try {
        const result = await getCities(page, PAGE_SIZE);

        currentSearchMode = null;

        renderCityRows(cityTableBody, result.data || [], true);
        renderPagination(result.pagination);

        document.getElementById("cityListInfo").textContent =
            `Toplam ${result.pagination?.totalCount ?? 0} şehir kaydı`;

    } catch (error) {
        handleApiError(error);
        renderTableMessage(cityTableBody, error.message, 5);
    }
}

// TABLO SATIRLARINI OLUŞTUR
function renderCityRows(tableBody, cities, showActions) {
    tableBody.innerHTML = "";

    if (!cities.length) {
        renderTableMessage(tableBody, "Gösterilecek şehir bulunamadı.", showActions ? 5 : 4);
        return;
    }

    cities.forEach(city => {
        const row = document.createElement("tr");

        addCell(row, city.plateCode);
        addCell(row, city.name);
        addCell(row, city.description || "-");

        const statusCell = document.createElement("td");
        const status = document.createElement("span");

        status.className = city.isActive
            ? "status active-status"
            : "status passive-status";

        status.textContent = city.isActive ? "Aktif" : "Pasif";

        statusCell.appendChild(status);
        row.appendChild(statusCell);

        if (showActions) {
            const actionCell = document.createElement("td");

            actionCell.appendChild(
                createActionButton("Düzenle", "edit", city.plateCode, "edit-button")
            );

            actionCell.appendChild(
                createActionButton(
                    city.isActive ? "Pasif Yap" : "Aktif Yap",
                    "status",
                    city.plateCode,
                    "status-button"
                )
            );

            actionCell.appendChild(
                createActionButton("Sil", "delete", city.plateCode, "delete-button")
            );

            row.appendChild(actionCell);
        }

        tableBody.appendChild(row);
    });
}

function addCell(row, value) {
    const cell = document.createElement("td");
    cell.textContent = value ?? "-";
    row.appendChild(cell);
}

function createActionButton(label, action, plateCode, className) {
    const button = document.createElement("button");

    button.type = "button";
    button.textContent = label;
    button.className = className;
    button.dataset.action = action;
    button.dataset.plateCode = plateCode;

    return button;
}

function createMessageRow(message, columnCount) {
    const row = document.createElement("tr");
    const cell = document.createElement("td");

    cell.colSpan = columnCount;
    cell.className = "empty-message";
    cell.textContent = message;

    row.appendChild(cell);
    return row;
}

function renderTableMessage(tableBody, message, columnCount) {
    tableBody.innerHTML = "";
    tableBody.appendChild(createMessageRow(message, columnCount));
}

// SAYFALAMA
function renderPagination(pagination) {
    const container = document.getElementById("pagination");
    container.innerHTML = "";

    if (!pagination || pagination.totalPages <= 1) {
        return;
    }

    const previous = document.createElement("button");
    previous.textContent = "‹";
    previous.disabled = currentPage <= 1;
    previous.addEventListener("click", () => {
        loadCityPage(currentPage - 1);
    });

    container.appendChild(previous);

    for (let page = 1; page <= pagination.totalPages; page++) {
        const button = document.createElement("button");

        button.textContent = page;

        if (page === currentPage) {
            button.classList.add("active");
        }

        button.addEventListener("click", () => loadCityPage(page));
        container.appendChild(button);
    }

    const next = document.createElement("button");
    next.textContent = "›";
    next.disabled = currentPage >= pagination.totalPages;
    next.addEventListener("click", () => {
        loadCityPage(currentPage + 1);
    });

    container.appendChild(next);
}

// ARAMA
async function handleSearch() {
    const keyword = document.getElementById("keywordSearch").value.trim();
    const plate = document.getElementById("plateSearch").value.trim();

    if (keyword && plate) {
        showToast("Aynı anda yalnızca bir arama türü kullanın.", "error");
        return;
    }

    try {
        if (plate) {
            await searchByPlate(plate);
            return;
        }

        if (keyword) {
            await searchByKeyword(keyword);
            return;
        }

        await loadCityPage(1);
    } catch (error) {
        handleApiError(error);
    }
}

// PLAKA İLE ARAMA
async function searchByPlate(plate) {
    const plateCode = Number(plate);

    if (!Number.isInteger(plateCode) || plateCode < 1 || plateCode > 81) {
        showToast("Plaka kodu 1 ile 81 arasında olmalıdır.", "error");
        return;
    }

    currentSearchMode = "plate";

    const result = await getCityByPlateCode(plateCode);

    renderCityRows(cityTableBody, [result.data], true);
    document.getElementById("pagination").innerHTML = "";

    document.getElementById("cityListInfo").textContent =
        "Plaka kodu ile arama sonucu";
}

// ANAHTAR KELİME İLE ARAMA
async function searchByKeyword(keyword) {
    currentSearchMode = "keyword";

    const result = await searchCities(keyword);

    renderCityRows(cityTableBody, result.data || [], true);
    document.getElementById("pagination").innerHTML = "";

    document.getElementById("cityListInfo").textContent =
        `"${keyword}" için ${result.data?.length ?? 0} sonuç bulundu`;
}

// ARAMAYI TEMİZLE
async function clearSearch() {
    document.getElementById("keywordSearch").value = "";
    document.getElementById("plateSearch").value = "";

    currentSearchMode = null;

    await loadCityPage(1);
}

// TABLO İŞLEMLERİ
async function handleCityTableAction(event) {
    const button = event.target.closest("button[data-action]");

    if (!button) return;

    const plateCode = Number(button.dataset.plateCode);
    const action = button.dataset.action;

    try {
        if (action === "edit") {
            const result = await getCityByPlateCode(plateCode);
            openEditCityModal(result.data);
        }

        if (action === "status") {
            const result = await getCityByPlateCode(plateCode);
            const newStatus = !result.data.isActive;

            await updateCityStatus(plateCode, newStatus);

            showToast("Şehir durumu güncellendi.", "success");
            await refreshCurrentPage();
        }

        if (action === "delete") {
            const confirmed = confirm(
                `${plateCode} plaka kodlu şehri silmek istediğinize emin misiniz?`
            );

            if (!confirmed) return;

            await deleteCity(plateCode);

            showToast("Şehir başarıyla silindi.", "success");
            await refreshCurrentPage();
        }
    } catch (error) {
        handleApiError(error);
    }
}

// YENİ ŞEHİR MODALI
function openAddCityModal() {
    modalMode = "add";
    editingPlateCode = null;

    cityForm.reset();

    document.getElementById("modalTitle").textContent = "Yeni Şehir";
    document.getElementById("cityPlateCode").disabled = false;
    document.getElementById("cityIsActive").checked = true;
    document.getElementById("cityFormMessage").textContent = "";

    cityModal.classList.remove("hidden");
}

// ŞEHİR DÜZENLEME MODALI
function openEditCityModal(city) {
    modalMode = "edit";
    editingPlateCode = city.plateCode;

    document.getElementById("modalTitle").textContent = "Şehir Düzenle";

    document.getElementById("cityPlateCode").value = city.plateCode;
    document.getElementById("cityPlateCode").disabled = true;

    document.getElementById("cityName").value = city.name;
    document.getElementById("cityDescription").value =
        city.description || "";

    document.getElementById("cityIsActive").checked = city.isActive;

    document.getElementById("cityFormMessage").textContent = "";

    cityModal.classList.remove("hidden");
}

// MODALI KAPAT
function closeCityModal() {
    cityModal.classList.add("hidden");
    cityForm.reset();

    document.getElementById("cityPlateCode").disabled = false;
    document.getElementById("cityFormMessage").textContent = "";
}

// ŞEHİR KAYDET
async function handleCitySave(event) {
    event.preventDefault();

    const plateCode = Number(
        document.getElementById("cityPlateCode").value
    );

    const city = {
        plateCode,
        name: document.getElementById("cityName").value.trim(),
        description: document.getElementById("cityDescription").value.trim(),
        isActive: document.getElementById("cityIsActive").checked
    };

    const message = document.getElementById("cityFormMessage");
    const saveButton = document.getElementById("saveCityButton");

    if (!Number.isInteger(plateCode) || plateCode < 1 || plateCode > 81) {
        message.textContent = "Plaka kodu 1 ile 81 arasında olmalıdır.";
        return;
    }

    if (!city.name) {
        message.textContent = "Şehir adı boş bırakılamaz.";
        return;
    }

    saveButton.disabled = true;
    saveButton.textContent = "Kaydediliyor...";
    message.textContent = "";

    try {
        if (modalMode === "add") {
            await addCity(city);
            showToast("Şehir başarıyla eklendi.", "success");
        } else {
            await updateCity(editingPlateCode, city);
            showToast("Şehir başarıyla güncellendi.", "success");
        }

        closeCityModal();

        await refreshCurrentPage();
    } catch (error) {
        message.textContent = error.message;
    } finally {
        saveButton.disabled = false;
        saveButton.textContent = "Kaydet";
    }
}

// İŞLEM SONRASI VERİLERİ YENİLE
async function refreshCurrentPage() {
    await loadStatistics();
    await loadDashboardPreview();

    if (currentSearchMode) {
        await handleSearch();
    } else {
        await loadCityPage(currentPage);
    }
}

// API HATALARI
function handleApiError(error) {
    console.error(error);

    if (error.status === 401) {
        logout();
        showLogin();
        loginMessage.textContent =
            "Oturumunuz sona erdi. Lütfen tekrar giriş yapın.";
        return;
    }

    if (error.status === 0 || error.status === undefined) {
        document.getElementById("apiStatus").textContent =
            "● API bağlantısı başarısız";

        document.getElementById("apiStatus").className =
            "api-status disconnected";
    }

    showToast(error.message || "Bir hata oluştu.", "error");
}

// BİLDİRİM
function showToast(message, type = "success") {
    toast.textContent = message;
    toast.className = `toast show ${type}`;

    clearTimeout(toastTimer);

    toastTimer = setTimeout(() => {
        toast.className = "toast";
    }, 3000);
}
