using Avalonia.Controls;
using Avalonia.Interactivity;
using Client.Models;
using Client.ViewModels;
using System.Collections.Generic;

namespace Client.Views.DialogViews
{
    public partial class GroupSelectionDialog : Window
    {
        public bool IsCancelled { get; private set; } = true;

        public GroupSelectionDialog() => InitializeComponent();

        public GroupSelectionDialog(IEnumerable<AccountGroup> groups) : this()
        {
            DataContext = new GroupSelectionDialogViewModel(groups);
        }

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is GroupSelectionDialogViewModel vm && vm.SelectedGroup != null)
            {
                IsCancelled = false;
                Close(vm.SelectedGroup);
            }
        }

        private void OnNoGroupClick(object sender, RoutedEventArgs e)
        {
            IsCancelled = false;
            Close(null);
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            IsCancelled = true;
            Close(null);
        }
    }
}
