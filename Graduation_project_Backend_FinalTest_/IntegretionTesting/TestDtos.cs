// DTOs used only in tests — not part of the backend API contract.
// These are kept here so integration tests compile while the backend
// evolves its own DTOs independently.

namespace Graduation_Project_Backend.DTOs;

/// <summary>
/// Test-only DTO used in AuthApiTests for a legacy login-or-register endpoint.
/// </summary>
public sealed class LoginOrRegisterDto
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Name { get; set; }
    public Guid MallID { get; set; }
}
