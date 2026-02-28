using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;

namespace Client.ViewModels.DialogWindow
{
    public partial class LoginDialogViewModel : ViewModelBase
    {
        private readonly AuthService _auth;

        [ObservableProperty]
        private string _email = string.Empty;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _isRegisterMode;

        public string Title => IsRegisterMode ? "Регистрация" : "Вход в аккаунт";
        public string ActionText => IsRegisterMode ? "Зарегистрироваться" : "Войти";
        public string ToggleText => IsRegisterMode
            ? "Уже есть аккаунт? Войти"
            : "Нет аккаунта? Зарегистрироваться";

        public event Action? OnSuccess;

        public LoginDialogViewModel(AuthService auth)
        {
            _auth = auth;
        }

        partial void OnIsRegisterModeChanged(bool value)
        {
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(ActionText));
            OnPropertyChanged(nameof(ToggleText));
        }

        [RelayCommand]
        private void ToggleMode()
        {
            IsRegisterMode = !IsRegisterMode;
            ErrorMessage = null;
        }

        [RelayCommand]
        private async Task Submit()
        {
            ErrorMessage = null;

            if (string.IsNullOrWhiteSpace(Email))
            {
                ErrorMessage = "Введите email";
                return;
            }
            if (string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Введите пароль";
                return;
            }

            IsLoading = true;

            var result = IsRegisterMode
                ? await _auth.RegisterAsync(Email.Trim(), Password)
                : await _auth.LoginAsync(Email.Trim(), Password);

            IsLoading = false;

            if (result.Ok)
            {
                OnSuccess?.Invoke();
            }
            else
            {
                ErrorMessage = result.Error;
            }
        }
    }
}
