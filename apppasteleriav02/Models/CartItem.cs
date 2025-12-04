using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace apppasteleriav02.Models
{
    // Asegúrate de usar este modelo (o que tu modelo existente implemente INotifyPropertyChanged)
    public class CartItem : INotifyPropertyChanged
    {
        public Guid ProductId { get; set; }
        public string? Nombre { get; set; }
        public string? ImagenPath { get; set; }
        public decimal Price { get; set; }

        private int _quantity;
        public int Quantity
        {
            get => _quantity;
            set
            {
                if (_quantity == value) return;
                _quantity = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}