using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace apppasteleriav02.Models
{
    public class OrderItem
    {

        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("pedidos_id")]
        public Guid OrderId { get; set; }

        [JsonPropertyName("producto_id")]
        public Guid ProductId { get; set; }

        [JsonPropertyName("cantidad")]
         public int Quantity { get; set; }

        [JsonPropertyName("precio")]
        public decimal Price { get; set; }
    }
}
