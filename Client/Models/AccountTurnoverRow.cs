namespace Client.Models
{
    public sealed class AccountTurnoverRow
    {
        public string AccountName { get; set; } = "";
        public string CurrencyCode { get; set; } = "";

        public decimal Opening { get; set; }   // Остаток на начало
        public decimal DebitTurnOver { get; set; }   
        public decimal CreditTurnOver { get; set; }   
        public decimal NetChange => DebitTurnOver - CreditTurnOver;

        public decimal Closing { get; set; }    // Остаток на конец
    }
}