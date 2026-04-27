using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Client.Models
{
    /// <summary>
    /// Группа счетов — для удобной группировки в боковой панели (например, «Карты», «Наличные»).
    /// Чисто UI-сущность, не уходит на сервер при синхронизации (привязка восстанавливается локально).
    /// </summary>
    public sealed class AccountGroup : ObservableObject
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public int SortOrder { get; set; } = 0;
    }
}
