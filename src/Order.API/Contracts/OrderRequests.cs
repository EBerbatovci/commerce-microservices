using Order.API.Models;

namespace Order.API.Contracts;

public sealed record CreateOrderRequest(
    string CustomerEmail,
    IReadOnlyCollection<CreateOrderItemRequest> Items);

public sealed record CreateOrderItemRequest(
    Guid ProductId,
    int Quantity);

public sealed record UpdateOrderStatusRequest(OrderStatus Status);
