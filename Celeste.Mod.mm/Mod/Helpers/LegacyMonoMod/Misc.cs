using Mono.Cecil.Cil;
using MonoMod;
using MonoMod.Cil;
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace Celeste.Mod.Helpers.LegacyMonoMod {
    public static class ILShims {
        [RelinkLegacyMonoMod("Mono.Cecil.Cil.Instruction MonoMod.Cil.ILLabel::Target")]
        public static Instruction ILLabel_GetTarget(ILLabel label) => label.Target; // This previously used to be a field
    }

    [RelinkLegacyMonoMod("MonoMod.Utils.PlatformHelper")]
    public static class LegacyPlatformHelper {

        [Flags]
        [RelinkLegacyMonoMod("MonoMod.Utils.Platform")]
        public enum Platform : int {
            OS = 1 << 0,
            Bits64 = 1 << 1,
            NT = 1 << 2,
            Unix = 1 << 3,
            ARM = 1 << 16,
            Wine = 1 << 17,
            Unknown = OS | (1 << 4),
            Windows = OS | NT | (1 << 5),
            MacOS = OS | Unix | (1 << 6),
            Linux = OS | Unix | (1 << 7),
            Android = Linux | (1 << 8),
            iOS = MacOS | (1 << 9),
        }

        private static Platform? _current;
        public static Platform Current {
            get => _current ??=
                    RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? Platform.Windows :
                    RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? Platform.MacOS :
                    RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? Platform.Linux :
                    Platform.Unknown;
            set => throw new NotSupportedException("PlatformHelper.set_Current is no longer supported");
        }

        public static bool Is(Platform platform) => (Current & platform) == platform;

        private static string _librarySuffix;
        public static string LibrarySuffix => _librarySuffix ??= Is(Platform.MacOS) ? "dylib" : Is(Platform.Unix) ? "so" : "dll";

    }

    internal static class MonoModPolice {

        public sealed class MonoModCrimeException : Exception {
            public MonoModCrimeException(string descr) : base($"MONOMOD CRIME DETECTED - THIS IS A BUG: {descr}") {}
        }

        public static void ReportMonoModCrime(string descr, MethodBase perpetrator) {
            Module perpetratorMod = null;
            try {
                perpetratorMod = perpetrator.Module; // This can throw, so just to be safe wrap it in a try-catch
            } catch {}
            ReportMonoModCrime(descr, perpetratorMod);
        }

        public static void ReportMonoModCrime(string descr) => ReportMonoModCrime(descr, (Assembly) null);
        public static void ReportMonoModCrime(string descr, Module perpetrator) => ReportMonoModCrime(descr, perpetrator?.Assembly);
        public static void ReportMonoModCrime(string descr, Assembly perpetrator) {
            // Check if we can trace this back to an offending mod
            EverestModuleMetadata perpetratorMeta = null;
            if (perpetrator != null)
                perpetratorMeta = (AssemblyLoadContext.GetLoadContext(perpetrator) as EverestModuleAssemblyContext)?.ModuleMeta;

            // This means that a mod did something objectively wrong (=a bug in the mod)
            // But because it "used to worked":tm:, we can't give them the crash they deserve
            // So we at least yell at them loudly in the log file ._.
            Logger.Log(LogLevel.Error, "legacy-monomod", "##################################################################################");
            Logger.Log(LogLevel.Error, "legacy-monomod", "                              MONOMOD CRIME DETECTED                              ");
            Logger.Log(LogLevel.Error, "legacy-monomod", "##################################################################################");
            Logger.Log(LogLevel.Error, "legacy-monomod", "                 !!! This means one of your mods has a bug !!!                    ");
            Logger.Log(LogLevel.Error, "legacy-monomod", "   However, for the sake of backwards compatibility, a crash has been prevented   ");
            Logger.Log(LogLevel.Error, "legacy-monomod", "      Please report this to the mod author so that they can fix their mod!        ");
            Logger.Log(LogLevel.Error, "legacy-monomod", "");
            if (perpetratorMeta != null)
                Logger.Log(LogLevel.Error, "legacy-monomod", $"Suspected perpetrator: {perpetratorMeta.Name} version {perpetratorMeta.VersionString} [{perpetratorMeta.Version}]");
            Logger.Log(LogLevel.Error, "legacy-monomod", $"Details of infraction: {descr}");
            Logger.LogDetailed(LogLevel.Error, "legacy-monomod", $"Stacktrace:");

            // If we know that the offender is a directory mod (which implies that this is a mod dev), still crash >:)
            if (!string.IsNullOrEmpty(perpetratorMeta?.PathDirectory))
                throw new MonoModCrimeException(descr);
        }

    }
}