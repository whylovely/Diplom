using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Client.Models
{
    public sealed class AccountGroup : ObservableObject
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public int SortOrder { get; set; } = 0;
    }
}
