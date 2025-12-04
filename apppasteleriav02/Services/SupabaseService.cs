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
        readonly HttpClient _http;
        readonly string _url;
        readonly string _anon;

        public SupabaseService()
        {
            _url = SupabaseConfig.SUPABASE_URL.TrimEnd('/');
            _anon = SupabaseConfig.SUPABASE_ANON_KEY;

            _http = new HttpClient();
            _http.DefaultRequestHeaders.Add("apikey", _anon);
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _anon);
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        // GET /rest/v1/productos?select=*
        public async Task<List<Product>> GetProductsAsync()
        {
            try
            {
                var resp = await _http.GetAsync($"{_url}/rest/v1/productos?select=*");
                resp.EnsureSuccessStatusCode();
                var json = await resp.Content.ReadAsStringAsync();
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var products = JsonSerializer.Deserialize<List<Product>>(json, opts) ?? new List<Product>();

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

        // Crear un pedido
        // POST /rest/v1/pedidos
        public async Task<Order> CreateOrderAsync(Guid userid, List<OrderItem> items)
        {
            var total = 0m;
            foreach (var it in items) total += it.Price * it.Quantity;

            var orderPayload = new { userid = userid, total = total, status = "pendiente" };
            var orderContent = new StringContent(JsonSerializer.Serialize(orderPayload), Encoding.UTF8, "application/json");

            using var orderReq = new HttpRequestMessage(HttpMethod.Post, $"{_url}/rest/v1/pedidos") { Content = orderContent };
            orderReq.Headers.Add("Prefer", "return=representation");

            var resp = await _http.SendAsync(orderReq);
            resp.EnsureSuccessStatusCode();
            var createdOrderJson = await resp.Content.ReadAsStringAsync();

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var created = JsonSerializer.Deserialize<List<Order>>(createdOrderJson, options);

            if (created == null || created.Count == 0)
            {
                throw new Exception("No se pudo crear el pedido.");
            }

            var createdOrder = created[0];

            // Insertar items a pedido_items usando el id del pedido recién creado
            var itemsPayload = new List<object>();
            foreach (var it in items)
            {
                itemsPayload.Add(new
                {
                    pedido_id = createdOrder.Id,
                    producto_id = it.ProductId,
                    cantidad = it.Quantity,
                    precio = it.Price
                });
            }

            var itemsContent = new StringContent(JsonSerializer.Serialize(itemsPayload), Encoding.UTF8, "application/json");
            using var itemsReq = new HttpRequestMessage(HttpMethod.Post, $"{_url}/rest/v1/pedido_items") { Content = itemsContent };
            itemsReq.Headers.Add("Prefer", "return=representation");
            var respItems = await _http.SendAsync(itemsReq);
            respItems.EnsureSuccessStatusCode();

            return createdOrder;
        }

        // Inicio de sesion email/password 
        public async Task<(bool ok, string accessToken, Guid userId, string error)> SignInAsync(string email, string password)
        {
            var payload = new { email = email, password = password, grant_type = "password" };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var resp = await _http.PostAsync($"{_url}/auth/v1/token?grant_type=password", content);
            var js = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode) return (false, null, Guid.Empty, js);

            using var doc = JsonDocument.Parse(js);
            var token = doc.RootElement.GetProperty("access_token").GetString();
            var userId = doc.RootElement.GetProperty("user").GetProperty("id").GetGuid();

            return (true, token, userId, null);
        }

        // Mostrar Perfil
        public async Task<Profile> GetProfileAsync(Guid id)
        {
            var resp = await _http.GetAsync($"{_url}/rest/v1/profiles?id=eq.{id}&select=*");
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var list = JsonSerializer.Deserialize<List<Profile>>(json, options);
            return (list != null && list.Count > 0) ? list[0] : null;
        }
    }
}