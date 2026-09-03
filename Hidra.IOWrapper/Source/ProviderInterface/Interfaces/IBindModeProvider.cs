using System;
using Hidra.IOWrapper.DataTransferObjects;

namespace Hidra.IOWrapper.ProviderInterface.Interfaces
{
    /// <inheritdoc />
    /// <summary>
    /// Provider supports "Bind Mode" (Press any input to bind)
    /// </summary>
    public interface IBindModeProvider : IProvider
    {
        void SetDetectionMode(DetectionMode detectionMode, DeviceDescriptor deviceDescriptor, Action<ProviderDescriptor, DeviceDescriptor, BindingReport, short> callback = null);
    }
}