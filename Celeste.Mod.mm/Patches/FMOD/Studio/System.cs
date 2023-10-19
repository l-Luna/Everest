using MonoMod;
using System;
using System.Runtime.InteropServices;

#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0414 // The field is assigned to, but never used

// ReSharper disable once CheckNamespace
namespace FMOD.Studio;

public class patch_System : System {
    
    // shush compiler
    public patch_System(IntPtr raw) : base(raw){}
    
    [MonoModReplace]
    public static RESULT create(out System system) {
        system = null;
        RESULT result = FMOD_Studio_System_Create(out IntPtr systemPtr, 0x2_02_18U);
        if (result != RESULT.OK)
            return result;
        system = new System(systemPtr);
        return result;
    }

    [MonoModReplace]
    public RESULT getLowLevelSystem(out FMOD.System system) {
        system = null;
        RESULT coreSystem = FMOD_Studio_System_GetCoreSystem(rawPtr, out IntPtr sysPtr);
        if (coreSystem != RESULT.OK)
            return coreSystem;
        system = new FMOD.System(sysPtr);
        return coreSystem;
    }

    // this replace isn't strictly required, just for convenience
    [MonoModReplace]
    [DllImport("fmodstudio")]
    private static extern RESULT FMOD_Studio_System_Create(
        out IntPtr studiosystem,
        uint headerversion
    );
    
    [DllImport("fmodstudio")]
    private static extern RESULT FMOD_Studio_System_GetCoreSystem(
        IntPtr studiosystem,
        out IntPtr system
    );
}