using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace apppasteleriav02.Services
{
    // Servicio de autenticación mínimo. Reemplazar las llamadas de ejemplo
    // por llamadas reales a Supabase SDK/REST.
    public class AuthService
    {
        public static AuthService Instance { get; } = new AuthService();

        const string TokenKey = "auth_token";
        const string UserIdKey = "auth_user_id";

        public string? AccessToken { get; private set; }
        public string? UserId { get; private set; }

        public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken);

        private AuthService()
        {
        }

        // Método de ejemplo: reemplazar con SupabaseAuth o tu API
        public async Task<bool> SignInAsync(string email, string password)
        {
            // TODO: realizar llamada real a Supabase Auth aquí.
            // Este ejemplo simula login exitoso para que la app pueda avanzar.
            // Reemplaza por:
            // var result = await SupabaseService.Instance.SignIn(email, password);
            // if (result.Success) { AccessToken = result.Token; UserId = result.UserId; ... }

            // Simulación mínima (para desarrollo): almacenar email como userId y token simulado
            AccessToken = "dev-token-simulado";
            UserId = email?.ToLowerInvariant() ?? "devuser";
            await SecureStorage.Default.SetAsync(TokenKey, AccessToken);
            await SecureStorage.Default.SetAsync(UserIdKey, UserId);
            return true;
        }

        public async Task LoadFromStorageAsync()
        {
            try
            {
                AccessToken = await SecureStorage.Default.GetAsync(TokenKey);
                UserId = await SecureStorage.Default.GetAsync(UserIdKey);
            }
            catch
            {
                // no available or permission denied
            }
        }

        // Simulación mínima de registro. Reemplaza con SupabaseService.SignUp y manejo real.
        public async Task<bool> SignUpAsync(string email, string password, string name, string? phone = null)
        {
            // TODO: Llamar SupabaseService.Instance.SignUp(...) y tratar respuesta
            // En esta versión de desarrollo devolvemos true para permitir flujo.
            // Guarda token/usuario si la API retorna directamente el token.
            await Task.Delay(300);
            return true;
        }

        public async Task SignOutAsync()
        {
            AccessToken = null;
            UserId = null;
            SecureStorage.Default.Remove(TokenKey);
            SecureStorage.Default.Remove(UserIdKey);
            await Task.CompletedTask;
        }
    }
}