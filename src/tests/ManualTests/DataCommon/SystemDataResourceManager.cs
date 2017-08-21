// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Dynamic;
using System.Reflection;

namespace System.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Allows user to get resource messages from system.data.sqlclient.dll using dynamic properties/methods.
    /// Refer to comments inside AssemblyResourceManager.cs for more details.
    /// </summary>
    public class SystemDataResourceManager : DynamicObject
    {
        private System.Reflection.Assembly _resourceAssembly;

        public static readonly dynamic Instance = new SystemDataResourceManager();

        public SystemDataResourceManager()
        {
            _resourceAssembly = typeof(SqlConnection).GetTypeInfo().Assembly;
        }

        public override IEnumerable<string> GetDynamicMemberNames() => Array.Empty<string>();

        /// <summary>
        /// enables dynamic property: asmResourceManager.ResourceName
        /// </summary>
        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            return TryGetResourceValue(binder.Name, null, out result);
        }

        /// <summary>
        /// enables dynamic property: asmResourceManager.ResourceName (params object[] args)
        /// This also support asmResourceManager.Get_ResourceName for old test as well
        /// </summary>
        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            var resourceName = binder.Name;
            if (resourceName.StartsWith("Get_"))
                resourceName = resourceName.Remove(0, 4);

            return TryGetResourceValue(resourceName, args, out result);
        }


        private bool TryGetResourceValue(string resourceName, object[] args, out object result)
        {
            var type = _resourceAssembly.GetType("System.Data.SqlClient.Resources.Res");
            var info = type.GetProperty(resourceName, BindingFlags.Public | BindingFlags.Static);

            result = null;
            if (info != null)
            {
                result = info.GetValue(null);
                if (args != null)
                {
                    result = string.Format((string)result, args);
                }
            }
            return result != null;
        }
    }
}