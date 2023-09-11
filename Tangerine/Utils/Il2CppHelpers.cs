using CallbackDefs;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using StageLib;
using System;
using UnityEngine;

namespace Tangerine.Utils
{
    public static class Il2CppHelpers
    {
        /// <summary>
        /// Alternative implementation of <see cref="FxManager.Play(string, Vector3, Quaternion, Il2CppReferenceArray{Il2CppSystem.Object})"/>
        /// </summary>
        public static void FxManagerPlay(string p_fxName, Vector3 p_worldPos, Quaternion p_quaternion, Il2CppReferenceArray<Il2CppSystem.Object> p_params = null)
        {
            if (MonoBehaviourSingleton<PoolManager>.Instance.IsPreload(p_fxName))
            {
                FxBase poolObj = MonoBehaviourSingleton<PoolManager>.Instance.GetPoolObj<FxBase>(p_fxName);
                poolObj.transform.SetParent(null);
                poolObj.transform.SetPositionAndRotation(p_worldPos, p_quaternion);
                FxManager.Instance.RegisterFxBase(poolObj);
                poolObj.Active(p_params);
                if (p_params != null && p_params.Length != 0)
                {
                    StageFXParam stageFXParam = p_params[0] as StageFXParam;
                    if (stageFXParam != null)
                    {
                        if (stageFXParam.tFOL != null)
                        {
                            stageFXParam.tFOL.tObj = poolObj.gameObject;
                        }
                        FxManager.Instance.ChangeFXColor(poolObj, stageFXParam);
                        return;
                    }
                }
            }
            else
            {
                FxManager.Instance.PreloadFx(p_fxName, 1, (Callback)(new Action(() => FxManagerPlay(p_fxName, p_worldPos, p_quaternion, p_params))));
            }
        }

        /// <summary>
        /// Alternative implementation of <see cref="FxManager.Play(string, Transform, Quaternion, Il2CppReferenceArray{Il2CppSystem.Object})"/>
        /// </summary>
        public static void FxManagerPlay(string pFxName, Transform pTransform, Quaternion pQuaternion, Il2CppReferenceArray<Il2CppSystem.Object> pParams = null)
        {
            if (MonoBehaviourSingleton<PoolManager>.Instance.IsPreload(pFxName))
            {
                FxBase poolObj = MonoBehaviourSingleton<PoolManager>.Instance.GetPoolObj<FxBase>(pFxName);
                Vector3 localScale = poolObj.transform.localScale;
                poolObj.transform.SetParent(pTransform);
                poolObj.transform.localPosition = Vector3.zero;
                poolObj.transform.localRotation = pQuaternion;
                poolObj.transform.localScale = localScale;
                FxManager.Instance.RegisterFxBase(poolObj);
                poolObj.Active(pParams);
                if (pParams != null && pParams.Length != 0)
                {
                    StageFXParam stageFXParam = pParams[0] as StageFXParam;
                    if (stageFXParam != null)
                    {
                        if (stageFXParam.tFOL != null)
                        {
                            stageFXParam.tFOL.tObj = poolObj.gameObject;
                        }
                        FxManager.Instance.ChangeFXColor(poolObj, stageFXParam);
                        return;
                    }
                }
            }
            else
            {
                FxManager.Instance.PreloadFx(pFxName, 1, (Callback)(new Action(() => FxManagerPlay(pFxName, pTransform, pQuaternion, pParams))));
            }
        }

        /// <summary>
        /// Sets the value of a <see cref="Nullable{T}"/> field
        /// </summary>
        /// <typeparam name="T">Enclosed type</typeparam>
        /// <param name="obj">Object that contains the field</param>
        /// <param name="ptr">NativeFieldInfoPtr of the field</param>
        /// <param name="value">value to set</param>
        public static unsafe void SetNullable<T>(Il2CppObjectBase obj, IntPtr ptr, Il2CppSystem.Nullable_Unboxed<T> value) where T : unmanaged
        {
            *(Il2CppSystem.Nullable_Unboxed<T>*)(IL2CPP.Il2CppObjectBaseToPtrNotNull(obj) + (int)IL2CPP.il2cpp_field_get_offset(ptr)) = value;
        }
    }
}
