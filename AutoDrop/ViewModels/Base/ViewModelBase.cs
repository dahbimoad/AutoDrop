using CommunityToolkit.Mvvm.ComponentModel;

namespace AutoDrop.ViewModels.Base;

/// <summary>
/// Base class for all ViewModels providing INotifyPropertyChanged implementation.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    private bool _isBusy;

    /// <summary>
    /// Indicates whether the ViewModel is performing a background operation.
    /// </summary>
    public bool IsBusy
    {
        get => _isBusy;
        protected set => SetProperty(ref _isBusy, value);
    }
}
