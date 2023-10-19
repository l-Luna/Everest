﻿using Celeste.Mod.Core;
using Celeste.Mod.Helpers;
using Monocle;
using MonoMod;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Threading;
using YamlDotNet.Serialization;

namespace Celeste.Mod {
    /// <summary>
    /// RUN AWAY. TURN AROUND. GO TO CELESTE'S MAIN FUNCTION INSTEAD.
    /// </summary>
    internal static class BOOT {

        [MakeEntryPoint]
        private static void Main(string[] args) {
            try {
                // 0.1 parses into 1 in regions using ,
                // This also somehow sets the exception message language to English.
                CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
                CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

                string everestPath = typeof(Celeste).Assembly.Location;

                // Launching Celeste.exe from a shortcut can sometimes set cwd to System32 on Windows.
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    Environment.CurrentDirectory = Path.GetDirectoryName(everestPath);

                try {
                    if (RestartViaLauncher())
                        return;
                } catch {
                }

                // Load the compatibility mode setting
                Everest.CompatibilityMode = Everest.CompatMode.None;
                try {
                    string path = patch_UserIO.GetSaveFilePath("modsettings-Everest");
                    if (File.Exists(path)) {
                        using Stream stream = File.OpenRead(path);
                        using StreamReader reader = new StreamReader(stream);
                        Dictionary<object, object> settings = new Deserializer().Deserialize<Dictionary<object, object>>(reader);
                        if (settings.TryGetValue(nameof(CoreModuleSettings.CompatibilityMode), out object val)) {
                            Everest.CompatibilityMode = Enum.Parse<Everest.CompatMode>((string) val);
                            Console.WriteLine($"Loaded compatibility mode setting: {Everest.CompatibilityMode}");
                        }
                    }
                } catch (Exception ex) {
                    LogError("COMPAT-MODE-LOAD", ex);
                    goto Exit;
                }

                // Handle the legacy FNA compatibility mode here, so that vanilla is also affected
                if (Everest.CompatibilityMode == Everest.CompatMode.LegacyFNA) {
                    Environment.SetEnvironmentVariable("FNA3D_D3D11_NO_FLIP_MODEL", "1");
                    Environment.SetEnvironmentVariable("FNA3D_D3D11_NO_EXCLUSIVE_FULLSCREEN", "1");
                }

                // Start vanilla if instructed to
                string vanillaDummy = Path.Combine(Path.GetDirectoryName(everestPath), "nextLaunchIsVanilla.txt");
                if (File.Exists(vanillaDummy) || args.FirstOrDefault() == "--vanilla") {
                    File.Delete(vanillaDummy);
                    StartVanilla();
                    goto Exit;
                }

                // Required for native libs to be picked up on Linux / MacOS
                SetupNativeLibPaths();

                patch_Celeste.Main(args);

                if (AppDomain.CurrentDomain.GetData("EverestRestart") as bool? ?? false) {
                    // Restart the original process
                    // This is as fast as the old "fast restarts" were
                    StartCelesteProcess();
                    goto Exit;
                } else if (Everest.RestartVanilla) {
                    // Start the vanilla process
                    StartVanilla();
                    goto Exit;
                }
            } catch (Exception e) {
                LogError("BOOT-CRITICAL", e);
                goto Exit;
            }


            // Needed because certain graphics drivers and native libs like to hang around for no reason.
            // Vanilla does the same on macOS and Linux, but NVIDIA on Linux likes to waste time in DrvValidateVersion.
            Exit:
            Console.WriteLine("Exiting Celeste process");
            Environment.Exit(0);
        }

        public static void LogError(string tag, Exception e) {
            e.LogDetailed(tag);

            if (Debugger.IsAttached)
                Debugger.Break();

            try {
                ErrorLog.Write(e.ToString());
                ErrorLog.Open();
            } catch { }
        }

        [MonoModIgnore]
        private static extern bool RestartViaLauncher();

        [MonoModIfFlag("Steamworks")]
        [MonoModPatch("RestartViaLauncher")]
        [MonoModReplace]
        private static bool RestartViaSteam() {
            return SteamAPI.RestartAppIfNecessary(new AppId_t(504230));
        }

        [MonoModIfFlag("NoLauncher")]
        [MonoModPatch("RestartViaLauncher")]
        [MonoModReplace]
        private static bool RestartViaNoLauncher() {
            return false;
        }

        private static void SetupNativeLibPaths() {
            // MacOS SIP Steam overlay hack (taken from the Linux launcher script - is this required?)
            bool didApplySteamSIPHack = false;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) &&
                !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("STEAM_DYLD_INSERT_LIBRARIES")) &&
                string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DYLD_INSERT_LIBRARIES"))
            ) {
                Console.WriteLine("Applying Steam DYLD_INSERT_LIBRARIES...");
                Environment.SetEnvironmentVariable("DYLD_INSERT_LIBRARIES", Environment.GetEnvironmentVariable("STEAM_DYLD_INSERT_LIBRARIES"));
                didApplySteamSIPHack = true;
            }

            // This is a bit hacky, but I'm not getting MiniInstaller to set an rpath ._.
            static void EnsureLibPathEnvVarSet(string envVar, string libPath) {
                libPath = Path.GetFullPath(libPath);

                string[] ldPath = Environment.GetEnvironmentVariable(envVar)?.Split(":") ?? Array.Empty<string>();
                if (!ldPath.Any(path => !string.IsNullOrWhiteSpace(path) && Path.GetFullPath(path) == libPath)) {
                    Environment.SetEnvironmentVariable(envVar, $"{libPath}:{Environment.GetEnvironmentVariable(envVar)}");
                    Console.WriteLine($"Restarting with {envVar}=\"{Environment.GetEnvironmentVariable(envVar)}\"...");

                    Process proc = StartCelesteProcess(clearFNAEnv: false);
                    proc.WaitForExit();
                    Environment.Exit(proc.ExitCode);
                }
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                SetDllDirectory(Path.Combine(AppContext.BaseDirectory, $"lib64-win-{(Environment.Is64BitProcess ? "x64" : "x86")}")); // Windows is the only platform with an API like this

                // Register an unmanaged DLL resolver so that we can take redirect fmod.dll to fmod64.dll or fmodL.dll as available
                AssemblyLoadContext.Default.ResolvingUnmanagedDll += static (_, name) => {
                    if (!(name.Equals("fmod", StringComparison.OrdinalIgnoreCase)
                          || name.Equals("fmod.dll", StringComparison.OrdinalIgnoreCase)
                          || name.Equals("fmodL", StringComparison.OrdinalIgnoreCase)
                          || name.Equals("fmodL.dll", StringComparison.OrdinalIgnoreCase)))
                        return IntPtr.Zero;

                    return NativeLibrary.TryLoad("fmod64L.dll", out IntPtr handle64L) ? handle64L :
                        NativeLibrary.TryLoad("fmod64.dll", out IntPtr handle64) ? handle64 :
                        NativeLibrary.TryLoad("fmodL.dll", out IntPtr handleL) ? handleL :
                        NativeLibrary.TryLoad("fmod.dll", out IntPtr handle) ? handle :
                        throw new DllNotFoundException("Unable to load any FMOD library 'fmod(64)(L).dll'.");
                };
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                EnsureLibPathEnvVarSet("LD_LIBRARY_PATH", Path.Combine(AppContext.BaseDirectory, "lib64-linux"));
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                EnsureLibPathEnvVarSet("DYLD_LIBRARY_PATH", Path.Combine(AppContext.BaseDirectory, "lib64-osx"));

            // If we got here without restarting the process, restart it now if required
            if (didApplySteamSIPHack) {
                Process proc = StartCelesteProcess(clearFNAEnv: false);
                proc.WaitForExit();
                Environment.Exit(proc.ExitCode);
            }

        }

        [SupportedOSPlatform("windows")]
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        public static Process StartCelesteProcess(string gameDir = null, bool clearFNAEnv = true) {
            gameDir ??= AppContext.BaseDirectory;

            Process game = new Process();

            game.StartInfo.FileName = Path.Combine(gameDir,
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Celeste.exe" :
                RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Celeste" :
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "Celeste" :
                throw new Exception("Unknown OS platform")
            );
            game.StartInfo.WorkingDirectory = gameDir;

            if (clearFNAEnv) {
                game.StartInfo.Environment.Clear();
                foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables()) {
                    string name = (string) entry.Key;
                    if (name.StartsWith("FNA_") || name.StartsWith("FNA3D_"))
                        continue;
                    game.StartInfo.Environment.Add(name, (string) entry.Value);
                }
            }

            Regex escapeArg = new Regex(@"(\\+)$");
            game.StartInfo.Arguments = string.Join(" ", Environment.GetCommandLineArgs().Select(s => "\"" + escapeArg.Replace(s, @"$1$1") + "\""));

            game.Start();
            return game;
        }

        public static void StartVanilla() {
            // Revert native library path to prevent accidentally messing with vanilla
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                SetDllDirectory(null);
            else
                Environment.SetEnvironmentVariable("LD_LIBRARY_PATH", null);
            
            // Don't clear FNA vars to preserve FNA compat mode
            StartCelesteProcess(Path.Combine(AppContext.BaseDirectory, "orig"), clearFNAEnv: false);
        }

    }
}
