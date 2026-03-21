using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Client.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Client.ViewModels
{
    public sealed partial class GroupSelectionDialogViewModel : ObservableObject
    {
        public ObservableCollection<AccountGroup> Groups { get; }
        [ObservableProperty] private AccountGroup? _selectedGroup;

        public GroupSelectionDialogViewModel(IEnumerable<AccountGroup> groups)
        {
            Groups = new ObservableCollection<AccountGroup>(groups);
        }
    }
}
