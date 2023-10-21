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

    [DllImport("fmodstudio")]
    private static extern RESULT FMOD_Studio_EventInstance_KeyOff(IntPtr _event);
}