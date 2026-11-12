namespace RestaurantBillSplitter.Core.Models;

public sealed record Bill(decimal Subtotal, decimal Tax, TipMode TipMode, decimal TipInput);
