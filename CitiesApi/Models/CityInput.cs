using System.ComponentModel.DataAnnotations;

namespace CitiesApi.Models
{
    public class CityInput
    {
        [Required(ErrorMessage = "Şehir adı zorunludur.")]
        public string Name { get; set; } = string.Empty;

        [Range(1, 81, ErrorMessage = "Plaka kodu 1 ile 81 arasında olmalıdır.")]
        public int PlateCode { get; set; }

        public string Description { get; set; } = string.Empty;

        public bool IsActive { get; set; }
    }
}