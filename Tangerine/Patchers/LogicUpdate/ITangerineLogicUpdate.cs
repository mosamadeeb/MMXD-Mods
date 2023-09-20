using System;

namespace Tangerine.Patchers.LogicUpdate
{
    public interface ITangerineLogicUpdate
    {
        IntPtr LogicPointer { get; }

        void LogicUpdate();
    }
}
