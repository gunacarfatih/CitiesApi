using System.ComponentModel.DataAnnotations;

namespace CitiesApi.Models
{
    public class City
    {
        [Required(ErrorMessage = "Şehir adı zorunludur.")]
        public string Name { get; set; } = string.Empty;

        public int PlateCode { get; set; }

        public string Description { get; set; } = string.Empty;

        public bool IsActive { get; set; }
    }
}