using apppasteleriav02.Models;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace apppasteleriav02.Services
{
    // Mantener el carrito de compras en memoria
    public class CartService : INotifyPropertyChanged
    {
        public static CartService Instance { get; } = new CartService();

        public ObservableCollection<CartItem> Items { get; } = new ObservableCollection<CartItem>();

        public event PropertyChangedEventHandler? PropertyChanged;

        // Total del carrito (suma precio * cantidad)
        public decimal Total => Items.Sum(i => i.Price * i.Quantity);

        public int Count => Items.Sum(i => i.Quantity);

        private CartService()
        {
            Items.CollectionChanged += Items_CollectionChanged;
        }

        private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Suscribir a PropertyChanged de los nuevos items para detectar cambios en Quantity
            if (e.NewItems != null)
            {
                foreach (var ni in e.NewItems.OfType<CartItem>())
                {
                    ni.PropertyChanged += CartItem_PropertyChanged;
                }
            }

            // Desuscribir de los items eliminados
            if (e.OldItems != null)
            {
                foreach (var oi in e.OldItems.OfType<CartItem>())
                {
                    oi.PropertyChanged -= CartItem_PropertyChanged;
                }
            }

            // Si hubo un reset (Clear), desuscribir todos
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                // en reset no hay OldItems, desuscribimos de todo por seguridad
                // (si los items ya referenciados implementan INotifyPropertyChanged)
                // No tenemos acceso directo a los antiguos, así que solo notificamos
            }

            // Notificar cambios derivables
            OnPropertyChanged(nameof(Items));
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(nameof(Total));
        }

        private void CartItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Si cambia la cantidad o el precio, recalcular Count/Total
            if (e.PropertyName == nameof(CartItem.Quantity) || e.PropertyName == nameof(CartItem.Price))
            {
                OnPropertyChanged(nameof(Count));
                OnPropertyChanged(nameof(Total));
            }
        }

        // Agregar un item al carrito, si existe aumentar
        public void Add(Product product, int qty = 1)
        {
            if (product == null) throw new ArgumentNullException(nameof(product));
            if (qty <= 0) qty = 1;

            var existing = Items.FirstOrDefault(i => i.ProductId == product.Id);

            if (existing != null)
            {
                existing.Quantity += qty; // CartItem notificará; CartService escuchará y actualizará Count/Total
            }
            else
            {
                var item = new CartItem
                {
                    ProductId = product.Id,
                    Nombre = product.Nombre,
                    ImagenPath = product.ImagenPath,
                    Price = product.Precio ?? 0m,
                    Quantity = qty
                };
                Items.Add(item);
                // Items.CollectionChanged hará el resto (suscribirá y notificará)
            }

            System.Diagnostics.Debug.WriteLine($"CartService.Add -> Items.Count={Items.Count}");
        }

        // Remueve un producto del carrito
        public void Remove(Guid productId)
        {
            var existing = Items.FirstOrDefault(i => i.ProductId == productId);
            if (existing != null)
            {
                Items.Remove(existing);
                // Items.CollectionChanged notificará Count/Total
            }
        }

        // Actualizar la cantidad de un producto en el carrito
        public void UpdateQuantity(Guid productId, int qty)
        {
            var existing = Items.FirstOrDefault(i => i.ProductId == productId);
            if (existing == null) return;

            if (qty <= 0)
            {
                Items.Remove(existing);
            }
            else
            {
                existing.Quantity = qty; // CartItem.PropertyChanged -> CartService reagirá
            }
        }

        // Limpiar el carrito
        public void Clear()
        {
            Items.Clear();
            // Items.CollectionChanged notificará Count/Total
        }

        public OrderItem[] ToOrderItems()
        {
            return Items.Select(i => new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = Guid.Empty,
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                Price = i.Price
            }).ToArray();
        }

        public bool ContainsProduct(Guid productId) => Items.Any(i => i.ProductId == productId);

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}