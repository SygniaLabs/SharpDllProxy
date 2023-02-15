using System;
using System.IO;
using System.Linq;

namespace SharpDllProxy
{
    class Program
    {
        public static string dllTemplate = @"
// Auto Generated, do not commit this file!
#include <Windows.h>
#include <stdio.h>
#include <stdlib.h>

extern ""C""
__declspec(dllexport)
void
waiter(void)
{
    Sleep(INFINITE);
}

#define _CRT_SECURE_NO_DEPRECATE
#pragma warning (disable : 4996)

        PRAGMA_COMMENTS



";

        static void Main(string[] args)
        {
            //Cheesy way to generate a temp filename for our original DLL
            var tempName = Path.GetFileNameWithoutExtension(Path.GetTempFileName());

            var orgDllPath = @"";
            string outPath = @"";

            var pragmaBuilder = "";

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].ToLower().Equals("--dll") || args[i].ToLower().Equals("-dll"))
                {
                    if (i + 1 < args.Length)
                        orgDllPath = Path.GetFullPath(args[i + 1]);
                }
                if (args[i].ToLower().Equals("--output") || args[i].ToLower().Equals("-output"))
                {
                    if (i + 1 < args.Length)
                        outPath = Path.GetFullPath(args[i + 1]);
                }
            }
            if (string.IsNullOrWhiteSpace(orgDllPath) || !File.Exists(orgDllPath)) {
                Console.WriteLine($"[!] Cannot locate DLL path, does it exists?");
                Environment.Exit(0);
            }

            Console.WriteLine($"[+] Reading exports from {orgDllPath}...");

            //Read PeHeaders -> Exported Functions from provided DLL
            PeNet.PeFile dllPeHeaders = new PeNet.PeFile(orgDllPath);

           //Build up our linker redirects
            foreach (var exportedFunc in dllPeHeaders.ExportedFunctions)
            {
                pragmaBuilder += $@"
#ifdef _TAKE_OVER_
#pragma comment(linker, ""/export:{exportedFunc.Name}=waiter"")
#else
#pragma comment(linker, ""/export:{exportedFunc.Name}={tempName}.{exportedFunc.Name},@{exportedFunc.Ordinal}"")
#endif	
";
            }
            Console.WriteLine($"[+] Redirected {dllPeHeaders.ExportedFunctions.Count()} function calls from {Path.GetFileName(orgDllPath)} to {tempName}.dll");

            //Replace data in our template
            dllTemplate = dllTemplate.Replace("PRAGMA_COMMENTS", pragmaBuilder);

            Console.WriteLine($"[+] Exporting DLL C source");

            File.WriteAllText(outPath + @"\" + "MocExports.cpp", dllTemplate);
            File.WriteAllBytes(outPath + @"\" + tempName + ".dll", File.ReadAllBytes(orgDllPath));
        }
    }
}
