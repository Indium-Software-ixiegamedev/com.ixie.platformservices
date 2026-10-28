using System;
using System.Reflection;
using Firebase;

namespace PlatformServices.AppCheck
{
    public sealed class ReflectionFirebaseAppCheckProviderConfigurator : IAppCheckProviderConfigurator
    {
        private readonly string providerFactoryTypeName;

        public ReflectionFirebaseAppCheckProviderConfigurator(string providerFactoryTypeName)
        {
            this.providerFactoryTypeName = providerFactoryTypeName;
        }

        public bool Configure(FirebaseApp app, out string failureReason)
        {
            failureReason = null;
            if (app == null)
            {
                failureReason = "Firebase app is unavailable.";
                return false;
            }

            Type appCheckType = Type.GetType("Firebase.AppCheck.FirebaseAppCheck, Firebase.AppCheck");
            Type providerFactoryType = Type.GetType(providerFactoryTypeName);
            if (appCheckType == null || providerFactoryType == null)
            {
                failureReason = "Firebase App Check provider assembly or provider factory is unavailable.";
                return false;
            }

            object providerFactory = GetProviderFactory(providerFactoryType);
            if (providerFactory == null)
            {
                failureReason = "Firebase App Check provider factory could not be created.";
                return false;
            }

            MethodInfo configurationMethod = FindConfigurationMethod(appCheckType, providerFactoryType, providerFactory);
            if (configurationMethod == null)
            {
                failureReason = "Firebase App Check provider configuration method is unavailable.";
                return false;
            }

            try
            {
                object target = configurationMethod.IsStatic ? null : appCheckType.GetMethod("GetInstance", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, new object[] { app });
                if (!configurationMethod.IsStatic && target == null)
                {
                    failureReason = "Firebase App Check instance could not be created.";
                    return false;
                }

                configurationMethod.Invoke(target, new[] { providerFactory });
                return true;
            }
            catch (Exception exception)
            {
                failureReason = "Firebase App Check provider configuration failed: " + exception.GetBaseException().Message;
                return false;
            }
        }

        private static object GetProviderFactory(Type providerFactoryType)
        {
            PropertyInfo instanceProperty = providerFactoryType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            if (instanceProperty != null)
                return instanceProperty.GetValue(null, null);

            FieldInfo instanceField = providerFactoryType.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
            if (instanceField != null)
                return instanceField.GetValue(null);

            return Activator.CreateInstance(providerFactoryType);
        }

        private static MethodInfo FindConfigurationMethod(Type appCheckType, Type providerFactoryType, object providerFactory)
        {
            MethodInfo[] methods = appCheckType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
            for (int index = 0; index < methods.Length; index++)
            {
                MethodInfo method = methods[index];
                if (method.Name != "SetAppCheckProviderFactory" && method.Name != "InstallAppCheckProviderFactory")
                    continue;

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType.IsAssignableFrom(providerFactoryType))
                    return method;
            }

            return null;
        }
    }
}
