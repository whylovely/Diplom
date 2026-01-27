namespace Client.Models
{
    // Расчет процентов на категории
    public sealed class CategoryShareRow
    {
        public string CategoryName { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public decimal SharePercent { get; set; } 
    }
}