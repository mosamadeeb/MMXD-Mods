using Il2CppInterop.Runtime.InteropTypes;
using System;

namespace Tangerine.Patchers.LogicUpdate
{
    /// <summary>
    /// Mirror interface for implementing <see cref="ILogicUpdate"/>, since Il2Cpp converted it from an interface into a class.
    /// Objects that implement this can be passed to <see cref="TangerineLogicUpdateManager"/>.
    /// </summary>
    public interface ITangerineLogicUpdate
    {
        /// <summary>
        /// Pointer to the <see cref="Il2CppObjectBase"/> that implements <see cref="LogicUpdate"/>
        /// </summary>
        IntPtr LogicPointer { get; }

        /// <summary>
        /// Mirror method for <see cref="ILogicUpdate.LogicUpdate"/>
        /// </summary>
        void LogicUpdate();
    }
}
