using System.Collections.Generic;
using Hidra.Core.Managers;
using Hidra.Core.Models;
using Hidra.Core.Models.Binding;
using Hidra.IOWrapper.DataTransferObjects;
using NUnit.Framework;

namespace Hidra.Tests.ManagerTests
{
    // DevicesManager's public methods (GetAvailableDeviceList, RefreshDeviceList, ...) all go
    // through Context.IOController, the native device-enumeration layer, so they aren't
    // unit-testable without a real or mocked provider. BuildDeviceBindingMenu is the one piece
    // that's pure data transformation (IOWrapper's DeviceReportNode tree -> Hidra's own
    // DeviceBindingNode tree), so it's what's covered here.
    [TestFixture]
    internal class DevicesManagerTests
    {
        [Test]
        public void BuildDeviceBindingMenu_BuildsNestedTreeWithBindingInfo()
        {
            var deviceNodes = new List<DeviceReportNode>
            {
                new DeviceReportNode
                {
                    Title = "Buttons",
                    Bindings = new List<BindingReport>
                    {
                        new BindingReport
                        {
                            Title = "Button 1",
                            Category = BindingCategory.Momentary,
                            Blockable = true,
                            BindingDescriptor = new BindingDescriptor { Type = BindingType.Button, Index = 0, SubIndex = 0 }
                        }
                    }
                }
            };

            var result = DevicesManager.BuildDeviceBindingMenu(deviceNodes, DeviceIoType.Input);

            Assert.That(result, Has.Count.EqualTo(1));
            var groupNode = result[0];
            Assert.That(groupNode.Title, Is.EqualTo("Buttons"));
            Assert.That(groupNode.ChildrenNodes, Has.Count.EqualTo(1));

            var bindingNode = groupNode.ChildrenNodes[0];
            Assert.That(bindingNode.Title, Is.EqualTo("Button 1"));
            Assert.That(bindingNode.IsBinding, Is.True);
            Assert.That(bindingNode.DeviceBindingInfo.KeyType, Is.EqualTo((int)BindingType.Button));
            Assert.That(bindingNode.DeviceBindingInfo.DeviceBindingCategory, Is.EqualTo(DeviceBindingCategory.Momentary));
            Assert.That(bindingNode.DeviceBindingInfo.Blockable, Is.True);
        }

        [Test]
        public void BuildDeviceBindingMenu_ReturnsAnEmptyListForNullNodes()
        {
            var result = DevicesManager.BuildDeviceBindingMenu(null, DeviceIoType.Input);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);
        }

        // Characterization test: an empty (but non-null) node list falls through to the method's
        // "return result.Count != 0 ? result : null" instead of returning an empty list like the
        // null case above does. That asymmetry is existing behavior, not something introduced
        // here, so this locks in the actual contract rather than the possibly-more-intuitive one.
        [Test]
        public void BuildDeviceBindingMenu_ReturnsNullForAnEmptyNodeList()
        {
            var result = DevicesManager.BuildDeviceBindingMenu(new List<DeviceReportNode>(), DeviceIoType.Input);

            Assert.That(result, Is.Null);
        }
    }
}
