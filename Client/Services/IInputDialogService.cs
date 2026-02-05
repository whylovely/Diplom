using System.Threading.Tasks;

namespace Client.Services
{
    public interface IInputDialogService
    {
        Task<string?> PromptAsync(string title, string message, string? initialText = null);
    }
}