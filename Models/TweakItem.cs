using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AdminWorks.Services;
using Microsoft.UI.Xaml;

namespace AdminWorks.Models
{
    public class TweakItem : INotifyPropertyChanged
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string CategoryTag { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconGlyph { get; set; } = "\uE945"; // Default Bolt
        public bool IsToggle { get; set; }

        public Visibility ToggleVisibility => IsToggle ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ActionVisibility => IsToggle ? Visibility.Collapsed : Visibility.Visible;
        public bool IsNotBusy => !IsBusy;

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy != value)
                {
                    _isBusy = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsNotBusy));
                }
            }
        }

        public string StatusText => IsToggle ? (IsActive ? "Enabled" : "Disabled") : "Apply";

        public Func<ExecutionService, Task>? Action { get; set; }
        public Func<ExecutionService, Task>? EnableAction { get; set; }
        public Func<ExecutionService, Task>? DisableAction { get; set; }
        public Func<bool>? CheckStatus { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
