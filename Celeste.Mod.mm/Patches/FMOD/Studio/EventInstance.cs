using MonoMod;
using System;
using System.Runtime.InteropServices;
using System.Text;

// ReSharper disable once CheckNamespace
namespace FMOD.Studio;

public class patch_EventInstance : EventInstance {
    public patch_EventInstance(IntPtr raw) : base(raw) {}

    [MonoModReplace]
    public new RESULT getParameterValue(string name, out float value, out float finalValue) =>
        FMOD_Studio_EventInstance_GetParameterByName(rawPtr, Encoding.UTF8.GetBytes(name + "\0"), out value, out finalValue);

    [MonoModReplace]
    public new RESULT setParameterValue(string name, float value) =>
        FMOD_Studio_EventInstance_SetParameterByName(rawPtr, Encoding.UTF8.GetBytes(name + "\0"), value, false);

    /*[MonoModReplace]
    public RESULT getParameter(string name, out ParameterInstance instance) {
        instance = null;
        RESULT parameter2 = FMOD_Studio_EventInstance_GetParameterDescriptionByName(rawPtr, Encoding.UTF8.GetBytes(name + "\0"), out IntPtr parameter1);
        if (parameter2 != RESULT.OK)
            return parameter2;
        instance = new ParameterInstance(parameter1);
        return parameter2;
    }

    [MonoModReplace]
    public RESULT getParameterCount(out int count) => FMOD_Studio_EventInstance_GetParameterDescriptionCount(rawPtr, out count);

    [MonoModReplace]
    public RESULT getParameterByIndex(int index, out ParameterInstance instance) {
        instance = null;
        RESULT parameterByIndex = FMOD_Studio_EventInstance_GetParameterDescriptionByIndex(rawPtr, index, out IntPtr parameter);
        if (parameterByIndex != RESULT.OK)
            return parameterByIndex;
        instance = new ParameterInstance(parameter);
        return parameterByIndex;
    }*/

    [MonoModReplace]
    public new RESULT triggerCue() =>
        FMOD_Studio_EventInstance_KeyOff(rawPtr);

    [DllImport("fmodstudio")]
    private static extern RESULT FMOD_Studio_EventInstance_GetParameterByName(
        IntPtr _event,
        byte[] name,
        out float value,
        out float finalvalue
    );

    [DllImport("fmodstudio")]
    private static extern RESULT FMOD_Studio_EventInstance_SetParameterByName(
        IntPtr _event,
        byte[] name,
        float value,
        bool ignoreSeekSpeed
    );
    
    /*[DllImport("fmodstudio")]
    private static extern RESULT FMOD_Studio_EventInstance_GetParameterDescriptionByName(
        IntPtr _event,
        byte[] name,
        out IntPtr parameter);

    [DllImport("fmodstudio")]
    private static extern RESULT FMOD_Studio_EventInstance_GetParameterDescriptionByIndex(
        IntPtr _event,
        int index,
        out IntPtr parameter);

    [DllImport("fmodstudio")]
    private static extern RESULT FMOD_Studio_EventInstance_GetParameterDescriptionCount(
        IntPtr _event,
        out int count);*/

    [DllImport("fmodstudio")]
    private static extern RESULT FMOD_Studio_EventInstance_KeyOff(IntPtr _event);
}