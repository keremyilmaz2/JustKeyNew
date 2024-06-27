namespace JustKeyNew.Services
{
    public interface IPrintNodeService
    {
        Task<string> GetPrintersAsync();
        Task<string> PrintFileAsync(string printerId, string filePath);
    }
}
