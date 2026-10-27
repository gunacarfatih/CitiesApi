namespace CitiesApi.Models
{
    public class CityInput
    {
        public string Name { get; set; } = string.Empty;
        public int PlateCode { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}