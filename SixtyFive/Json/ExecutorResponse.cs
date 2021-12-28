using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace SixtyFive.Json
{
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class ExecutorResponse
    {
        public ExecutorResponse
        (
            long code,
            bool okToCache,
            List<TextWrapper> stdout,
            List<TextWrapper> stderr,
            bool didExecute,
            BuildResult buildResult
        )
        {
            Code = code;
            OkToCache = okToCache;
            Stdout = stdout;
            Stderr = stderr;
            DidExecute = didExecute;
            BuildResult = buildResult;
        }

        public long Code { get; set; }

        public bool OkToCache { get; set; }

        public List<TextWrapper> Stdout { get; set; }

        public List<TextWrapper> Stderr { get; set; }

        public bool DidExecute { get; set; }

        public BuildResult BuildResult { get; set; }
    }
}