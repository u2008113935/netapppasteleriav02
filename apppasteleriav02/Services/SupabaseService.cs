using apppasteleriav02.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace apppasteleriav02.Services
{
    public class SupabaseService
    {
        public static SupabaseService Instance { get; } = new SupabaseService();

        readonly HttpClient _http;
        readonly string _url;
        readonly string _anon;
        readonly JsonSerializerOptions _jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        public SupabaseService()
        {
            _url = SupabaseConfig.SUPABASE_URL.TrimEnd('/');
            _anon = SupabaseConfig.SUPABASE_ANON_KEY;

            // Crear HttpClient una sola vez (singleton)
            _http = new HttpClient();

            // Siempre enviar apikey (anon) en header; no ponemos Authorization por defecto.
            // Authorization se establecerá con SetUserToken cuando haya un token de usuario.
            if (!string.IsNullOrWhiteSpace(_anon))
                _http.DefaultRequestHeaders.Add("apikey", _anon);

            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Establece o elimina el Authorization header del HttpClient.
        /// Si token es null o vacío, se elimina el header (quedará la anon key en apikey).
        /// </summary>
        public void SetUserToken(string? token)
        {
            // Elimina header Authorization si existe
            _http.DefaultRequestHeaders.Authorization = null;

            if (!string.IsNullOrWhiteSpace(token))
            {
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                Debug.WriteLine("SupabaseService: Authorization header set for user token.");
            }
            else
            {
                Debug.WriteLine("SupabaseService: Authorization header cleared (no user token).");
            }
        }

        // GET /rest/v1/productos?select=*
        public async Task<List<Product>> GetProductsAsync()
        {
            try
            {
                var resp = await _http.GetAsync($"{_url}/rest/v1/productos?select=*");
                var json = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"GetProductsAsync failed: {resp.StatusCode} - {json}");
                    return new List<Product>();
                }

                var products = JsonSerializer.Deserialize<List<Product>>(json, _jsonOpts) ?? new List<Product>();

                // Normaliza las rutas/URLs de imagen (detectar URL absoluta vs filename)
                NormalizeProductImages(products);

                return products;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SupabaseService.GetProductsAsync error: " + ex.Message);
                return new List<Product>();
            }
        }

        // Normaliza el campo ImagenPath de los productos.
        void NormalizeProductImages(IEnumerable<Product>? products)
        {
            if (products == null) return;
            foreach (var p in products)
            {
                var raw = p.ImagenPath;
                if (string.IsNullOrWhiteSpace(raw))
                {
                    p.ImagenPath = null;
                    Debug.WriteLine($"Product '{p.Nombre}' image normalized -> (null)");
                    continue;
                }

                var decoded = Uri.UnescapeDataString(raw).Trim();

                if (Uri.TryCreate(decoded, UriKind.Absolute, out var absolute))
                {
                    // Si ya es URL absoluta (signed o pública), úsala
                    p.ImagenPath = absolute.ToString();
                }
                else
                {
                    // Si no, asumir filename/relative path y construir URL pública
                    var fileName = decoded.TrimStart('/');
                    p.ImagenPath = $"{_url}/storage/v1/object/public/{SupabaseConfig.BUCKET_NAME}/{Uri.EscapeDataString(fileName)}";
                }

                Debug.WriteLine($"Product '{p.Nombre}' image normalized -> {p.ImagenPath}");
            }
        }

        // Crear un pedido (ajusta nombres de campos según tu esquema en Supabase)
        // POST /rest/v1/pedidos
        public async Task<Order> CreateOrderAsync(Guid userid, List<OrderItem> items)
        {
            // 1) calcular total en base a items
            var total = 0m;
            foreach (var it in items) total += it.Price * it.Quantity;

            // 2) construir payload del pedido
            // OBSERVACIÓN: usar user_id (snake_case) es lo común en Postgres/Supabase.
            var orderPayload = new
            {
                user_id = userid,
                total = total,
                status = "pendiente",
                created_at = DateTime.UtcNow
            };

            var orderContent = new StringContent(JsonSerializer.Serialize(orderPayload), Encoding.UTF8, "application/json");

            using var orderReq = new HttpRequestMessage(HttpMethod.Post, $"{_url}/rest/v1/pedidos") { Content = orderContent };
            orderReq.Headers.Add("Prefer", "return=representation");

            // 3) enviar pedido
            var resp = await _http.SendAsync(orderReq);
            var createdOrderJson = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                Debug.WriteLine($"CreateOrderAsync (order) failed: {resp.StatusCode} - {createdOrderJson}");
                throw new Exception($"CreateOrderAsync (order) failed: {createdOrderJson}");
            }

            var created = JsonSerializer.Deserialize<List<Order>>(createdOrderJson, _jsonOpts);
            if (created == null || created.Count == 0)
            {
                throw new Exception("No se pudo crear el pedido (respuesta vacía).");
            }
            var createdOrder = created[0];

            // 4) preparar payload de items
            // IMPORTANTE: la clave de la FK debe coincidir con tu columna en la tabla (ej.: pedidos_id o pedido_id).
            // En este ejemplo uso 'pedidos_id' (ajusta si tu tabla usa 'pedido_id').
            var itemsPayload = new List<object>();
            foreach (var it in items)
            {
                itemsPayload.Add(new
                {
                    pedidos_id = createdOrder.Id,   // <-- verificar que esta sea la columna FK correcta en tu tabla de items
                    producto_id = it.ProductId,
                    cantidad = it.Quantity,
                    precio = it.Price
                });
            }

            var itemsContent = new StringContent(JsonSerializer.Serialize(itemsPayload), Encoding.UTF8, "application/json");
            using var itemsReq = new HttpRequestMessage(HttpMethod.Post, $"{_url}/rest/v1/pedido_items") { Content = itemsContent };
            itemsReq.Headers.Add("Prefer", "return=representation");

            var respItems = await _http.SendAsync(itemsReq);
            var respItemsBody = await respItems.Content.ReadAsStringAsync();

            if (!respItems.IsSuccessStatusCode)
            {
                Debug.WriteLine($"CreateOrderAsync (items) failed: {respItems.StatusCode} - {respItemsBody}");
                throw new Exception($"CreateOrderAsync (items) failed: {respItemsBody}");
            }

            // 5) retornar el pedido creado
            return createdOrder;
        }

        // Inicio de sesion email/password 
        public async Task<(bool Success, string? AccessToken, string? RefreshToken, Guid? UserId, string? Error)> SignInAsync(string email, string password)
        {
            try
            {
                var payload = new { email = email, password = password };
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var resp = await _http.PostAsync($"{_url}/auth/v1/token?grant_type=password", content);
                var js = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode) return (false, null, null, null, js);

                using var doc = JsonDocument.Parse(js);
                var root = doc.RootElement;

                var accessToken = root.GetProperty("access_token").GetString();
                var refreshToken = root.TryGetProperty("refresh_token", out var r) ? r.GetString() : null;

                var userIdStr = root.GetProperty("user").GetProperty("id").GetString();

                Guid? userId = null;
                if (!string.IsNullOrWhiteSpace(userIdStr) && Guid.TryParse(userIdStr, out var guid)) userId = guid;

                return (true, accessToken, refreshToken, userId, null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SignInAsync error: {ex.Message}");
                return (false, null, null, null, ex.Message);
            }
        }

        // Mostrar Perfil
        public async Task<Profile> GetProfileAsync(Guid id)
        {
            var resp = await _http.GetAsync($"{_url}/rest/v1/profiles?id=eq.{id}&select=*");
            var json = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                Debug.WriteLine($"GetProfileAsync failed: {resp.StatusCode} - {json}");
                throw new Exception($"GetProfileAsync failed: {json}");
            }

            var list = JsonSerializer.Deserialize<List<Profile>>(json, _jsonOpts);
            return (list != null && list.Count > 0) ? list[0] : null;
        }
    }
}