using System;
using System.Collections.Generic;
using Hidra.Core.Models.Binding;

namespace Hidra.Core.Models
{
    public class CallbackMultiplexer
    {
        private DeviceBinding.ValueChanged? _mappingUpdate;
        private readonly int _index;
        private readonly List<short> _cache;

        public CallbackMultiplexer(List<short> cache, int index, DeviceBinding.ValueChanged mappingUpdate)
        {
            _mappingUpdate = mappingUpdate;
            _index = index;
            _cache = cache;
        }

        public void Update(short value)
        {
            _cache[_index] = value;
            _mappingUpdate?.Invoke(value);
        }

        ~CallbackMultiplexer()
        {
            _mappingUpdate = null;
        }
    }
}
