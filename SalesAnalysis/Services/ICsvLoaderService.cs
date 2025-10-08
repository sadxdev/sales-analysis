namespace SalesAnalysis.Services
{
    public interface ICsvLoaderService
    {
        public Task<LoadResult> LoadCsvFileAsync(string filePath, CancellationToken ct = default);
    }
}

