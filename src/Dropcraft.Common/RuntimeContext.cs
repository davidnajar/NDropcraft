﻿using System;
using Dropcraft.Common.Configuration;
using Dropcraft.Common.Handler;

namespace Dropcraft.Common
{
    public abstract class RuntimeContext
    {
        public string ProductPath { get; private set; }

        protected RuntimeContext(string productPath)
        {
            ProductPath = productPath;
        }

        public void RegisterExtensibilityPoint(string extensibilityPointKey,
            IExtensibilityPointHandler extensibilityPoint)
        {
            OnRegisterExtensibilityPoint(extensibilityPointKey, extensibilityPoint);
        }

        public void UnregisterExtensibilityPoint(string extensibilityPointKey)
        {
            OnUnregisterExtensibilityPoint(extensibilityPointKey);
        }

        public IExtensibilityPointHandler GetExtensibilityPoint(string extensibilityPointKey)
        {
            return OnGetExtensibilityPoint(extensibilityPointKey);
        }

        public void RegisterExtension(ExtensionInfo extensionInfo)
        {
            OnRegisterExtension(extensionInfo);
        }

        public void RegisterRuntimeEventHandler(IRuntimeEventsHandler runtimeEventsHandler)
        {
            OnRegisterRuntimeEventHandler(runtimeEventsHandler);
        }

        public void UnregisterRuntimeEventHandler(IRuntimeEventsHandler runtimeEventsHandler)
        {
            OnUnregisterRuntimeEventHandler(runtimeEventsHandler);
        }

        public void RaiseRuntimeEvent(RuntimeEvent runtimeEvent)
        {
            OnRaiseRuntimeEvent(runtimeEvent);
        }

        public T GetHostService<T>() where T : class
        {
            return OnGetHostService<T>();
        }

        public void RegisterHostService(Type type, Func<object> serviceFactory)
        {
            OnRegisterHostService(type, serviceFactory);
        }

        protected abstract void OnRegisterExtensibilityPoint(string extensibilityPointKey, IExtensibilityPointHandler extensibilityPoint);
        protected abstract void OnUnregisterExtensibilityPoint(string extensibilityPointKey);
        protected abstract IExtensibilityPointHandler OnGetExtensibilityPoint(string extensibilityPointKey);
        protected abstract void OnRegisterExtension(ExtensionInfo extensionInfo);
        protected abstract void OnRegisterRuntimeEventHandler(IRuntimeEventsHandler runtimeEventsHandler);
        protected abstract void OnUnregisterRuntimeEventHandler(IRuntimeEventsHandler runtimeEventsHandler);
        protected abstract void OnRaiseRuntimeEvent(RuntimeEvent runtimeEvent);
        protected abstract T OnGetHostService<T>() where T : class;

        protected abstract void OnRegisterHostService(Type type, Func<object> serviceFactory);
    }
}