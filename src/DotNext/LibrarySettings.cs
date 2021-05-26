using System;

namespace DotNext
{
    internal static class LibrarySettings
    {
        private const string StackallocThresholdEnvar = "DOTNEXT_STACK_ALLOC_THRESHOLD";
        private const int DefaultStackallocThreshold = 511;

        // TODO: Remove this switch in next major version and remove appropriate test for it
        private const string UndefinedEqualsNullSwitch = "DotNext.Optional.UndefinedEqualsNull";

        internal static int StackallocThreshold
        {
            get
            {
                if (!int.TryParse(Environment.GetEnvironmentVariable(StackallocThresholdEnvar), out int result) || result < 16)
                    result = DefaultStackallocThreshold;
                else if ((result & 1) == 0)
                    result = checked(result - 1);

                return result;
            }
        }

        // TODO: Remove this switch in next major version
        internal static bool IsUndefinedEqualsNull
            => AppContext.TryGetSwitch(UndefinedEqualsNullSwitch, out var enabled) ? enabled : false;
    }
}