using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace SixtyFive.Json
{
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class BuildResult
    {
        public BuildResult
        (
            long code,
            bool okToCache,
            List<TextWrapper> stdout,
            List<TextWrapper> stderr,
            string inputFilename,
            string executableFilename,
            List<string> compilationOptions
        )
        {
            Code = code;
            OkToCache = okToCache;
            Stdout = stdout;
            Stderr = stderr;
            InputFilename = inputFilename;
            ExecutableFilename = executableFilename;
            CompilationOptions = compilationOptions;
        }

        public long Code { get; set; }

        public bool OkToCache { get; set; }

        public List<TextWrapper> Stdout { get; set; }

        public List<TextWrapper> Stderr { get; set; }

        public string InputFilename { get; set; }

        public string ExecutableFilename { get; set; }

        public List<string> CompilationOptions { get; set; }
    }
}