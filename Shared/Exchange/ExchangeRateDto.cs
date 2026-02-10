using System;

namespace Shared.Exchange;

public sealed record ExchangeRateDto(
    string Currency, 
    decimal Rate,    
    DateTimeOffset Date
);
