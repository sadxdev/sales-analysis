namespace SalesAnalysis.Services
{
    public interface ICsvLoaderService
    {
        Task LoadCsvAsync(string filePath);
    }
}
