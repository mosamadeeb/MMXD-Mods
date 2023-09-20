using System;
using System.Collections.Generic;

namespace Tangerine.Patchers.LogicUpdate
{
    /// <summary>
    /// Contains methods that allow using <see cref="GameLogicUpdateManager"/> without inheriting from <see cref="ILogicUpdate"/>
    /// </summary>
    public static class TangerineLogicUpdateManager
    {
        private readonly static Dictionary<IntPtr, ILogicUpdate> _logicDict = new();

        private static ILogicUpdate GetOrAddLogic(ITangerineLogicUpdate logicUpdate)
        {
            if (!_logicDict.TryGetValue(logicUpdate.LogicPointer, out var logic))
            {
                logic = new(logicUpdate.LogicPointer);
            }

            return logic;
        }

        /// <summary>
        /// Calls <see cref="GameLogicUpdateManager.AddUpdate(ILogicUpdate)"/>
        /// </summary>
        /// <param name="logicUpdate">Object registered in Il2Cpp that has an implementation of <see cref="ITangerineLogicUpdate.LogicUpdate"/></param>
        public static void AddUpdate(ITangerineLogicUpdate logicUpdate)
        {
            GameLogicUpdateManager.Instance.AddUpdate(GetOrAddLogic(logicUpdate));
        }

        /// <summary>
        /// Calls <see cref="GameLogicUpdateManager.CheckUpdateContain(ILogicUpdate)"/>
        /// </summary>
        /// <inheritdoc cref="AddUpdate(ITangerineLogicUpdate)"/>
        /// <returns>The result of the method call</returns>
        public static bool CheckUpdateContain(ITangerineLogicUpdate logicUpdate)
        {
            return GameLogicUpdateManager.Instance.CheckUpdateContain(GetOrAddLogic(logicUpdate));
        }

        /// <summary>
        /// Calls <see cref="GameLogicUpdateManager.RemoveUpdate(ILogicUpdate)"/>
        /// </summary>
        /// <inheritdoc cref="AddUpdate(ITangerineLogicUpdate)"/>
        public static void RemoveUpdate(ITangerineLogicUpdate logicUpdate)
        {
            GameLogicUpdateManager.Instance.RemoveUpdate(GetOrAddLogic(logicUpdate));
            _logicDict.Remove(logicUpdate.LogicPointer);
        }
    }
}
