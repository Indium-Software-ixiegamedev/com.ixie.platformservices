# PlatformServices Module

## 1. Purpose and Current Scope

`PlatformServices` is the Unity-facing abstraction for platform and backend services used by Bharat Darshan. It ships as the `com.ixie.platformservices` package. Game code accesses typed interfaces through `IPlatformService.GetFeature<T>()`; concrete Firebase and native SDK details remain in provider implementations that live in optional assemblies.

The production module currently provides:

- Firebase application initialization.
- Firebase Authentication.
- Anonymous guest authentication.
- Google Account authentication adapter with explicit account selection.
- Google Play Games authentication adapter for Android.
- Sign in with Apple through Firebase federated OAuth on iOS.
- Existing Firebase session restoration.
- Guest-to-provider account linking that preserves the Firebase UID.
- Sequential feature initialization and reverse-order shutdown.
- Injectable lifecycle and feature-failure logging.

The module also contains a Firestore cloud-save implementation registered by the Firebase installer, interface definitions for future services, and editor-only test doubles. Firebase and Unity IAP code compiles only when the corresponding SDK is present and the feature is enabled in Project Settings (see section 3).

This document describes the code that currently exists. Planned features are explicitly marked as such.

## 2. Implementation Status

| Feature | Production status | Notes |
| --- | --- | --- |
| Core lifecycle | Implemented | Registration, sequential initialization, state changes, and shutdown |
| Core logging | Implemented and registered | Unity console logger identifies feature initialization and disposal failures |
| Firebase App | Implemented; registered by the Firebase installer | Checks and fixes Firebase dependencies before obtaining the default app |
| Firebase Auth | Implemented; registered by the Firebase installer | Guest, Google Account, and Apple ID providers are registered; the Google Play Games adapter exists but is not registered |
| Google Account adapter | Implemented, external SDK required | Used by the Google login button; requires `GOOGLE_SIGN_IN` and a Web OAuth client ID |
| Apple ID adapter | Implemented | Enabled only for iOS device builds |
| Google Play Games adapter | Implemented, not registered | Retained separately for future Play Games-specific login; its registration is commented out in the Firebase installer |
| Firestore cloud save | Implemented; registered by the Firebase installer | Registered after Firebase Auth; security rules are required before production use |
| Consent / UMP / ATT | Interface and editor double only | No production implementation |
| Integrity gate | Implemented, enabled by default | `enableIntegrityCheck` controls both the local integrity gate and Firebase App Check registration; platform checks include application identity, genuine-app status, build policy, and optional Android signing/installer signals |
| Firebase App Check | Production orchestration implemented, provider SDK required | Registered only when `enableIntegrityCheck` is true; configures the platform provider through reflection and fails closed on device if the provider cannot be configured |
| In-app purchases | Client and Firebase Functions source implemented; deployment/configuration required | `PLATFORMSERVICES_IAP_ENABLED` registers non-consumable products when a catalog is supplied; Gen 2 callables enforce Auth/App Check and validate store proof. Apple notifications and Google RTDN reconcile refunds. Production remains disabled until environment parameters, store access, and deployments are verified |
| Remote Config | Interface and editor double only | No production implementation |
| Analytics | Interface and editor double only | No production implementation |
| Push notifications | Interface and editor double only | Not exposed by `IPlatformService` and not registered by either factory |

## 3. Runtime Architecture

### 3.1 Package and Assemblies

```text
ExternalPlugins/com.ixie.platformservices/
|-- Runtime/                  Ixie.PlatformServices (core; no Firebase or Unity IAP references)
|   |-- Firebase/             Ixie.PlatformServices.Firebase (optional)
|   `-- Iap/UnityIap/         Ixie.PlatformServices.UnityIap (optional)
|-- Editor/                   Ixie.PlatformServices.Editor (settings, SDK detection, test doubles)
|-- Samples~/                 PlatformServicesBasics sample
`-- Documentation~/           This document
```

The core assembly contains the lifecycle, the feature interfaces (`IAuthService`, `IIapService`, `IAppCheckService`, and so on), `IapProductCatalog`, `IntegrityService`, and `ProductionPlatformServiceFactory`. It has no reference to Firebase or Unity IAP types. The two optional assemblies reference the core and contribute their features through installers.

### 3.2 Enabling Optional Features

Optional assemblies are controlled by scripting define symbols. An assembly compiles only when all of its constraint defines are set.

| Define | Set by | Meaning |
| --- | --- | --- |
| `PLATFORMSERVICES_FIREBASE` | Automatically, by `FirebaseSdkDetector` | `Firebase.App`, `Firebase.Auth`, `Firebase.Firestore`, and `Firebase.Functions` DLLs are in the project (`.unitypackage` or UPM) |
| `PLATFORMSERVICES_FIREBASE_ENABLED` | Manually, in Project Settings | Compile the Firebase platform services |
| `PLATFORMSERVICES_UNITY_IAP` | `versionDefines` in the Unity IAP asmdef | `com.unity.purchasing` 5.0.0 or later is installed |
| `PLATFORMSERVICES_IAP_ENABLED` | Manually, in Project Settings | Compile the Unity IAP platform service |

`Ixie.PlatformServices.Firebase` requires `PLATFORMSERVICES_FIREBASE` and `PLATFORMSERVICES_FIREBASE_ENABLED`. `Ixie.PlatformServices.UnityIap` requires `PLATFORMSERVICES_UNITY_IAP` and `PLATFORMSERVICES_IAP_ENABLED`.

The **Project Settings > Ixie Platform Services** page shows detection status (including any missing Firebase assemblies and the detected Unity IAP version) and provides the two enable toggles. A toggle is disabled until its SDK is detected, and changing it updates the defines for the Standalone, Server, iOS, Android, and WebGL build targets and triggers a recompile. `FirebaseSdkDetector` re-evaluates `PLATFORMSERVICES_FIREBASE` on domain reload and whenever a Firebase DLL is imported, deleted, or moved.

Notes:

- IAP receipt validation is provided by the Firebase integration, so IAP requires Firebase to be enabled as well.
- The Firebase assembly is an asmdef and cannot see script-source plugins that compile into `Assembly-CSharp`. The Google Sign-In and Play Games adapters (behind `GOOGLE_SIGN_IN` and `GOOGLE_PLAY_GAMES`) need those plugins as DLLs or asmdefs; if they are asmdefs, add them to the Firebase asmdef references.

### 3.3 Composition

```text
BootStrapper
    |
    v
PlatformService (MonoBehaviour integration layer)
    |
    v
ProductionPlatformServiceFactory.CreateProduction()
    |
    v
PlatformServiceCore / IPlatformService
    |
    +-- UnityLogService / IPlatformServiceLogger
    |
    +-- IntegrityService / IIntegrityService            (factory, when enableIntegrityCheck)
    |
    +-- PlatformFeatureInstallerRegistry installers, in Order:
        |
        +-- FirebasePlatformFeatureInstaller (Order 100)
        |   +-- FirebaseService / IFirebaseService
        |   +-- FirebaseAppCheckService / IAppCheckService   (when enableIntegrityCheck)
        |   +-- FirebaseAuthService / IAuthService
        |   |   +-- GoogleAccountAuthProviderAdapter
        |   |   `-- AppleAuthProviderAdapter
        |   +-- FirestoreCloudSaveService / ICloudSaveService
        |   `-- publishes FirebaseFunctionReceiptValidator as options.ReceiptValidator
        |
        `-- UnityIapPlatformFeatureInstaller (Order 200)
            `-- UnityIapService / IIapService (needs a catalog and a receipt validator)
                +-- Unity IAP StoreController
                `-- IReceiptValidator (FirebaseFunctionReceiptValidator)
```

`CreateProduction(googleWebClientId, enableIntegrityCheck, integrityConfiguration, appCheckConfiguration, iapProductCatalog)` builds a `PlatformServiceOptions` object, registers `IntegrityService` first when `enableIntegrityCheck` is true, and then runs every installer in `PlatformFeatureInstallerRegistry` in ascending `Order`. Installers receive the `IPlatformService` and the options; `PlatformServiceOptions.ReceiptValidator` is how the Firebase installer hands a validator to the IAP installer. There is no `enableIap` parameter: IAP is registered when the IAP assembly is enabled and `iapProductCatalog` is supplied (a missing catalog logs a warning and skips IAP).

When the Firebase assembly is not compiled, only Integrity is registered by default, and `GetFeature<IAuthService>()` and similar calls return `null`. When `enableIntegrityCheck` is false, both `IntegrityService` and `FirebaseAppCheckService` are omitted; Firebase and Auth remain registered.

The optional assemblies register their installers at load time (`[RuntimeInitializeOnLoadMethod]`, plus `[InitializeOnLoadMethod]` in the Editor), so the core never references them. `PlatformFeatureInstallerRegistry.Register` replaces an existing installer of the same type.

Registration order is initialization order. Integrity must be registered first, Firebase before App Check, and App Check before services that make protected Firebase requests. Installer `Order` values (100 for Firebase, 200 for IAP) preserve that sequence; custom installers can pick an order between them.

### 3.4 Custom Installers

Project-specific features can join the composition without editing the package by implementing `IPlatformFeatureInstaller` and registering it at load time:

```csharp
public sealed class MyInstaller : IPlatformFeatureInstaller
{
    public int Order => 150; // after Firebase (100), before IAP (200)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Register() => PlatformFeatureInstallerRegistry.Register(new MyInstaller());

    public void Install(IPlatformService platform, PlatformServiceOptions options)
    {
        IFirebaseService firebase = platform.GetFeature<IFirebaseService>();
        if (firebase != null)
            platform.RegisterFeature(new MyFeature(firebase));
    }
}
```

The `PlatformServicesBasics` sample (Package Manager > Platform Services > Samples) demonstrates this, plus manual composition without `CreateProduction`. Code that touches Firebase types should be wrapped in `#if PLATFORMSERVICES_FIREBASE && PLATFORMSERVICES_FIREBASE_ENABLED`.

The project uses its existing `ServiceLocator` to expose the `PlatformService` MonoBehaviour to game systems. `PlatformServiceCore` itself remains a plain C# composition root and does not depend on the service locator.

## 4. Core Lifecycle

### 4.1 `IPlatformFeature`

Every registered feature implements the same lifecycle contract:

```csharp
public interface IPlatformFeature : IDisposable
{
    bool IsInitialized { get; }
    Task<bool> InitializeAsync(CancellationToken cancellationToken = default);
}
```

### 4.2 `PlatformServiceCore`

`PlatformServiceCore` owns registered feature instances and exposes them through `GetFeature<T>()` and `HasFeature<T>()`; it has no per-service properties and references no feature namespaces. Its optional constructor dependency, `IPlatformServiceLogger`, keeps the core independent from a specific logging framework.

Lifecycle states are:

```text
NotStarted -> Initializing -> Ready
                         \-> Failed
Ready/Failed -> ShuttingDown -> Shutdown
```

Current behavior:

- Features must be registered before initialization starts.
- Features initialize sequentially in registration order.
- Initialization stops and the core enters `Failed` when a feature returns `false`.
- Cancellation is propagated as `OperationCanceledException` and sets the state to `Failed` when appropriate.
- Other initialization exceptions are caught by the core and return `false`.
- Repeated initialization returns immediately when the core is already ready.
- Shutdown disposes features in reverse registration order.
- One feature throwing during disposal does not prevent the remaining features from being disposed.
- `GetFeature<T>()` returns the first registered feature assignable to `T` (interface or concrete type), or `null` when none is registered. Lookups are cached per requested type and the cache is cleared on every registration.
- `HasFeature<T>()` can be used before accessing an optional feature.

There is no special parallel or post-auth initialization phase. Any future dependencies must be represented by registration order (factory or installer `Order`) or by a deliberate extension to the core.

### 4.3 Lifecycle Logging

`IPlatformServiceLogger` defines information, warning, and error methods. `UnityPlatformServiceLogger.cs` (class `UnityLogService`) is injected by the production factory and writes messages to the Unity Console with a `[PlatformServices]` prefix.

During initialization, the core logs:

- The number of registered features.
- The concrete feature type before initialization.
- Successful feature initialization.
- A feature returning `false`, including its concrete type.
- Cancellation, including the active feature type.
- Exceptions, including the active feature type and stack trace.
- Successful completion of all registered features.

Disposal exceptions also include the concrete feature type and stack trace. Shutdown continues after a disposal failure.

The logger is optional so isolated tests or reusable compositions can construct `PlatformServiceCore` without a Unity dependency:

```csharp
PlatformServiceCore silentCore = new PlatformServiceCore();
PlatformServiceCore productionCore = new PlatformServiceCore(new UnityLogService());
```

## 5. Firebase Application Service

`FirebaseService` implements `IFirebaseService` and owns the `FirebaseApp` reference.

Initialization performs:

1. Cancellation check.
2. `FirebaseApp.CheckAndFixDependenciesAsync()`.
3. Dependency status validation.
4. Retrieval of `FirebaseApp.DefaultInstance`.

`IsAvailable` is true only when initialization succeeded, dependencies are available, and the app instance exists. Dependent Firebase features return `false` when Firebase is unavailable.

Platform configuration files are stored under `Assets/StreamingAssets`:

- Android: `google-services.json`
- iOS: `GoogleService-Info.plist`

### 5.1 Integrity Gate

`IntegrityService` is registered before Firebase so a failed local check stops all later feature initialization. `PlatformService` exposes the serialized `enableIntegrityCheck` option, which defaults to `true` and controls both `IntegrityService` and Firebase App Check registration.

When enabled, the service checks:

- `Application.identifier` against `expectedApplicationIdentifier`.
- `Application.genuine` when `RequireGenuineApplication` is enabled (enabled by default).
- `DEVELOPMENT_BUILD` according to `allowDevelopmentBuild` (development builds are rejected by default).
- On Android devices only, the signing certificate SHA-256 fingerprint when `requireAndroidSigningCertificate` is enabled.
- On Android devices only, the installer package name when `requireAndroidInstallerPackage` is enabled.

The expected application identifier is configured in Unity Player settings under Identification / Package Name. The signing fingerprint comes from the release signing certificate; the installer package is the distribution store's Android package name. The Unity component tooltips describe these values and their sources.

If a required validation fails, initialization returns `false`, the core enters `Failed`, and subsequent registered features do not initialize. iOS currently receives application-identifier, genuine-app, and development-build checks only; jailbreak/tamper checks are not implemented. These client-side checks are signals and are not a substitute for trusted server enforcement.

Turning `enableIntegrityCheck` off skips both local checks and Firebase App Check. This is intended only for projects/builds that deliberately do not use either protection; it also removes App Check attestation from Firebase requests.

### 5.2 Firebase App Check

`FirebaseAppCheckService` is registered after `FirebaseService` and before Firebase Auth, and only when `enableIntegrityCheck` is true. It uses `IAppCheckProviderConfigurator`; the Firebase installer selects Play Integrity on Android and App Attest on iOS. The provider factory type names are defined in the installer and can be overridden through `AppCheckConfiguration.AndroidProviderFactoryTypeName` and `IosProviderFactoryTypeName` (both `null` by default in the core). `ReflectionFirebaseAppCheckProviderConfigurator` resolves Firebase provider types and methods at runtime, so the Firebase App Check SDK/provider assembly must be present and the configured type names must match that SDK version. Device initialization fails if provider configuration is unavailable. Editor configuration may be allowed by `AppCheckConfiguration.AllowUnconfiguredInEditor`.

`GetTokenAsync()` retrieves a Firebase App Check token after successful initialization. Provider setup and token retrieval do not by themselves enforce App Check: enforcement must also be enabled for each Firebase product/backend, and security rules and server-side authorization remain required. Reflection can be removed once the project makes a compatible provider SDK a mandatory compile-time dependency and the implementation is changed to direct typed API calls.

### 5.3 In-App Purchasing

Unity IAP is version 5.4.3 in this project. IAP is controlled by **Project Settings > Ixie Platform Services** (`PLATFORMSERVICES_IAP_ENABLED`); there is no runtime `enableIap` flag. When enabled, and an `IapProductCatalog` is passed to `CreateProduction`, `UnityIapPlatformFeatureInstaller` registers `UnityIapService` after Firebase Auth and App Check, using the validator the Firebase installer publishes as `PlatformServiceOptions.ReceiptValidator`. It requires a valid `IapProductCatalog`, an initialized App Check service, and a signed-in player before purchases can start. Store connection failure is exposed through `IIapService.IsStoreReady` and `LastError` without failing the whole platform startup. Backend source and detailed setup instructions are in `functions/` and `Docs/iap-production-guide.md`; no Firebase project has been selected or deployed from this repository.

Shloka tracks must be configured as store non-consumables. `IapProductCatalog` maps each canonical product ID to Apple/Google store IDs and one or more permanent entitlement IDs. The game checks `IIapService.HasEntitlement(entitlementId)` or listens to `EntitlementsChanged`; ownership is not inferred from local preferences or a client-only store callback.

Purchase processing follows this order:

1. Unity IAP reports a `PendingOrder`.
2. The client submits its product ID, store, transaction ID, store purchase token/JWS, raw receipt, and idempotency key to the `validateIapPurchase` callable.
3. The backend verifies the transaction with the relevant store, binds it to the authenticated Firebase UID, atomically records the transaction and grants the server-owned entitlements, then returns the complete owned-entitlement snapshot.
4. Only after the backend accepts the transaction and returns the product's configured entitlements does the client update its in-memory entitlement snapshot and call `ConfirmPurchase`.
5. If verification fails or the backend is unavailable, the client does not confirm the pending order. Unity IAP can redeliver it later; the backend must make transaction processing idempotent.

`RestorePurchasesAsync()` requests a store restore, fetches store purchases, verifies any previously confirmed catalog orders through the backend, and refreshes the server snapshot through `getIapEntitlements`. Consumable balances are not part of the Shloka design; if consumables are added later, persist their ledger server-side before acknowledging them.

The callable contract is:

- `getIapAccountBinding`: derives UID from callable Auth and creates/returns stable Apple `appAccountToken` and Google obfuscated-account identifiers. Unity IAP sets them before checkout; the verifier requires them to match.
- `validateIapPurchase`: request fields are `productId`, `store`, `transactionId`, `storePurchaseToken`, `rawReceipt`, and `idempotencyKey`; response fields are `valid`, canonical `productId`, `transactionId`, optional `errorCode`, and the complete `ownedEntitlementIds` snapshot.
- `getIapEntitlements`: takes no player UID from the client; it derives the UID from callable authentication and returns the complete `ownedEntitlementIds` snapshot.

The server source is present, but deployment requires real dev/prod Firebase project IDs and store configuration. Before enabling IAP in any release, deploy and test the functions/rules, enable callable App Check enforcement, grant the Functions runtime identity access to Google Play Developer API, configure Apple's V2 notification URL and Google's RTDN topic, and populate the project-specific product catalog. Never grant entitlements from client-supplied product mappings or raw receipt parsing alone. Separate development and production Firebase projects, store app IDs, product catalogs, and databases.

## 6. Authentication

### 6.1 Public Contract

`IAuthService` supports these providers:

```csharp
public enum AuthProviderType
{
    Guest,
    GooglePlayGames,
    AppleId,
    GoogleAccount
}
```

The service exposes the current `UserProfile`, authentication state, an `AuthStateChanged` event, and operations for sign-in, provider linking, sign-out, and account deletion.

### 6.2 Initialization and Session Restoration

`FirebaseAuthService.InitializeAsync()` initializes the Firebase Auth client and restores `FirebaseAuth.CurrentUser` when Firebase has a cached session.

Important behavior:

- A new installation remains signed out after platform initialization.
- Initialization does not automatically create a guest account.
- A login begins only after the player selects a login option.
- A returning Firebase session is restored without reopening a provider prompt.
- Authentication initialization can succeed while `IsAuthenticated` is false.

This distinction allows the login scene to be shown on first launch.

### 6.3 Guest Login

Guest login calls `FirebaseAuth.SignInAnonymouslyAsync()` when no Firebase user exists.

If a Firebase user is already active, selecting guest does not replace that user. The service republishes the existing profile instead.

Anonymous authentication must be enabled in Firebase Console under **Authentication > Sign-in method**.

### 6.4 Provider Adapter Boundary

Native providers implement `IFirebaseAuthProviderAdapter`:

```csharp
public interface IFirebaseAuthProviderAdapter
{
    AuthProviderType Provider { get; }
    bool IsSupported { get; }
    Task<FirebaseUser> SignInAsync(
        FirebaseAuth auth,
        FirebaseUser currentUser,
        CancellationToken cancellationToken = default);
}
```

Provider adapters are responsible for opening the provider flow and returning the resulting Firebase user. `FirebaseAuthService` remains the single owner of Firebase authentication state and publishes the resulting `UserProfile`.

When the current user is anonymous, adapters link the provider to that user rather than creating an unrelated Firebase account. This preserves the Firebase UID and any data associated with the guest.

### 6.5 Google Account Sign-In

`GoogleAccountAuthProviderAdapter` is the provider used by the login screen's Google button. It:

1. Configures Google Sign-In with the injected Web OAuth client ID.
2. Calls `GoogleSignIn.DefaultInstance.SignOut()` to clear the cached SDK account selection.
3. Calls interactive `SignIn()`, causing the Google account chooser to be presented when the button is clicked.
4. Creates a Firebase credential from the returned ID token with `GoogleAuthProvider.GetCredential()`.
5. Links the credential for an anonymous user or signs in when there is no guest to preserve.

The Web OAuth client ID is serialized on `PlatformService` as `googleWebClientId` and passed through the production factory. It must not be replaced by the Android client ID.

The adapter requires the Google Sign-In for Unity SDK and the `GOOGLE_SIGN_IN` scripting define. Without the SDK, define, or web client ID, it reports itself as unsupported and sign-in returns `false`.

Required Android configuration:

1. Enable Google in Firebase Authentication.
2. Create the Web and Android OAuth clients in the same Firebase/Google Cloud project.
3. Configure the Android OAuth client with the package name and signing SHA fingerprints.
4. Add `GOOGLE_SIGN_IN` to Android scripting define symbols.
5. Assign the Web OAuth client ID on the `PlatformService` component.
6. Test account selection and Firebase login on a physical device.

### 6.6 Google Play Games on Android

`GooglePlayGamesAuthProviderAdapter`:

1. Authenticates through `PlayGamesPlatform`.
2. Requests server-side access and receives a server auth code.
3. Creates a Firebase credential with `PlayGamesAuthProvider.GetCredential()`.
4. Links the credential for an anonymous user or signs in when there is no guest to preserve.

The adapter is supported only when all of these conditions are true:

- The target is Android.
- The build is not an Editor build.
- `GOOGLE_PLAY_GAMES` is defined.
- The official Google Play Games plugin for Unity is installed and configured.

Without the package and define, the adapter remains compile-safe and provider sign-in returns `false`.

Required external configuration:

1. Enable Play Games in Firebase Authentication.
2. Configure Play Games Services in Google Play Console.
3. Create and configure the web application OAuth client.
4. Configure the matching credentials in Firebase.
5. Run the Google Play Games Unity setup with the web client ID.
6. Add `GOOGLE_PLAY_GAMES` to Android scripting define symbols.

The Play Games adapter is retained as a separate provider for future Play Games-specific authentication. The current Google login button does not invoke it.

### 6.7 Sign in with Apple on iOS

`AppleAuthProviderAdapter` uses Firebase's `FederatedOAuthProvider` with provider ID `apple.com`.

It links with the current anonymous user or starts a normal Firebase provider sign-in when no guest exists. No separate Unity Apple authentication plugin is used by the current implementation.

The adapter is supported only on an iOS device build. Required external configuration:

1. Enable Apple in Firebase Authentication.
2. Enable Sign in with Apple for the application identifier in Apple Developer.
3. Configure the Apple team, key, and service details required by Firebase.
4. Add the Sign in with Apple capability to the generated Xcode target.
5. Validate the flow on a physical device.

### 6.8 Authentication Events and Threading

`FirebaseAuthService` captures the current `SynchronizationContext` when it is constructed. `AuthStateChanged` is invoked directly when already on that context, otherwise it is posted back to the captured context.

`IPlatformMainThreadDispatcher` exists as a future abstraction but is not currently injected into production services.

### 6.9 Account Deletion

The current `DeleteAccountAsync()` calls `FirebaseUser.DeleteAsync()` directly and clears the local profile after success.

This only deletes the Firebase Auth user. It does not currently provide a backend workflow for deleting Firestore data, purchase records, analytics data, or other user-owned records. Production compliance work must add a trusted, authenticated, retryable backend deletion workflow before this is treated as complete account deletion.

## 7. Application Login Flow

### 7.1 Startup

`BootStrapper`:

1. Creates and registers the project services.
2. Calls `PlatformService.Initialize()`.
3. Awaits `PlatformService.InitializationTask`.
4. Raises `OnInitializeCompleteEvent` after successful platform initialization.

`autoLoadMainMenu` defaults to disabled so login navigation remains owned by `LoginManager`.

`PlatformService` currently uses `ProductionPlatformServiceFactory.CreateProduction()` in all builds, including the Editor. The testing factory is used by tests, not by normal runtime startup. The serialized `enableIntegrityCheck` option is passed into the factory and controls registration of both Integrity and App Check.

### 7.2 First Launch

```text
Boot completes
    |
    v
No LAST_LOGIN_TYPE preference
    |
    v
Load login scene
    |
    +-- Android: Google Account button + Guest button
    |
    +-- iOS: Apple button + Guest button
```

`LoginSelectionUI` raises a `LoginType` event. `LoginManager` maps that type to `AuthProviderType` and awaits `IAuthService.SignInAsync()`.

The main menu loads and `LAST_LOGIN_TYPE` is saved only after Firebase reports a successful login. Concurrent login requests are ignored while a provider flow is in progress.

### 7.3 Returning Player

When `LAST_LOGIN_TYPE` exists, `LoginManager` checks the Firebase session restored during platform initialization:

- Authenticated: load the main menu.
- Not authenticated: remove the stale preference and load the login scene.

The stored login type is a navigation hint, not an authentication credential.

## 8. Cloud Save

`FirestoreCloudSaveService` is registered by `FirebasePlatformFeatureInstaller` after Firebase Auth, so it is available through `GetFeature<ICloudSaveService>()` whenever the Firebase assembly is enabled.

It supports:

- `SaveDataAsync<T>()`
- `LoadDataAsync<T>()`
- `DeleteDataAsync()`
- Cancellation checks around Firestore operations
- Validation that collection and document IDs are non-empty single path segments

Before relying on it in production, define the user document structure and deploy Firestore Security Rules that authorize access using the authenticated Firebase UID. Do not rely on client-side path validation as an authorization boundary.

To register it manually without the installer, register it after Firebase:

```csharp
platform.RegisterFeature(new FirestoreCloudSaveService(firebase));
```

## 9. Editor Testing

`TestingPlatformServices.cs` is compiled only under `UNITY_EDITOR` (in `Ixie.PlatformServices.Editor`). It provides deterministic test doubles for:

- Consent
- App Check
- Authentication
- IAP
- Remote Config
- Cloud Save
- Analytics
- Push notifications

`TestingPlatformServiceFactory.CreateDefault()` currently registers all of those except push notifications. The Firebase test double, `TestingFirebaseService`, lives in the Firebase assembly (`Runtime/Firebase/Testing/`, editor only) and is registered manually when needed.

The editor tests cover:

- Successful feature initialization.
- Cancellation propagation.
- Optional feature registration and lookup through `GetFeature<T>()`.
- Production factory registration of Firebase Authentication (through the Firebase installer).
- Feature names when initialization returns `false`.
- Feature names and exceptions when initialization throws.

These test doubles validate core orchestration and consumer logic. They do not validate Firebase native binaries, provider dialogs, store configuration, OAuth credentials, or device behavior. Android and iOS provider flows still require physical-device testing.

## 10. Planned Interfaces

The following contracts exist, but production implementations have not been added:

### Consent

`IConsentService` defines consent status, consent requests, and privacy options. Google UMP and iOS ATT integrations are not implemented.

### Remote Config

`IRemoteConfigService` defines fetch and typed value access. There is no Firebase Remote Config implementation.

### Analytics

`IAnalyticsService` defines event and user-property operations. There is no Firebase Analytics implementation.

### Push Notifications

`IPushNotificationService` defines registration and token operations. There is no Firebase Cloud Messaging implementation. The root `IPlatformService` does not currently expose a notification handle.

Do not treat editor doubles as evidence that these production integrations are complete.

## 11. Current Source Layout

```text
ExternalPlugins/com.ixie.platformservices/
|-- package.json
|-- Runtime/                                     (Ixie.PlatformServices)
|   |-- Core/
|   |   |-- IPlatformFeature.cs
|   |   |-- IPlatformFeatureInstaller.cs
|   |   |-- IPlatformMainThreadDispatcher.cs
|   |   |-- IPlatformServiceLogger.cs
|   |   |-- IPlatformService.cs
|   |   |-- PlatformFeatureInstallerRegistry.cs
|   |   |-- PlatformServiceCore.cs
|   |   |-- PlatformServiceOptions.cs
|   |   `-- UnityPlatformServiceLogger.cs
|   |-- ProductionPlatformService/
|   |   `-- ProductionPlatformServiceFactory.cs
|   |-- Integrity/
|   |   |-- IIntegrityService.cs
|   |   |-- IntegrityConfiguration.cs
|   |   `-- IntegrityService.cs
|   |-- Auth/IAuthService.cs
|   |-- CloudSave/ICloudSaveService.cs
|   |-- Consent/IConsentService.cs
|   |-- AppCheck/
|   |   |-- IAppCheckService.cs
|   |   `-- AppCheckConfiguration.cs
|   |-- Iap/
|   |   |-- IIapService.cs
|   |   |-- IReceiptValidator.cs
|   |   |-- IapProductCatalog.cs
|   |   `-- UnityIap/                            (Ixie.PlatformServices.UnityIap)
|   |       |-- UnityIapPlatformFeatureInstaller.cs
|   |       `-- UnityIapService.cs
|   |-- RemoteConfig/IRemoteConfigService.cs
|   |-- Analytics/IAnalyticsService.cs
|   |-- Notifications/IPushNotificationService.cs
|   `-- Firebase/                                (Ixie.PlatformServices.Firebase)
|       |-- FirebasePlatformFeatureInstaller.cs
|       |-- IFirebaseService.cs
|       |-- FirebaseService.cs
|       |-- FirebaseAuthService.cs
|       |-- IFirebaseAuthProviderAdapter.cs
|       |-- GoogleAccountAuthProviderAdapter.cs
|       |-- GooglePlayGamesAuthProviderAdapter.cs
|       |-- AppleAuthProviderAdapter.cs
|       |-- FirestoreCloudSaveService.cs
|       |-- AppCheck/
|       |   |-- FirebaseAppCheckService.cs
|       |   |-- IAppCheckProviderConfigurator.cs
|       |   |-- ReflectionFirebaseAppCheckProviderConfigurator.cs
|       |   `-- UnavailableAppCheckProviderConfigurator.cs
|       |-- Iap/FirebaseFunctionReceiptValidator.cs
|       `-- Testing/TestingFirebaseService.cs    (editor only)
|-- Editor/                                      (Ixie.PlatformServices.Editor)
|   |-- Settings/
|   |   |-- FirebaseSdkDetector.cs
|   |   |-- PlatformServicesDefines.cs
|   |   `-- PlatformServicesSettingsProvider.cs
|   `-- Testing/TestingPlatformServices.cs
|-- Samples~/PlatformServicesBasics/
|   |-- PlatformServicesBootstrap.cs
|   `-- ExampleFirebaseFeatureInstaller.cs
`-- Documentation~/platformservices_architecture_specification.md
```

Trusted IAP backend source lives outside `Assets`:

```text
functions/
|-- src/index.ts
|-- src/iap/config.ts
|-- src/iap/errors.ts
|-- src/iap/fulfillment.ts
|-- src/iap/identity.ts
|-- src/iap/store-verification.ts
|-- src/iap/store-notifications.ts
`-- certificates/ (Apple public Root CA certificates)
```

Application integration code lives outside the module:

```text
Assets/IxieServices/Scripts/Services/
|-- BootStrapper.cs
`-- PlatformService.cs

Assets/BharatDarshan/Scripts/
|-- Login/LoginManager.cs
`-- UI/Login/LoginSelectionUI.cs
```

## 12. Extension Rules

When adding a production feature:

1. Implement its existing interface or add a focused interface under the owning feature folder.
2. Inject dependencies through the constructor.
3. Register dependencies before dependents, either in the factory or in an `IPlatformFeatureInstaller` with a suitable `Order`.
4. Support `CancellationToken` for asynchronous operations.
5. Keep platform-specific imports and code behind precise compiler directives.
6. Return `false` from initialization when a required dependency is unavailable.
7. Let initialization exceptions reach `PlatformServiceCore` so the logger retains the feature name and stack trace.
8. Dispose owned resources and clear observable state.
9. Marshal callbacks that affect Unity objects or game events onto the Unity main thread.
10. Add an editor-safe test double only when consumer or lifecycle tests need it.
11. Add focused tests for registration, initialization failure, cancellation, and shutdown.
12. Update the implementation-status table in this document.
13. Keep the core assembly free of optional SDK types. Put SDK-dependent code in an optional assembly with `defineConstraints`, expose it through an installer registered at load time, and look features up with `GetFeature<T>()`.

## 13. Security and Production Boundaries

- Treat the Unity client, local preferences, save payloads, provider tokens, and purchase receipts as untrusted input.
- Never ship service-account keys, Apple private keys, receipt-verification secrets, or privileged backend credentials in `Assets`.
- Enforce user-data access through Firebase Security Rules and authenticated Firebase UIDs.
- App Check can reduce abuse but does not replace authorization.
- Never log raw auth tokens, provider credentials, receipts, email addresses, or device identifiers.
- Preserve anonymous UIDs by linking credentials; do not silently replace guest accounts and orphan their progress.
- Resolve provider credential collisions explicitly. Do not merge or switch accounts without clear player confirmation and a defined data policy.
- Account deletion must eventually include server-owned data and be idempotent and retryable.
- Purchase entitlements are written only by trusted Functions after store verification, Firebase UID/account-binding checks, and an idempotent Firestore transaction. Never grant them from client state.
- Consent and ATT requirements must be implemented before enabling analytics, ads, or attribution SDK behavior that requires them.
