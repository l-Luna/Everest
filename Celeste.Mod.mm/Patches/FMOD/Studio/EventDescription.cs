using System;
using System.Runtime.InteropServices;
using System.Text;

// ReSharper disable once CheckNamespace
namespace FMOD.Studio;

public class patch_EventDescription : EventDescription {
    public patch_EventDescription(IntPtr raw) : base(raw) {}

    public RESULT getParameterCount(out int count) => FMOD_Studio_EventDescription_GetParameterDescriptionCount(rawPtr, out count);

    public RESULT getParameterByIndex(int index, out PARAMETER_DESCRIPTION parameter) {
        parameter = new PARAMETER_DESCRIPTION();
        RESULT res = FMOD_Studio_EventDescription_GetParameterDescriptionByIndex(rawPtr, index, out PARAMETER_DESCRIPTION_INTERNAL param);
        if (res != RESULT.OK)
            return res;
        param.assign(out parameter);
        return res;
    }

    public RESULT getParameter(string name, out PARAMETER_DESCRIPTION parameter) {
        parameter = new PARAMETER_DESCRIPTION();
        RESULT res = FMOD_Studio_EventDescription_GetParameterDescriptionByName(rawPtr, Encoding.UTF8.GetBytes(name + "\0"), out PARAMETER_DESCRIPTION_INTERNAL param);
        if (res != RESULT.OK)
            return res;
        param.assign(out parameter);
        return res;
    }

    [DllImport("fmodstudio")]
    private static extern RESULT FMOD_Studio_EventDescription_GetParameterDescriptionCount(
        IntPtr eventdescription,
        out int count);

    [DllImport("fmodstudio")]
    private static extern RESULT FMOD_Studio_EventDescription_GetParameterDescriptionByIndex(
        IntPtr eventdescription,
        int index,
        out PARAMETER_DESCRIPTION_INTERNAL parameter);

    [DllImport("fmodstudio")]
    private static extern RESULT FMOD_Studio_EventDescription_GetParameterDescriptionByName(
        IntPtr eventdescription,
        byte[] name,
        out PARAMETER_DESCRIPTION_INTERNAL parameter);
    
    // it's not called internal for nothing
    internal struct PARAMETER_DESCRIPTION_INTERNAL {
        public IntPtr name;
        public int index;
        public float minimum;
        public float maximum;
        public float defaultvalue;
        public PARAMETER_TYPE type;

        public void assign(out PARAMETER_DESCRIPTION publicDesc) {
            publicDesc.name = stringFromNativeUtf8(name);
            publicDesc.index = index;
            publicDesc.minimum = minimum;
            publicDesc.maximum = maximum;
            publicDesc.defaultvalue = defaultvalue;
            publicDesc.type = type;
        }
    }

    // TODO: put these somewhere better please
    internal static int stringLengthUtf8(IntPtr nativeUtf8) {
        int ofs = 0;
        while (Marshal.ReadByte(nativeUtf8, ofs) != 0)
            ++ofs;
        return ofs;
    }

    internal static string stringFromNativeUtf8(IntPtr nativeUtf8) {
        int count = stringLengthUtf8(nativeUtf8);
        if (count == 0)
            return string.Empty;
        byte[] numArray = new byte[count];
        Marshal.Copy(nativeUtf8, numArray, 0, numArray.Length);
        return Encoding.UTF8.GetString(numArray, 0, count);
    }
}