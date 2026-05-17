using AsmResolver.PE;
using AsmResolver.PE.Builder;
using AsmResolver.PE.File;
using AsmResolver.PE.Imports;
using System;
using System.IO;
using File = System.IO.File;

namespace Droute.Core
{
    public static class PatchManager
    {
        public const string MAIN_PROXY_DLL = "version.dll";
        public const string MAIN_PAYLOAD_DLL = "droute.dll";

        public enum ArchitectureBitness
        {
            Auto,
            Force64,
            Force32
        }

        public static void DuplicateProxy(string destPath, ArchitectureBitness bitness = ArchitectureBitness.Force64)
        {
            string windowsPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string sourceFolder;

            switch (bitness)
            {
                case ArchitectureBitness.Force64:
                    if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess)
                        sourceFolder = Path.Combine(windowsPath, "Sysnative");
                    else
                        sourceFolder = Environment.GetFolderPath(Environment.SpecialFolder.System);
                    break;

                case ArchitectureBitness.Force32:
                    if (Environment.Is64BitOperatingSystem)
                        sourceFolder = Path.Combine(windowsPath, "SysWOW64");
                    else
                        sourceFolder = Environment.GetFolderPath(Environment.SpecialFolder.System);
                    break;

                case ArchitectureBitness.Auto:
                default:
                    sourceFolder = Environment.GetFolderPath(Environment.SpecialFolder.System);
                    break;
            }

            string source = Path.Combine(sourceFolder, MAIN_PROXY_DLL);

            if (!File.Exists(source))
                throw new FileNotFoundException($"Required system component is missing: {source}");

            File.Copy(source, destPath, true);
        }

        public static void ApplyPEPatch(string filePath)
        {
            var peFile = PEFile.FromFile(filePath);
            var peImage = PEImage.FromFile(peFile);

            var myDll = new ImportedModule(MAIN_PAYLOAD_DLL);
            myDll.Symbols.Add(new ImportedSymbol(0, "DllMain"));
            peImage.Imports.Add(myDll);

            var builder = new TemplatedPEFileBuilder()
            {
                TrampolineImports = true
            };

            peFile = builder.CreateFile(peImage);
            peFile.Write(filePath);
        }

        public static void DeleteFile(string filePath)
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}
