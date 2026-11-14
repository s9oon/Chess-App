namespace backend.DTO;

// Returned by POST /Session. Orval turns this into: interface SessionResponse { id: string }
public record SessionResponse(string Id);
