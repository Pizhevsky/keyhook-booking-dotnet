namespace KeyhookBooking.DTOs.WebSockets;

public sealed record WsMessage<T>(string Type, T Payload);
