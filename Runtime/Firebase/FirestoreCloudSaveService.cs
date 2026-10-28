using System;
using System.Threading;
using System.Threading.Tasks;
using Firebase.Firestore;
using PlatformServices.CloudSave;
using PlatformServices.Core;

namespace PlatformServices.Firebase
{
    public sealed class FirestoreCloudSaveService : ICloudSaveService
    {
        private readonly IFirebaseService firebase;
        private FirebaseFirestore firestore;

        public FirestoreCloudSaveService(IFirebaseService firebase)
        {
            this.firebase = firebase ?? throw new ArgumentNullException(nameof(firebase));
        }

        public bool IsInitialized { get; private set; }

        public Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!firebase.IsAvailable)
                return Task.FromResult(false);

            firestore = FirebaseFirestore.GetInstance(firebase.App);
            IsInitialized = firestore != null;
            return Task.FromResult(IsInitialized);
        }

        public async Task<bool> SaveDataAsync<T>(string collection, string documentId, T data, CancellationToken cancellationToken = default)
        {
            DocumentReference document = GetDocument(collection, documentId);
            cancellationToken.ThrowIfCancellationRequested();
            await document.SetAsync(data).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return true;
        }

        public async Task<T> LoadDataAsync<T>(string collection, string documentId, CancellationToken cancellationToken = default)
        {
            DocumentReference document = GetDocument(collection, documentId);
            cancellationToken.ThrowIfCancellationRequested();
            DocumentSnapshot snapshot = await document.GetSnapshotAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return snapshot.Exists ? snapshot.ConvertTo<T>() : default;
        }

        public async Task<bool> DeleteDataAsync(string collection, string documentId, CancellationToken cancellationToken = default)
        {
            DocumentReference document = GetDocument(collection, documentId);
            cancellationToken.ThrowIfCancellationRequested();
            await document.DeleteAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return true;
        }

        public void Dispose()
        {
            firestore = null;
            IsInitialized = false;
        }

        private DocumentReference GetDocument(string collection, string documentId)
        {
            if (!IsInitialized || firestore == null)
                throw new InvalidOperationException("Firestore has not been initialized.");
            if (string.IsNullOrWhiteSpace(collection) || collection.Contains("/"))
                throw new ArgumentException("Collection must be a non-empty single path segment.", nameof(collection));
            if (string.IsNullOrWhiteSpace(documentId) || documentId.Contains("/"))
                throw new ArgumentException("Document ID must be a non-empty single path segment.", nameof(documentId));

            return firestore.Collection(collection).Document(documentId);
        }
    }
}