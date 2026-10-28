using System.Threading;
using System.Threading.Tasks;
using Firebase.Auth;
using PlatformServices.Auth;

namespace PlatformServices.Firebase
{
    public interface IFirebaseAuthProviderAdapter
    {
        AuthProviderType Provider { get; }
        bool IsSupported { get; }
        Task<FirebaseUser> SignInAsync(
            FirebaseAuth auth,
            FirebaseUser currentUser,
            CancellationToken cancellationToken = default);
    }
}