using System.Threading.Tasks;
using Client.Models;

namespace Client.Services
{
    public interface ICategoryDialogService
    {
        Task<Category?> ShowAddCategoryDialogAsync(string? initialName = null);
    }
}