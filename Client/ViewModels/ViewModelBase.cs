using CommunityToolkit.Mvvm.ComponentModel;

namespace Client.ViewModels
{
    /// <summary>
    /// Базовый класс для всех ViewModel. Наследует <see cref="ObservableObject"/>
    /// из CommunityToolkit.Mvvm — это даёт <c>[ObservableProperty]</c>, <c>[RelayCommand]</c>
    /// и автоматический INotifyPropertyChanged.
    /// </summary>
    public abstract class ViewModelBase : ObservableObject
    {
    }
}
