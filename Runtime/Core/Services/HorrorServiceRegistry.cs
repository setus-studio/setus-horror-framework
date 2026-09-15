using System;
using System.Collections.Generic;

namespace Setus.HorrorFramework.Core.Services
{
    public sealed class HorrorServiceRegistry
    {
        private readonly Dictionary<Type, object> services = new Dictionary<Type, object>();

        public int Count => services.Count;

        public bool Register<TService>(TService service)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            var serviceType = typeof(TService);
            if (services.ContainsKey(serviceType))
            {
                return false;
            }

            services.Add(serviceType, service);
            return true;
        }

        public TService GetRequired<TService>()
        {
            if (TryGet<TService>(out var service))
            {
                return service;
            }

            throw new InvalidOperationException($"Service is not registered: {typeof(TService).FullName}");
        }

        public bool TryGet<TService>(out TService service)
        {
            if (services.TryGetValue(typeof(TService), out var rawService))
            {
                service = (TService)rawService;
                return true;
            }

            service = default;
            return false;
        }

        public void Clear()
        {
            services.Clear();
        }
    }
}
