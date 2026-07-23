namespace BellucSketch.Mobile.Services;

/// <summary>
/// Guarda o token JWT no armazenamento seguro do dispositivo (Keystore no Android via
/// <c>SecureStorage</c> do MAUI Essentials).
/// </summary>
public interface ITokenStore
{
    Task<string?> ObterTokenAsync();
    Task SalvarTokenAsync(string token);
    Task LimparAsync();
}
