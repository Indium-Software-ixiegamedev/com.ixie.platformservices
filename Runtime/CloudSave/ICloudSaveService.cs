using System.Threading;
using System.Threading.Tasks;
using PlatformServices.Core;

namespace PlatformServices.CloudSave
{
    public interface ICloudSaveService : IPlatformFeature
    {
        Task<bool> SaveDataAsync<T>(string collection, string documentId, T data, CancellationToken cancellationToken = default);
        Task<T> LoadDataAsync<T>(string collection, string documentId, CancellationToken cancellationToken = default);
        Task<bool> DeleteDataAsync(string collection, string documentId, CancellationToken cancellationToken = default);
    }
}