using System;
using System.Collections.Generic;

namespace Tangerine.Patchers.LogicUpdate
{
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
        /// 
        /// </summary>
        /// <param name="logicUpdate"></param>
        public static void AddUpdate(ITangerineLogicUpdate logicUpdate)
        {
            GameLogicUpdateManager.Instance.AddUpdate(GetOrAddLogic(logicUpdate));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="logicUpdate"></param>
        /// <returns></returns>
        public static bool CheckUpdateContain(ITangerineLogicUpdate logicUpdate)
        {
            return GameLogicUpdateManager.Instance.CheckUpdateContain(GetOrAddLogic(logicUpdate));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="logicUpdate"></param>
        public static void RemoveUpdate(ITangerineLogicUpdate logicUpdate)
        {
            GameLogicUpdateManager.Instance.RemoveUpdate(GetOrAddLogic(logicUpdate));
            _logicDict.Remove(logicUpdate.LogicPointer);
        }
    }
}
